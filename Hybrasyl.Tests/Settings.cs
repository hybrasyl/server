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
using System.Collections.Generic;
using System.IO;

namespace Hybrasyl.Tests;

public class Settings
{
    private static Settings _settings;
    public PlatformSettings PlatformSettings;

    private Settings()
    {
        var json = File.ReadAllText("hybrasyltest-settings.json");
        PlatformSettings = JsonConvert.DeserializeObject<PlatformSettings>(json);
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

public class PlatformSettings
{
    public Dictionary<string, DirectorySettings> Directories { get; init; }
}

public class DirectorySettings
{
    public string WorldDataDirectory { get; init; }
    public string LogDirectory { get; init; }
    public string DataDirectory { get; init; }
}