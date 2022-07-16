/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting;
using System.Text.RegularExpressions;
using Hybrasyl.Casting;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;
using Sentry;
using Serilog;
using StackExchange.Redis;
using User = Hybrasyl.Objects.User;

namespace Hybrasyl.Scripting;

/// <summary>
/// A logging class that can be used by scripts natively in Lua.
/// </summary>
[MoonSharpUserData]
    
public class ScriptLogger
{
        
    public string ScriptName { get; set; }

    public ScriptLogger(string name)
    {
        ScriptName = name;
    }

    public void Info(string message) => Log.Information("{ScriptName} : {Message}", ScriptName, message);
    public void Error(string message) => Log.Error("{ScriptName} : {Message}", ScriptName, message);
}

public class Script
{
    static readonly Regex LuaRegex = new(@"(.*):\(([0-9]*),([0-9]*)-([0-9]*)\): (.*)$");

    public string RawSource { get; set; }

    public string Name { get; set; }
    public string FullPath { get; private set; }
    public string FileName => Path.GetFileName(FullPath);

    public ScriptProcessor Processor { get; set; }
    public MoonSharp.Interpreter.Script Compiled { get; private set; }
    public HybrasylWorldObject Associate { get; private set; }

    public bool Disabled { get; set; }

    public ScriptExecutionResult LoadExecutionResult { get; set; }

    public Script Clone()
    {
        var clone = new Script(FullPath, Processor);
        // A clone doesn't need to run OnLoad again, which is guaranteed to be evaluated 
        // only once per script (aka this.Compiled) lifetime
        clone.Run(false);
        return clone;
    }

    public Script(string path, ScriptProcessor processor)
    {
        FullPath = path;
        Name = Path.GetFileName(path).ToLower();
        Compiled = new MoonSharp.Interpreter.Script(CoreModules.Preset_SoftSandbox);
        RawSource = string.Empty;
        Processor = processor;
    }

    public Script(string script, string name)
    {
        FullPath = string.Empty;
        Name = name;
        Compiled = new MoonSharp.Interpreter.Script(CoreModules.Preset_SoftSandbox);
        RawSource = script;
    }

    public void AssociateScriptWithObject(WorldObject obj)
    {
        if (Associate?.Obj?.Id == obj.Id)
            return;

        Associate = new HybrasylWorldObject(obj);
        if (obj is VisibleObject vo)
            Compiled.Globals.Set("map", UserData.Create(new HybrasylMap(vo.Map)));
        Compiled.Globals.Set("associate", UserData.Create(Associate));
        Compiled.Globals.Set("origin", UserData.Create(Associate));
        obj.Script = this;
    }

    public ScriptExecutionResult Reload()
    {
        Compiled = new MoonSharp.Interpreter.Script(CoreModules.Preset_SoftSandbox);
        return Run();
    }

    /// <summary>
    /// Function to dynamically make the right UserData wrapper for a given object.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static DynValue GetUserDataValue(dynamic obj)
    {
        if (obj == null)
            return DynValue.NewNil();

        return obj switch
        {
            bool => DynValue.NewBoolean(obj),
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => DynValue
                .NewNumber(obj),
            string => DynValue.NewString(obj),
            User user => UserData.Create(new HybrasylUser(user)),
            Monster monster => UserData.Create(new HybrasylMonster(monster)),
            World world => UserData.Create(new HybrasylWorld(world)),
            Map map => UserData.Create(new HybrasylMap(map)),
            Reactor reactor => UserData.Create(new HybrasylReactor(reactor)),
            ItemObject item => UserData.Create(new HybrasylItemObject(item)),
            WorldObject wobj => UserData.Create(new HybrasylWorldObject(wobj)),
            HybrasylInteractable hi => UserData.Create(hi),
            // todo: wrapper?
            CastableObject co => UserData.Create(co),
            _ => DynValue.NewNil()
        };
    }

