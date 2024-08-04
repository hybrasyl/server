// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;

namespace Hybrasyl.Subsystems.Scripting;

public class ScriptProcessor(World world)
{
    public World World { get; } = world;
    private Dictionary<Guid, Script> _scripts = new();
    private Dictionary<string, Guid> _locatorIndex = new();
    private Dictionary<string, Guid> _nameIndex = new();
    private Dictionary<Guid, List<Guid>> _scriptAttachments = new();

    static ScriptProcessor()
    {
        // Register UserData types for MoonScript. This only needs to be done once.
        // NB registering assemblies is required for RegisterType of
        // any type in that assembly to work correctly
        UserData.RegisterAssembly(typeof(Game).Assembly);
        UserData.RegisterType<Gender>();
        UserData.RegisterType<LegendIcon>();
        UserData.RegisterType<LegendColor>();
        UserData.RegisterType<LegendMark>();
        UserData.RegisterType<DateTime>();
        UserData.RegisterType<TimeSpan>();
        UserData.RegisterType<ElementType>();
        UserData.RegisterType<Direction>();
        // Ensure usage of Guid in various scripts is handled correctly
        MoonSharp.Interpreter.Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Guid>(v =>
            DynValue.NewString(v.ToString()));
    }

    public void CompileScripts()
    {
        // Scan each directory for *.lua files
        var numFiles = 0;
        var numErrors = 0;
        foreach (var file in Directory.GetFiles(World.ScriptDirectory, "*.lua", SearchOption.AllDirectories))
        {
            var path = file.Replace(World.ScriptDirectory, "");
            var scriptName = Path.GetFileName(file);
            if (path.StartsWith("_") || path.StartsWith("modules"))
                continue;

            try
            {
                var script = new Script(file, this);
                RegisterScript(script);
                if (path.StartsWith("common") || path.StartsWith("startup"))
                {
                    GameLog.ScriptingInfo($"{nameof(CompileScripts)}: Loading & executing script {path}");
                    script.Run();
                }
                else
                    GameLog.ScriptingInfo($"{nameof(CompileScripts)}: Loading {path}");
                numFiles++;
            }
            catch (Exception e)
            {
                GameLog.ScriptingError($"{nameof(CompileScripts)}: {scriptName}: Registration failed: {e}");
                numErrors++;
            }
        }

        GameLog.Info($"{nameof(CompileScripts)}: loaded {numFiles} scripts");
        if (numErrors > 0)
            GameLog.Error($"{nameof(CompileScripts)}: {numErrors} scripts had errors - check scripting log");
    }

    private string GenerateLocator(string path)
    {
        var locator = path.Replace(World.ScriptDirectory, "").Replace(@"\", ":").Replace("/", ":");
        return locator.StartsWith(":") ? locator[1..] : locator;
    }

    public void RegisterScriptAttachment(Script script, WorldObject obj)
    {
        if (script == null || obj == null)
            throw new ArgumentException("both arguments required");

        if (!_scriptAttachments.TryGetValue(script.Guid, out var _))
            _scriptAttachments.Add(script.Guid, new());

        _scriptAttachments[script.Guid].Add(obj.Guid);
    }

    public bool TryGetScript(string scriptName, out Script script)
    {
        script = null;
        if (_nameIndex.TryGetValue(scriptName, out var guid) || _nameIndex.TryGetValue($"{scriptName.ToLower()}.lua", out guid))
            script = _scripts[guid];

        return script != null;
    }

    public bool TryGetScriptByLocator(string relativePath, out Script script)
    {
        script = null;
        var locator = GenerateLocator(relativePath);
        if (!_locatorIndex.TryGetValue(locator, out var guid))
            return false;
        script = _scripts[guid];
        return true;
    }

    public void RegisterScript(Script script, bool run = true)
    {
        script.Processor ??= this;
        script.Guid = Guid.NewGuid();
        script.Locator = GenerateLocator(script.FullPath);
        if (run)
            script.Run();

        _scripts[script.Guid] = script;
        _nameIndex[script.Name] = script.Guid;
        _locatorIndex[script.Locator] = script.Guid;
    }

    public void ReloadScript(Guid guid)
    {
        if (!_scripts.TryGetValue(guid, out var script))
        {
            GameLog.ScriptingError($"{nameof(ReloadScript)}: reload failed, guid {guid} not found");
            return;
        }

        script.Reload();

        var attachments = new List<Guid>(_scriptAttachments[guid]);
        _scriptAttachments[guid].Clear();

        foreach (var attachment in attachments)
        {
            if (!World.WorldState.TryGetWorldObject(attachment, out WorldObject obj) ||
                obj is not ISpawnable spawnable) continue;
            spawnable.OnSpawn();
            GameLog.ScriptingInfo($"{nameof(ReloadScript)}: type {spawnable.GetType()} ({spawnable.Name}): triggering OnSpawn due to script reload");
        }
    }

}