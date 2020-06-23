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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;
using Serilog;

namespace Hybrasyl.Scripting
{
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

        public string RawSource { get; set; }
        public string Name { get; set; }
        public string FullPath { get; private set; }
        public string FileName => Path.GetFileName(FullPath);

        public ScriptProcessor Processor { get; set; }
        public MoonSharp.Interpreter.Script Compiled { get; private set; }
        public HybrasylWorldObject Associate { get; private set; }

        public bool Disabled { get; set; }
        public string CompilationError { get; private set; }
        public string LastRuntimeError { get; private set; }

        private HashSet<String> _FunctionIndex { get; set; }

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
            Disabled = false;
            CompilationError = string.Empty;
            LastRuntimeError = string.Empty;
            _FunctionIndex = new HashSet<String>();
        }

        public void AssociateScriptWithObject(WorldObject obj)
        {
            Associate = new HybrasylWorldObject(obj);
            if (obj is VisibleObject)
            { 
                var visibleObject = obj as VisibleObject;
                Compiled.Globals.Set("map", UserData.Create(new HybrasylMap(visibleObject.Map)));
            }
            Compiled.Globals.Set("associate", UserData.Create(Associate));
            obj.Script = this;
        }

        public dynamic GetObjectWrapper(WorldObject obj)
        {
            if (obj is User)
                return new HybrasylUser(obj as User);
            return new HybrasylWorldObject(obj);
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
        public dynamic GetUserDataValue(dynamic obj)
        {
            if (obj == null)
                return DynValue.NewNil();
            if (obj is User)
                return UserData.Create(new HybrasylUser(obj as User));
            else if (obj is World)
                return UserData.Create(new HybrasylWorld(obj as World));
            else if (obj is Map)
                return UserData.Create(new HybrasylMap(obj as Map));
            else if (obj is WorldObject)
                return UserData.Create(new HybrasylWorldObject(obj as WorldObject));
            return UserData.Create(obj); 
        }

        /// <summary>
        /// Check to see if a script implements a given function.
        /// </summary>
        /// <param name="name">The function name to check</param>
        /// <returns></returns>
        public bool HasFunction(string name)
        {
            return Compiled.Globals.Get(name) != DynValue.Nil;
        }

        /// <summary>
        /// Load the script from disk and execute it.
        /// </summary>
        /// <param name="onLoad">Whether or not to execute OnLoad() if it exists in the script.</param>
        /// <returns>boolean indicating whether the script was reloaded or not</returns>
        public bool Run(bool onLoad=true)
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
                Compiled.DoFile(FullPath);
                if (onLoad)
                    ExecuteFunction("OnLoad");               
            }
            catch (Exception ex) when (ex is InterpreterException)
            {
                GameLog.ScriptingError("{Function}: Error executing script {FileName}: {Message}",
                                MethodBase.GetCurrentMethod().Name, FileName, 
                                (ex as InterpreterException).DecoratedMessage);
                Disabled = true;
                CompilationError = ex.ToString();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Set a value to be used by a script. Note that we only support interop with strings here.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetGlobalValue(string name, string value)
        {
            var v = DynValue.NewString(value);
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
                Compiled.DoString(expr, Compiled.Globals);
            }
            catch (Exception ex) when (ex is InterpreterException)
            {
                GameLog.ScriptingError("{Function}: Error executing expression {expr} in {FileName} (invoker {Invoker}): {Message}",
                    MethodBase.GetCurrentMethod().Name, expr, FileName, (invoker as WorldObject).Name,
                    (ex as InterpreterException).DecoratedMessage);
                //Disabled = true;
                CompilationError = ex.ToString();
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
                return Compiled.DoString(expr);
            }
            catch (Exception ex) when (ex is InterpreterException)
            {
                GameLog.ScriptingError("{Function}: Error executing expression: {expr} in {FileName} (invoker {Invoker}): {Message}",
                    MethodBase.GetCurrentMethod().Name, expr, FileName, (invoker as WorldObject).Name,
                    (ex as InterpreterException).DecoratedMessage);
                 //Disabled = true;
                CompilationError = ex.ToString();
                return DynValue.Nil;
            }
        }

        public bool ExecuteFunction(string functionName, dynamic invoker, dynamic source, dynamic scriptItem=null)
        {
            if (Disabled)
                return false;

            try
            {
                if (HasFunction(functionName))
                {
                    Compiled.Globals["utility"] = typeof(HybrasylUtility);
                    Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
                    Compiled.Globals.Set("source", GetUserDataValue(source));
                    if (scriptItem != null)
                        Compiled.Globals.Set("item", GetUserDataValue(scriptItem));
                    Compiled.Call(Compiled.Globals[functionName]);
                }
                else
                    return false;
            }
            catch (Exception ex) when (ex is InterpreterException)
            {
                GameLog.ScriptingError("{Function}: Error executing function {ScriptFunction} in {FileName} (invoker {Invoker}): {Message}",
                    MethodBase.GetCurrentMethod().Name, functionName, FileName, (invoker as WorldObject).Name,
                    (ex as InterpreterException).DecoratedMessage);
                //Disabled = true;
                CompilationError = ex.ToString();
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
                    GameLog.ScriptingError("{Function}: function {ScriptFunction} in {FileName} did not exist?", MethodBase.GetCurrentMethod().Name,
                        functionName, FileName);
                    return false;
                }
            }
            catch (Exception ex) when (ex is InterpreterException)
            {
                GameLog.ScriptingError("{Function}: Error executing script function {ScriptFunction} in {FileName} (invoker {Invoker}): {Message}",
                    MethodBase.GetCurrentMethod().Name, functionName, (invoker as WorldObject).Name, FileName,
                    (ex as InterpreterException).DecoratedMessage);
                CompilationError = ex.ToString();
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
                    Compiled.Call(Compiled.Globals[functionName]);
                }
                else
                    return false;
            }
            catch (Exception ex) when (ex is InterpreterException)
            {
                GameLog.ScriptingError("{Function}: Error executing script function {ScriptFunction} in {FileName}: {Message}",
                    MethodBase.GetCurrentMethod().Name, functionName, FileName,
                    (ex as InterpreterException).DecoratedMessage);
                CompilationError = ex.ToString();
                return false;
            }

            return true;

        }

        /// <summary>
        /// Attach a Scriptable to an in game NPC.
        /// </summary>
        /// <returns></returns>
        public bool AttachScriptable(WorldObject obj)
        {
            Associate = new HybrasylWorldObject(obj);
            return true;
        }
    }
}