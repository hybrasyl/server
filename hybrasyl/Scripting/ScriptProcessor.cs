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
using System.Reflection;
using System.Text.RegularExpressions;
using Hybrasyl.Enums;
using Serilog;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    public class ScriptProcessor
    {

        private Dictionary<string, List<Script>> _scripts { get; set; }
        public HybrasylWorld World { get; private set; }

        public ScriptProcessor(World world)
        {
            World = new HybrasylWorld(world);
            // Register UserData types for MoonScript
            UserData.RegisterAssembly(typeof(Game).Assembly);
            UserData.RegisterType<Sex>();
            UserData.RegisterType<LegendIcon>();
            UserData.RegisterType<LegendColor>();
            UserData.RegisterType<LegendMark>();
            UserData.RegisterType<DateTime>();
            UserData.RegisterType<TimeSpan>();
            _scripts = new Dictionary<string, List<Script>>();            
        }

        // "Ri OnA.lua" => riona
        private string SanitizeName(string scriptName) => Regex.Replace(scriptName.ToLower().Normalize(), ".lua$", "");

        private bool TryGetScriptInstances(string scriptName, out List<Script> scriptList)
        {
            scriptList = null;
            if (_scripts.TryGetValue(SanitizeName(scriptName), out scriptList))
            {
                return true;
            }
            return false;
        }
        public bool TryGetScript(string scriptName, out Script script)
        {
            script = null;
            if (TryGetScriptInstances(scriptName, out List<Script> s))
            {
                script = s[0].Clone();
                return true;
            }
            return false;
        }

        public void RegisterScript(Script script)
        {
            script.Run();
            var name = SanitizeName(script.Name);
            if (!_scripts.ContainsKey(name))
            {
                _scripts[name] = new List<Script>();
            }
            _scripts[name].Add(script);
        }


        public bool DeregisterScript(string scriptName)
        {
            if (TryGetScriptInstances(scriptName, out List<Script> s))
            {
                foreach (var instance in s)
                {
                    instance.Reload();
                    GameLog.ScriptingInfo($"Reloading instance of {scriptName}: associate was {instance.Associate?.Name ?? "None"}");
                }
                return true;
            }
            return false;
        }
    }
}
