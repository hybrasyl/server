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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;
using Serilog;

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

public struct LuaException
{
    public string Filename;
    public int LineNumber;
    public string Error;
};

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
    public string CompilationError { get; private set; } = string.Empty;
    public string LastRuntimeError { get; private set; } = string.Empty;

    public DynValue LastReturnValue { get; set; } = DynValue.Nil;

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
        obj.Script = this;
    }

    public static dynamic GetObjectWrapper(WorldObject obj)
    {
        return obj switch
        {
            User user => new HybrasylUser(user),
            Reactor reactor => new HybrasylReactor(reactor),
            _ => new HybrasylWorldObject(obj)
        };
    }

    public bool Reload()
    {
        Compiled = new MoonSharp.Interpreter.Script(CoreModules.Preset_SoftSandbox);
        return Run();
    }

    /// <summary>
    /// Function to dynamically make the right UserData wrapper for a given object.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static dynamic GetUserDataValue(dynamic obj)
    {
        if (obj == null)
            return DynValue.NewNil();

        return obj switch
        {
            User user => UserData.Create(new HybrasylUser(user)),
            Monster monster => UserData.Create(new HybrasylMonster(monster)),
            World world => UserData.Create(new HybrasylWorld(world)),
            Map map => UserData.Create(new HybrasylMap(map)),
            Reactor reactor => UserData.Create(new HybrasylReactor(reactor)),
            WorldObject worldObject => UserData.Create(new HybrasylWorldObject(worldObject)),
            _ => UserData.Create(obj)
        };
    }

    private static LuaException ParseException(string decoratedMessage)
    {
        var matches = LuaRegex.Match(decoratedMessage);
        var ret = new LuaException();
        if (matches.Success && matches.Groups.Count == 6)
        {
            ret.Filename = Path.GetFileName(matches.Groups[1].Value);
            ret.LineNumber = int.Parse(matches.Groups[2].Value);
            ret.Error = matches.Groups[5].Value;
        }
        else
            ret.Error = decoratedMessage;
        return ret;
    }

    private string ExtractLuaSource(int linenumber)
    {
        var lines = RawSource.Split('\n').ToList();
        var lua = $"## {lines[linenumber - 2]}\n## --->{lines[linenumber - 1]}\n";
        if (linenumber < lines.Count)
            lua = $"{lua}## {lines[linenumber]}";
        return lua;
    }

    private string HumanizeException(Exception ex)
    {
        string summary;

        if (ex is InterpreterException ie)
        {
            // Get information from decorated message. CallStack is only available for
            // certain lua exceptions; decorated message / innerexception always has what
            // is needed.
            var e = ParseException(ie.DecoratedMessage);
            if (!string.IsNullOrEmpty(e.Filename))
                summary = $"Line {e.LineNumber}: {e.Error}\n{ExtractLuaSource(e.LineNumber)}";
            else
                summary = $"Could not be parsed, raw message follows {ie.DecoratedMessage}";
            if (ie is SyntaxErrorException)
            {
                return $"\nSyntax error: {summary}";
            }
            if (ie is ScriptRuntimeException)
                return $"\nScripting runtime error: {summary}";
            if (ie is DynamicExpressionException)
                return $"\nLua type error: {summary}";
            else
                return $"\nInternal error from Lua code: STACK: {summary}\nERR: {ie.DecoratedMessage}";
        }
        else
        {
            var retstr = $"\nC# exception, perhaps caused by Lua script code: STACK: {ex.StackTrace}\n ERR:{ex.Message}";
            if (ex.InnerException != null)
                retstr = $"{retstr}\nINNER STACK TRACE: {ex.InnerException.StackTrace}\n INNER ERR: {ex.InnerException.Message}";
            return retstr;
        }
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
        // TODO: streamline / refactor
        Compiled.Globals["Gender"] = UserData.CreateStatic<Xml.Gender>();
        Compiled.Globals["LegendIcon"] = UserData.CreateStatic<LegendIcon>();
        Compiled.Globals["LegendColor"] = UserData.CreateStatic<LegendColor>();
        Compiled.Globals["Class"] = UserData.CreateStatic<Xml.Class>();
        Compiled.Globals["utility"] = typeof(HybrasylUtility);
        Compiled.Globals.Set("world", UserData.Create(Processor.World));
        Compiled.Globals.Set("logger", UserData.Create(new ScriptLogger(Name)));
    }

    /// <summary>
    /// Load the script from disk and execute it.
    /// </summary>
    /// <param name="onLoad">Whether or not to execute OnLoad() if it exists in the script.</param>
    /// <returns>boolean indicating whether the script was reloaded or not</returns>
    public bool Run(bool onLoad = true)
    {
        try
        {
            Compiled.Globals["Gender"] = UserData.CreateStatic<Xml.Gender>();
            Compiled.Globals["LegendIcon"] = UserData.CreateStatic<LegendIcon>();
            Compiled.Globals["LegendColor"] = UserData.CreateStatic<LegendColor>();
            Compiled.Globals["Class"] = UserData.CreateStatic<Xml.Class>();
            Compiled.Globals["utility"] = typeof(HybrasylUtility);
            Compiled.Globals.Set("world", UserData.Create(Processor.World));
            Compiled.Globals.Set("logger", UserData.Create(new ScriptLogger(Name)));
            Compiled.Globals.Set("this_script", DynValue.NewString(Name));
            // Load file into RawSource so we have access to it later
            RawSource = File.ReadAllText(FullPath);
            Compiled.DoFile(FullPath);
            if (onLoad)
            {
                GameLog.ScriptingInfo($"Loading: {Path.GetFileName(FullPath)}");
                ExecuteFunction("OnLoad");
            }
        }
        catch (Exception ex)
        {
            Game.ReportException(ex);
            var error_msg = HumanizeException(ex);
            GameLog.ScriptingError("Run: Error executing script {FileName} (associate {assoc}): {Message}",
                FileName, Associate?.Name ?? "none", error_msg);

            Disabled = true;
            CompilationError = ex.ToString();
            LastRuntimeError = error_msg;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Set a string value to be used by a script.
    /// </summary>
    /// <param name="name">The name of the Lua variable.</param>
    /// <param name="value">The string value of the variable.</param>
    public void SetGlobalValue(string name, string value)
    {
        var v = DynValue.NewString(value);
        Compiled.Globals.Set(name, v);
    }

    /// <summary>
    /// Set a numeric value to be used by a script.
    /// </summary>
    /// <param name="name">The name of the Lua variable.</param>
    /// <param name="value">The numeric value of the variable.</param>
    public void SetGlobalValue(string name, uint value)
    {
        var v = DynValue.NewNumber(value);
        Compiled.Globals.Set(name, v);
    }

    public void SetGlobalValue(string name, bool value)
    {
        var v = DynValue.NewBoolean(value);
        Compiled.Globals.Set(name, v);
    }
        
    /// <summary>
    /// Execute a Lua expression in the context of an associated world object.
    /// Primarily used for dialog callbacks.
    /// </summary>
    /// <param name="expr">The javascript expression, in string form.</param>
    /// <param name="invoker">The invoker (caller).</param>
    /// <param name="source">Optionally, the source of the script call, invocation or dialog</param>
    /// <returns></returns>
    public bool Execute(string expr, dynamic invoker, dynamic source = null)
    {
        if (Disabled)
            return false;

        try
        {
            Compiled.Globals["utility"] = typeof(HybrasylUtility);
            Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
            if (source != null)
                Compiled.Globals.Set("source", GetUserDataValue(source));

            // We pass Compiled.Globals here to make sure that the updated table (with new variables) makes it over
            LastReturnValue = Compiled.DoString(expr, Compiled.Globals);
        }
        catch (Exception ex) 
        {
            Game.ReportException(ex);
            var errorMsg = HumanizeException(ex);
            GameLog.ScriptingError("Execute: Error executing expression {expr} in {FileName} (associate {associate}, invoker {invoker}, source {source}): {Message}",
                expr, FileName, Associate?.Name ?? "none", invoker?.Name ?? "none", source?.Name ?? "none", errorMsg);
            //Disabled = true;
            CompilationError = ex.ToString();
            LastRuntimeError = errorMsg;
            return false;
        }
        return true;
    }

    public DynValue ExecuteAndReturn(string expr, dynamic invoker)
    {
        if (Disabled)
            return DynValue.Nil;

        try
        {
            Compiled.Globals["utility"] = typeof(HybrasylUtility);
            Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
            Compiled.Globals.Set("parent", GetUserDataValue(invoker));
            return Compiled.DoString(expr);
        }
        catch (Exception ex)
        {
            Game.ReportException(ex);
            var errorMsg = HumanizeException(ex);
            GameLog.ScriptingError("Execute: Error executing expression {expr} in {FileName} (associate {associate}, invoker {invoker}): {Message}",
                expr, FileName, Associate?.Name ?? "none", invoker?.Name ?? "none", errorMsg);
            //Disabled = true;
            CompilationError = ex.ToString();
            return DynValue.Nil;
        }
    }

    public bool ExecuteFunction(string functionName, dynamic invoker, dynamic source, dynamic scriptItem=null, bool returnFromScript=false)
    {
        if (Disabled)
            return false;
        try
        {
            if (HasFunction(functionName))
            {
                Compiled.Globals["utility"] = typeof(HybrasylUtility);
                var f = GetUserDataValue(invoker as Monster);
                Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
                Compiled.Globals.Set("source", GetUserDataValue(source));
                var q = GetUserDataValue(invoker);
                var z = GetUserDataValue(source);
                if (scriptItem != null)
                    Compiled.Globals.Set("item", GetUserDataValue(scriptItem));

                if (returnFromScript) return Compiled.Call(Compiled.Globals[functionName]).Boolean;
                else Compiled.Call(Compiled.Globals[functionName]);
            }
            else
            {
                //GameLog.ScriptingWarning("ExecuteFunction: function {fn} in {FileName} did not exist",
                //    functionName, FileName);
                return false;
            }
        }
        catch (Exception ex) 
        {
            Game.ReportException(ex);
            var errorMsg = HumanizeException(ex);
            GameLog.ScriptingError("ExecuteFunction: Error executing function {fn} in {FileName} (associate {associate}, invoker {invoker}, item {item}): {Message}",
                functionName, FileName, Associate?.Name ?? "none", invoker?.Name ?? "none", scriptItem?.Name ?? "none", errorMsg);
            //Disabled = true;
            CompilationError = errorMsg;
            return false;
        }
        return true;
    }

    public bool ExecuteFunction(string functionName, dynamic invoker)
    {
        if (Disabled)
            return false;

        try
        {
            if (HasFunction(functionName))
            {
                Compiled.Globals["utility"] = typeof(HybrasylUtility);
                Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
                Compiled.Call(Compiled.Globals[functionName]);
            }
            else
            {
                //GameLog.ScriptingWarning("ExecuteFunction: function {fn} in {FileName} did not exist",
                //    functionName, FileName);
                return false;
            }
        }
        catch (Exception ex)
        {
            Game.ReportException(ex);
            var errorMsg = HumanizeException(ex);
            GameLog.ScriptingError("ExecuteFunction: Error executing function {fn} in {FileName} (associate {associate}, invoker {invoker}): {Message}",
                functionName, FileName, Associate?.Name ?? "none", invoker?.Name ?? "none", errorMsg);
            CompilationError = errorMsg;
            return false;
        }

        return true;

    }

    public bool ExecuteFunction(string functionName)
    {
        if (Disabled)
            return false;

        try
        {
            if (HasFunction(functionName))
            {
                Compiled.Globals["utility"] = typeof(HybrasylUtility);
                // Provide extra information when running spawn/load to aid in debugging
                if (functionName is "OnSpawn" or "OnLoad")
                {
                    string assoc = null;
                    if (Associate != null)
                    {
                        assoc = $"{Associate.Type}";
                        if (!string.IsNullOrEmpty(Associate.Name))
                            assoc = $"{assoc} {Associate.Name}";
                        assoc = $"{assoc}: {Associate.LocationDescription}";
                    }
                    GameLog.ScriptingInfo($"{FileName}: (associate is {assoc ?? "none"}), executing {functionName}");
                }
                Compiled.Call(Compiled.Globals[functionName]);
            }
            else
            {
                //GameLog.ScriptingWarning("ExecuteFunction: function {fn} in {FileName} did not exist",
                //    functionName, FileName);
                return false;
            }
        }
        catch (Exception ex)
        {
            Game.ReportException(ex);
            var errorMsg = HumanizeException(ex);
            GameLog.ScriptingError("ExecuteFunction: Error executing function {fn} in {FileName} (associate {associate}): {Message}",
                functionName, FileName, Associate?.Name ?? "none", errorMsg);
            CompilationError = errorMsg;
            return false;
        }

        return true;

    }
}