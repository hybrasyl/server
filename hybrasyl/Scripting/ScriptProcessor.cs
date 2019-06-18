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
using Hybrasyl.Enums;
using log4net;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    public class ScriptProcessor
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog ScriptingLogger = LogManager.GetLogger(Assembly.GetEntryAssembly(),"ScriptingLog");

        public Dictionary<string, Script> Scripts { get; private set; }
        public HybrasylWorld World { get; private set; }

        public ScriptProcessor(World world)
        {
            Scripts = new Dictionary<string, Script>();
            World = new HybrasylWorld(world);
            // Register UserData types for MoonScript
            UserData.RegisterAssembly(typeof(Game).Assembly);
            UserData.RegisterType<Sex>();
            UserData.RegisterType<LegendIcon>();
            UserData.RegisterType<LegendColor>();
            UserData.RegisterType<LegendMark>();
            UserData.RegisterType<DateTime>();
            UserData.RegisterType<TimeSpan>();
            
        }

        public bool TryGetScript(string scriptName, out Script script)
        {
            // Try to find "name.lua" or "name"
            if (Scripts.TryGetValue($"{scriptName.ToLower()}.lua", out script) ||
                Scripts.TryGetValue(scriptName.ToLower(), out script))
            {
                return true;
            }
            return false;
        }

        public bool RegisterScript(Script script)
        {
            script.Run();
            Scripts[script.Name] = script;
            return true;
        }

        public bool DeregisterScript(string scriptname)
        {
            Scripts[scriptname] = null;
            return true;
        }
    }
}
