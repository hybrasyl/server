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

using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace HybrasylTests;

public class Settings
{
    private static Settings _settings;
    public JsonSettings JsonSettings;

    private Settings()
    {
        var json = File.ReadAllText("hybrasyltest-settings.json");
        JsonSettings = JsonConvert.DeserializeObject<JsonSettings>(json);
    }

    private static object _lock { get; } = new();

    public static Settings HybrasylTests
    {
        get
        {
            lock (_lock)
            {
                _settings ??= new Settings();
                return _settings;
            }
        }
    }
}

public class JsonSettings
{
    public string WorldDataDirectory { get; init; }
    public string LogDirectory { get; init;  }
    public string DataDirectory { get; init; }
}