    private string ExtractLuaSource(int linenumber)
    {
        var lines = RawSource.Split('\n').ToList();
        if (lines.Count < linenumber)
            return RawSource;
        var lua = $"## {lines[linenumber - 2]}\n## --->{lines[linenumber - 1]}\n";
        if (linenumber < lines.Count)
            lua = $"{lua}## {lines[linenumber]}";
        return lua;
    }

    private ScriptExecutionError HumanizeException(Exception ex)
    {
        var ret = new ScriptExecutionError();

        if (ex is InterpreterException ie)
        {
            // Get information from decorated message. CallStack is only available for
            // certain lua exceptions; decorated message / innerexception always has what
            // is needed.
            var matches = LuaRegex.Match(ie.DecoratedMessage);
            if (matches.Success && matches.Groups.Count == 6)
            {
                ret.Filename = Path.GetFileName(matches.Groups[1].Value);
                ret.LineNumber = int.Parse(matches.Groups[2].Value);
                ret.Error = matches.Groups[5].Value;
            }
            else
                ret.Error = ie.DecoratedMessage;

            var summary = !string.IsNullOrEmpty(ret.Filename)
                ? $"Line {ret.LineNumber}: {ret.Error}\n{ExtractLuaSource(ret.LineNumber)}"
                : $"Could not be parsed, raw message follows {ie.DecoratedMessage}";

            ret.HumanizedError = ie switch
            {
                SyntaxErrorException => $"Syntax error: {summary}",
                DynamicExpressionException => $"\nLua dynamic expression error: {summary}",
                ScriptRuntimeException => $"\nScripting runtime error: {summary}",
                _ => $"\nInternal error from Lua code: STACK: {summary}\nERR: {ie.DecoratedMessage}"
            };
            ret.ErrorType = ie switch
            {
                SyntaxErrorException => ScriptErrorType.SyntaxError,
                DynamicExpressionException => ScriptErrorType.DynamicExpressionError,
                ScriptRuntimeException => ScriptErrorType.RuntimeError,
                InternalErrorException => ScriptErrorType.InternalError,
                _ => ScriptErrorType.Unknown
            };
        }

        // C# or other exception
        ret.HumanizedError = $"\nC# exception, perhaps caused by Lua script code: STACK: {ex.StackTrace}\n ERR:{ex.Message}";
        ret.ErrorType = ScriptErrorType.CSharpError;
        if (ex.InnerException != null)
            ret.HumanizedError = $"{ret.HumanizedError}\nINNER STACK TRACE: {ex.InnerException.StackTrace}\n INNER ERR: {ex.InnerException.Message}";
        return ret;
    }

    /// <summary>
    /// Check to see if a script implements a given function.
    /// </summary>
    /// <param name="name">The function name to check</param>
    /// <returns></returns>
    public bool HasFunction(string name)
    {
        return !Equals(Compiled.Globals.Get(name), DynValue.Nil);
    }

    public void SetGlobals()
    {
        Compiled.Globals["Gender"] = UserData.CreateStatic<Xml.Gender>();
        Compiled.Globals["LegendIcon"] = UserData.CreateStatic<LegendIcon>();
        Compiled.Globals["LegendColor"] = UserData.CreateStatic<LegendColor>();
        Compiled.Globals["Class"] = UserData.CreateStatic<Xml.Class>();
        Compiled.Globals["utility"] = typeof(HybrasylUtility);
        Compiled.Globals.Set("world", UserData.Create(Processor.World));
        Compiled.Globals.Set("logger", UserData.Create(new ScriptLogger(Name)));
        Compiled.Globals.Set("this_script", DynValue.NewString(Name));
    }

