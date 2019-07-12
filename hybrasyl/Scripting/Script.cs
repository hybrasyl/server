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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
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
using log4net;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]
    public class ScriptLogger
    {
        private static readonly ILog ScriptingLogger = LogManager.GetLogger(Assembly.GetEntryAssembly(),"ScriptingLog");
        
        public string ScriptName { get; set; }

        public ScriptLogger(string name)
        {
            ScriptName = name;
        }

        public void Info(string message) => ScriptingLogger.Info($"{ScriptName} : {message}");

        public void Error(string message) => ScriptingLogger.Error($"{ScriptName} : {message}");
    }

    public class Script
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog ScriptingLogger = LogManager.GetLogger(Assembly.GetEntryAssembly(),"ScriptingLog");

        public string RawSource { get; set; }
        public string Name { get; set; }
        public string FullPath { get; private set; }

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
            clone.Run();
            return clone;
        }

        public Script(string path, ScriptProcessor processor)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
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
        /// <returns>boolean indicating whether the script was reloaded or not</returns>
        public bool Run()
        {
            try
            {
                Compiled.Globals["Sex"] = UserData.CreateStatic<Sex>();
                Compiled.Globals["LegendIcon"] = UserData.CreateStatic<LegendIcon>();
                Compiled.Globals["LegendColor"] = UserData.CreateStatic<LegendColor>();
                Compiled.DoFile(FullPath);
                Compiled.Globals.Set("world", UserData.Create(Processor.World));
                Compiled.Globals.Set("logger", UserData.Create(new ScriptLogger(Name)));
            }
            catch (ScriptRuntimeException e)
            {
                ScriptingLogger.Error($"Error executing script {FullPath}: {e.DecoratedMessage}, full stacktrace follows:\n{e.StackTrace}");
                Disabled = true;
                CompilationError = e.ToString();
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
        /// <returns></returns>
        public bool Execute(string expr, dynamic invoker)
        {
            if (Disabled)
                return false;

            try
            {
                Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
                // We pass Compiled.Globals here to make sure that the updated table (with new variables) makes it over
                Compiled.DoString(expr, Compiled.Globals);
            }
            catch (ScriptRuntimeException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing expression: {expr}: \n{e.DecoratedMessage} full stacktrace follows:\n{e.StackTrace}");
                //Disabled = true;
                CompilationError = e.ToString();
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
                Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
                return Compiled.DoString(expr);
            }
            catch (ScriptRuntimeException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing expression: {expr}: \n{e.DecoratedMessage} full stacktrace follows:\n{e.StackTrace}");
                //Disabled = true;
                CompilationError = e.ToString();
                return DynValue.Nil;
            }
        }

        public bool ExecuteFunction(string functionName, dynamic invoker, dynamic target)
        {
            if (Disabled)
                return false;

            try
            {
                if (HasFunction(functionName))
                {
                    Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
                    Compiled.Globals.Set("target", GetUserDataValue(target));
                    Compiled.Call(Compiled.Globals[functionName]);
                }
                else
                    return false;
            }
            catch (ScriptRuntimeException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing expression: {functionName} ({e.DecoratedMessage}) full stacktrace follows:\n{e.StackTrace}");
                //Disabled = true;
                CompilationError = e.ToString();
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
                    Compiled.Globals.Set("invoker", GetUserDataValue(invoker));
                    Compiled.Call(Compiled.Globals[functionName]);
                }
                else
                    return false;
            }
            catch (ScriptRuntimeException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing function: {functionName} ({e.DecoratedMessage}) , full stacktrace follows:\n\n{e.StackTrace}");
                //Disabled = true;
                CompilationError = e.ToString();
                return false;
            }

            return true;

        }

        public bool ExecuteFunction(string functionName)
        {
            if (Disabled || !(Associate is HybrasylWorldObject))
                return false;

            try
            {
                if (HasFunction(functionName))
                    Compiled.Call(Compiled.Globals[functionName]);
                else
                    return false;
            }
            catch (ScriptRuntimeException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing function: {functionName} ({e.DecoratedMessage}) , full stacktrace follows:\n{e.StackTrace}");
                //Disabled = true;
                CompilationError = e.ToString();
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