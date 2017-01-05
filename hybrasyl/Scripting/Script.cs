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
 
using IronPython.Runtime.Operations;
using System;
using System.IO;
using Hybrasyl.Objects;
using log4net;
using Microsoft.Scripting.Hosting;

namespace Hybrasyl.Scripting
{
    public class Script
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public ScriptSource Source { get; set; }
        public string Name { get; set; }
        public string Path { get; private set; }

        public ScriptProcessor Processor { get; set; }
        public CompiledCode Compiled { get; private set; }
        public ScriptScope Scope { get; set; }
        public dynamic Instance { get; set; }
        public HybrasylWorldObject Associate { get; private set; }

        public bool Disabled { get; set; }
        public string CompilationError { get; private set; }
        public string LastRuntimeError { get; private set; }

        public Script Clone()
        {
            var clone = new Script(Path, Processor);
            // Reload and reinstantiate the script with a new ScriptScope
            Scope = Processor.Engine.CreateScope();
            clone.Load();
            clone.InstantiateScriptable();
            return clone;
        }

        public Script(string path, ScriptProcessor processor)
        {
            Path = path;
            Compiled = null;
            Source = null;
            Processor = processor;
            Disabled = false;
            CompilationError = string.Empty;
            LastRuntimeError = string.Empty;
        }

        public void AssociateScriptWithObject(WorldObject obj)
        {
            Associate = new HybrasylWorldObject(obj);
            obj.Script = this;
        }

        public dynamic GetObjectWrapper(WorldObject obj)
        {
            if (obj is User)
                return new HybrasylUser(obj as User);
            return new HybrasylWorldObject(obj);
        }
        /// <summary>
        /// Load the script from disk, recompile it into bytecode, and execute it.
        /// </summary>
        /// <returns>boolean indicating whether the script was reloaded or not</returns>
        public bool Load()
        {
            string scriptText;
            try
            {
                scriptText = File.ReadAllText(Path);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Couldn't open script {0}: {1}", Path, e.ToString());
                Disabled = true;
                CompilationError = e.ToString();
                return false;
            }

            scriptText = //ScriptProcessor.RestrictStdlib 
                ScriptProcessor.HybrasylImports + scriptText;

            Source =
                Processor.Engine.CreateScriptSourceFromString(scriptText);

            Name = System.IO.Path.GetFileName(Path).ToLower();

            try
            {
                Compile();
                Compiled.Execute(Scope);
                Disabled = false;
            }
            catch (Exception e)
            {
                var pythonFrames = PythonOps.GetDynamicStackFrames(e);
                var exceptionstring = Processor.Engine.GetService<ExceptionOperations>().FormatException(e);
                Logger.ErrorFormat("script {0} encountered error, Python stack follows", Path);
                Logger.ErrorFormat("{0}", exceptionstring);
                Disabled = true;
                CompilationError = exceptionstring;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Compile the script, using the global Hybrasyl engine.
        /// </summary>
        /// <returns>boolean indicating success or failure (might raise exception in the future)</returns>
        public bool Compile()
        {
            if (Source == null) return false;
            Compiled = Source.Compile();
            return true;
        }

        /// <summary>
        /// If the script has a Scriptable class (used for WorldObject hooks), instantiate it.
        /// </summary>
        public bool InstantiateScriptable()
        {
            // First, disable the script, then if we have an instance, delete it.
            Disabled = true;

            if (Instance != null)
                Instance = null;

            try
            {
                var klass = Scope.GetVariable("Scriptable");
                Scope.SetVariable("world", Processor.World);
                if (Associate != null)
                {
                    Scope.SetVariable("npc", Associate);
                    Associate.Obj.ResetPursuits();
                }
                Instance = Processor.Engine.Operations.CreateInstance(klass);
                Disabled = false;
            }
            catch (Exception e)
            {
                var pythonFrames = PythonOps.GetDynamicStackFrames(e);
                var exceptionstring = Processor.Engine.GetService<ExceptionOperations>().FormatException(e);
                Logger.ErrorFormat("script {0} encountered error, Python stack follows", Path);
                Logger.ErrorFormat("{0}", exceptionstring);
                Logger.ErrorFormat("script {0} now disabled", Path);
                Disabled = true;
                CompilationError = exceptionstring;
                return false;
            }
            return true;
        }

        public bool ExecuteFunction(ScriptInvocation invocation, params object[] parameters)
        {
            if (Disabled)
                return false;

            if (!Processor.Engine.Operations.IsCallable(invocation.Function)) return false;
            if (invocation.Invoker is User)
            {
                Scope.SetVariable("invoker", new HybrasylUser(invocation.Invoker as User));
            }
            else
            {
                Scope.SetVariable("invoker", new HybrasylWorldObject(invocation.Invoker as WorldObject));
            }
            if (invocation.Associate is WorldObject)
            {
                Scope.SetVariable("npc", new HybrasylWorldObject(invocation.Associate as WorldObject));
            }
            try
            {
                var ret = Processor.Engine.Operations.Invoke(invocation.Function, parameters);
                if (ret is bool)
                    return (bool)ret;
            }
            catch (Exception e)
            {
                var pythonFrames = PythonOps.GetDynamicStackFrames(e);
                var exceptionstring = Processor.Engine.GetService<ExceptionOperations>().FormatException(e);
                Logger.ErrorFormat("script {0} encountered error, Python stack follows", Path);
                Logger.ErrorFormat("{0}", exceptionstring);
                Logger.ErrorFormat("script {0} now disabled", Path);
                LastRuntimeError = exceptionstring;
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
            Logger.InfoFormat("Scriptable name: {0}", Instance.name);
            return true;
        }

        /// <summary>
        /// If the script has a Scriptable class and the given function exists, execute it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters">The parameters to pass to the function.</param>
        public void ExecuteScriptableFunction(string name, params object[] parameters)
        {
            if (Disabled)
                return;

            Scope.SetVariable("world", Processor.World);
            Scope.SetVariable("npc", Associate);

            try
            {
                Processor.Engine.Operations.InvokeMember(Instance, name, parameters);
            }
            catch (System.NotImplementedException)
            {
                Logger.DebugFormat("script {0}: missing member {1}", Path, name);
            }
            catch (Exception e)
            {
                var pythonFrames = PythonOps.GetDynamicStackFrames(e);
                var exceptionstring = Processor.Engine.GetService<ExceptionOperations>().FormatException(e);
                Logger.ErrorFormat("script {0} encountered error, Python stack follows", Path);
                Logger.ErrorFormat("{0}", exceptionstring);
                Logger.ErrorFormat("script {0} now disabled");
                LastRuntimeError = exceptionstring;
            }
        }

        /// <summary>
        /// Execute the script in the passed scope.
        /// </summary>
        /// <param name="scope">The ScriptScope the script will execute in.</param>
        public void ExecuteScript(WorldObject caller = null)
        {
            dynamic resolvedCaller;

            if (caller != null)
            {
                if (caller is User)
                    resolvedCaller = new HybrasylUser(caller as User);
                else
                    resolvedCaller = new HybrasylWorldObject(caller);
                Scope.SetVariable("npc", resolvedCaller);
            }

            Scope.SetVariable("world", Processor.World);
            Compiled.Execute(Scope);
        }

    }
}
