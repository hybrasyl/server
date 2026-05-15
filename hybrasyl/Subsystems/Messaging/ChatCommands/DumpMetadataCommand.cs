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

using Hybrasyl.Internals.Metafiles;
using Hybrasyl.Objects;
using System;
using System.IO;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

internal class DumpMetadataCommand : ChatCommand
{
    public new static string Command = "dumpmetadata";
    public new static string ArgumentText = "<string metadatafile>";
    public new static string HelpText = "Dump (in hex) a metadata file ";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.ContainsKey<CompiledMetafile>(args[0]))
        {
            var file = Game.World.WorldState.Get<CompiledMetafile>(args[0]);
            var filepath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hybrasyl");
            File.WriteAllBytes($"{filepath}\\{args[0]}.mdf", file.Data);
            return Success($"{filepath}\\{args[0]}.mdf written to disk");
        }

        return Fail("Look chief idk about all that");
    }
}