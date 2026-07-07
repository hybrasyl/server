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

using Hybrasyl.Networking;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

// Probe command for the DALib networking run: render a paper packet (0x35 ReadonlyPaper /
// 0x1B EditablePaper) on the live retail client with controlled fields, so the Type-byte
// texture mapping and the Width/Height orientation can be resolved by eye.
//
// Bytes are written in the order the retail client deserializer reads them (verified via
// Ghidra: FUN_0054a530 editable / FUN_0054a680 readonly). The paper's text echoes the
// parameters sent so the rendered result is self-labelling.
internal class PaperCommand : ChatCommand
{
    public new static string Command = "paper";
    // The handler reads '|' as the separator between alternative argument-count forms and
    // counts '<' in each to build the allowed arg counts. So this declares two forms:
    // <kind> <type> (2 args; width/height default) and <kind> <type> <width> <height> (4 args).
    public new static string ArgumentText = "<kind> <type> | <kind> <type> <width> <height>";

    public new static string HelpText =
        "Render a paper packet on your client. kind=read (0x35) or edit (0x1B); " +
        "type/width/height are bytes. Defaults: read 2 3 12 (distinct W/H so orientation shows).";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        var kind = args.Length > 0 ? args[0].ToLowerInvariant() : "read";

        byte type = 2, width = 3, height = 12;
        if (args.Length > 1 && !byte.TryParse(args[1], out type))
            return Fail("type must be a byte (0-255)");
        if (args.Length > 2 && !byte.TryParse(args[2], out width))
            return Fail("width must be a byte (0-255)");
        if (args.Length > 3 && !byte.TryParse(args[3], out height))
            return Fail("height must be a byte (0-255)");

        var text = $"kind={kind} type={type} w={width} h={height}";

        switch (kind)
        {
            case "read":
            {
                // 0x35 ReadonlyPaper: [Type][Width][Height][Centered][string16 Text]
                var packet = new ServerPacket(0x35);
                packet.WriteByte(type);
                packet.WriteByte(width);
                packet.WriteByte(height);
                packet.WriteBoolean(false); // Centered
                packet.WriteString16(text);
                user.Enqueue(packet);
                return Success($"Sent 0x35 ReadonlyPaper: {text}");
            }
            case "edit":
            {
                // 0x1B EditablePaper: [Slot][Type][Width][Height][string16 Text]
                var packet = new ServerPacket(0x1B);
                packet.WriteByte(0x00); // Slot
                packet.WriteByte(type);
                packet.WriteByte(width);
                packet.WriteByte(height);
                packet.WriteString16(text);
                user.Enqueue(packet);
                return Success($"Sent 0x1B EditablePaper: {text}");
            }
            default:
                return Fail("kind must be 'read' or 'edit'");
        }
    }
}
