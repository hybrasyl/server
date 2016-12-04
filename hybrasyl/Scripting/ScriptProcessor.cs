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
 
 using System.Collections.Generic;
using IronPython.Hosting;
using log4net;
using Microsoft.Scripting.Hosting;

namespace Hybrasyl.Scripting
{
    public class ScriptProcessor
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ScriptEngine Engine { get; private set; }
        public Dictionary<string, Script> Scripts { get; private set; }
        public HybrasylWorld World { get; private set; }

        // We make an attempt to limit Hybrasyl scripts to stdlib,
        // excluding "dangerous" functions (imports are disallowed outright,
        // along with file i/o or eval/exec/
        public static readonly string RestrictStdlib =
        @"__builtins__.__import__ = None
__builtins__.reload = None
__builtins__.open = None 
__builtins__.eval = None
__builtins__.compile = None
__builtins__.execfile = None
__builtins__.file = None
__builtins__.memoryview = None
__builtins__.raw_input = None

";

        public static readonly string HybrasylImports =
            @"import clr
clr.AddReference('Hybrasyl')
from Hybrasyl.Enums import *
from System import DateTime

";

        public ScriptProcessor(World world)
        {
            Engine = Python.CreateEngine();
            var paths = Engine.GetSearchPaths();
            // FIXME: obvious
            paths.Add(@"C:\Program Files (x86)\IronPython 2.7\Lib");
            paths.Add(@"C:\Python27\Lib");
            Engine.SetSearchPaths(paths);
            Engine.ImportModule("random");

            Scripts = new Dictionary<string, Script>();
            World = new HybrasylWorld(world);
        }

        public bool TryGetScript(string scriptName, out Script script)
        {
            // Try to find "name.py" or "name"
            if (Scripts.TryGetValue($"{scriptName.ToLower()}.py", out script))
            {
                return true;
            }
            return Scripts.TryGetValue(scriptName.ToLower(), out script);
        }

        public Script GetScript(string scriptName)
        {
            Script script;
            // Try to find "name.py" or "name"
            var exists = Scripts.TryGetValue($"{scriptName.ToLower()}.py", out script);
            if (!exists)
            {
                if (Scripts.TryGetValue($"{scriptName.ToLower()}", out script))
                    return script;
            }
            else
                return script;
            return null;
        }

        public bool RegisterScript(Script script)
        {
            script.Scope = Engine.CreateScope();
            script.Load();
            Scripts[script.Name] = script;

            if (script.Disabled)
            {
                Logger.ErrorFormat("{0}: error loading script", script.Name);
                return false;
            }
            else
            {
                Logger.InfoFormat("{0}: loaded successfully", script.Name);
                return true;
            }
        }

        public bool DeregisterScript(string scriptname)
        {
            Scripts[scriptname] = null;
            return true;
        }
    }
}
