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

using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

/// <summary>
///     TEST / PROBE — emit a hand-built S→C 0x30 NpcDialog of a chosen DialogType to see how the retail
///     client renders it. Hybrasyl only ever drives types 0/2/4/10; this exposes the client-only
///     "fossils" 3 (SimpleOptions), 5 (SimpleTextInput), 6 (OptionsWithFace) and 9 (NexonId protected
///     input). See <c>User.ShowDialogProbe</c> and <c>samhail/binary/0x30-npcdialog-recv</c>.
/// </summary>
internal class DialogProbeCommand : ChatCommand
{
    public new static string Command = "dialogprobe";
    public new static string ArgumentText = "<byte dialogType>";

    public new static string HelpText =
        "Emit a raw 0x30 NpcDialog of the given DialogType (0,2,3,4,5,6,9,10) to probe client rendering. " +
        "Run with an NPC dialog already open if a bare invocation does not display.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length < 1 || !byte.TryParse(args[0], out var dialogType))
            return Fail("Usage: /dialogprobe <dialogType>  (try 3, 5, 6 or 9)");

        user.ShowDialogProbe(dialogType);
        return Success($"Sent 0x30 NpcDialog probe, DialogType {dialogType}.");
    }
}