    /// <summary>
    /// Load the script from disk and execute it.
    /// </summary>
    /// <param name="onLoad">Whether or not to execute OnLoad() if it exists in the script.</param>
    /// <returns>boolean indicating whether the script was reloaded or not</returns>
    public ScriptExecutionResult Run(bool onLoad = true)
    {
        var result = new ScriptExecutionResult();
        try
        {
            SetGlobals();
            // Load file into RawSource so we have access to it later
            RawSource = File.ReadAllText(FullPath);
            result.Return = Compiled.DoFile(FullPath);
            result.Result = ScriptResult.Success;
            LoadExecutionResult = result;
            if (!onLoad)
                return result;
            GameLog.ScriptingInfo($"Loading: {Path.GetFileName(FullPath)}");
            ExecuteFunction("OnLoad");
            Disabled = false;
            return result;
        }
        catch (Exception ex)
        {
            Game.ReportException(ex);
            result.Error = HumanizeException(ex);
            result.Result = ScriptResult.Failure;
            LoadExecutionResult = result;
            GameLog.ScriptingError("Run: Error executing script {FileName} (associate {assoc}): {Message}",
                FileName, Associate?.Name ?? "none", result.Error.HumanizedError);
            Disabled = true;
            return result;
        }
    }

    public void ProcessEnvironment(ScriptEnvironment env)
    {
        if (env is null) return;
        foreach (var (key, value) in env.Variables)
        {
            Compiled.Globals.Set(key, GetUserDataValue(value));
        }
    }

    /// <summary>
    /// Execute a Lua expression in the context of an associated world object.
    /// Primarily used for dialog callbacks.
    /// </summary>
    /// <param name="expr">The javascript expression, in string form.</param>
    /// <param name="invoker">The invoker (caller).</param>
    /// <param name="source">Optionally, the source of the script call, invocation or dialog</param>
    /// <returns></returns>
    public ScriptExecutionResult ExecuteExpression(string expr, ScriptEnvironment environment = null)
    {
        var result = new ScriptExecutionResult
        {
            Result = ScriptResult.Disabled, Return = DynValue.Nil, ExecutedExpression = expr,
            Location = environment?.DialogPath, ExecutionTime = DateTime.Now
        };


        if (Disabled)
        {
            if (Associate != null)
                Associate.Obj.LastExecutionResult = result;
            return result;
        }

        try
        {
            ProcessEnvironment(environment);
            // We pass Compiled.Globals here to make sure that the updated table (with new variables) makes it over
            result.Return = Compiled.DoString(expr, Compiled.Globals);
            result.Result = ScriptResult.Success;
        }
        catch (Exception ex)
        {
            Game.ReportException(ex);
            result.Error = HumanizeException(ex);
            GameLog.ScriptingError(
                $"ExecuteExpression: Error executing expression {expr} in {FileName}: Variables: {environment}\n{result.Error}");
        }
        if (Associate != null)
            Associate.Obj.LastExecutionResult = result;

        return result;
    }

    public ScriptExecutionResult ExecuteFunction(string functionName, ScriptEnvironment environment = null)
    {
        var result = new ScriptExecutionResult
        {
            Result = ScriptResult.Disabled, Return = DynValue.Nil, ExecutedExpression = functionName,
            Location = environment?.DialogPath, ExecutionTime = DateTime.Now
        };

        if (Disabled)
        {
            if (Associate != null)
                Associate.Obj.LastExecutionResult = result;
            return result;
        }

        try
        {
            if (!HasFunction(functionName))
            {
                result.Result = ScriptResult.FunctionMissing;
            }
            else
            {
                ProcessEnvironment(environment);
                result.Return = Compiled.Call(Compiled.Globals[functionName]);
                result.Result = ScriptResult.Success;
            }
        }
        catch (Exception ex)
        {
            Game.ReportException(ex);
            result.Error = HumanizeException(ex);
            result.Result = ScriptResult.Failure;
            GameLog.ScriptingError(
                $"ExecuteFunction: Error executing expression {functionName} in {FileName}: Variables: {environment}\n{result.Error}");
        }
        if (Associate != null)
            Associate.Obj.LastExecutionResult = result;
        return result;

    }


}