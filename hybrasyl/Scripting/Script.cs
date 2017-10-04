﻿/*
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
using System.IO;
using Hybrasyl.Objects;
using log4net;
using NLua;

namespace Hybrasyl.Scripting
{

    public class ScriptLogger
    {
        private static readonly ILog ScriptingLogger = LogManager.GetLogger("ScriptingLog");
        
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
        private static readonly ILog ScriptingLogger = LogManager.GetLogger("ScriptingLog");

        public string RawSource { get; set; }
        public string Name { get; set; }
        public string FullPath { get; private set; }

        public Lua State { get; private set; }
        public ScriptProcessor Processor { get; set; }
        public LuaFunction Compiled { get; private set; }
        public HybrasylWorldObject Associate { get; private set; }

        public bool Disabled { get; set; }
        public string CompilationError { get; private set; }
        public string LastRuntimeError { get; private set; }

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
            Compiled = null;
            RawSource = string.Empty;
            Processor = processor;
            Disabled = false;
            CompilationError = string.Empty;
            LastRuntimeError = string.Empty;
            State = new Lua();
            PrepareState();
        }

        public void AssociateScriptWithObject(WorldObject obj)
        {
            Associate = new HybrasylWorldObject(obj);
            State["associate"] = Associate;
            obj.Script = this;
        }

        public dynamic GetObjectWrapper(WorldObject obj)
        {
            if (obj is User)
                return new HybrasylUser(obj as User);
            return new HybrasylWorldObject(obj);
        }

        /// <summary>
        /// Prepare a script's Lua state for executing Hybrasyl scripting code, adding needed host object and type references.
        /// </summary>
        public void PrepareState()
        {
            State.LoadCLRPackage();
            State.DoString(@"Scripting = CLRPackage('Hybrasyl', 'Hybrasyl.Scripting')");
            State.DoString(@"Enums = CLRPackage('Hybrasyl', 'Hybrasyl.Enums')");
            State["world"] = Processor.World;
            State["logger"] = new ScriptLogger(Name);
            // Prohibit future imports from other .NET assemblies
            State.DoString(@"import = function() end");
            State.DoString(@"CLRPackage = function() end");
        }

        /// <summary>
        /// Check to see if a script implements a given function.
        /// </summary>
        /// <param name="name">The function name to check</param>
        /// <returns></returns>
        public bool HasFunction(string name)
        {
            var luaFunction = State[name] as LuaFunction;
            return luaFunction != null;
        }

        /// <summary>
        /// Load the script from disk and execute it.
        /// </summary>
        /// <returns>boolean indicating whether the script was reloaded or not</returns>
        public bool Run()
        {
            try
            {
                State.DoFile(FullPath);
            }
            catch (NLua.Exceptions.LuaScriptException e)
            {
                ScriptingLogger.Error($"Error executing script {FullPath}: {e.ToString()}, full stacktrace follows:\n{e.StackTrace}");
                Disabled = true;
                CompilationError = e.ToString();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Execute a JavaScript expression in the context of an associated world object.
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
                State["invoker"] = GetObjectWrapper(invoker);
                State.DoString(expr);
            }
            catch (NLua.Exceptions.LuaScriptException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing expression: {expr}: \n{e.ToString()} full stacktrace follows:\n{e.StackTrace}");
                Disabled = true;
                CompilationError = e.ToString();
                return false;
            }
            return true;

        }

        public bool ExecuteFunction(string functionName, dynamic associate, dynamic invoker)
        {
            if (Disabled)
                return false;

            try
            {
                var luaFunction = State[functionName] as LuaFunction;
                if (luaFunction != null)
                {
                    State["associate"] = GetObjectWrapper(associate);
                    State["invoker"] = GetObjectWrapper(invoker);
                    luaFunction.Call();
                }
                else
                    return false;
            }
            catch (NLua.Exceptions.LuaScriptException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing expression: {functionName} ({e.ToString()}) full stacktrace follows:\n{e.StackTrace}");
                Disabled = true;
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
                var luaFunction = State[functionName] as LuaFunction;
                if (luaFunction != null)
                {
                    State["invoker"] = GetObjectWrapper(invoker);
                    luaFunction.Call();
                }
                else
                    return false;
            }
            catch (NLua.Exceptions.LuaScriptException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing function: {functionName} ({e.ToString()}) , full stacktrace follows:\n{e.StackTrace}");
                Disabled = true;
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
                var luaFunction = State[functionName] as LuaFunction;
                if (luaFunction != null)
                {
                    luaFunction.Call();
                }
                else
                    return false;
            }
            catch (NLua.Exceptions.LuaScriptException e)
            {
                ScriptingLogger.Error($"{Name}: Error executing function: {functionName} ({e.ToString()}) , full stacktrace follows:\n{e.StackTrace}");
                Disabled = true;
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