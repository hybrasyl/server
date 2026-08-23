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
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using System;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

// Send an arbitrary S->C packet to your own client, so the retail client's behavior for any
// opcode can be observed directly. The body bytes are written verbatim, so nothing here asserts
// a shape for the opcode being probed - which is the point when the shape is what is under test.
// GM-only.
internal class SendRawCommand : ChatCommand
{
    public new static string Command = "sendraw";

    // Two forms: <opcode> (no body) and <opcode> <bodyHex> (a contiguous hex body string).
    public new static string ArgumentText = "<opcodeHex> | <opcodeHex> <bodyHex>";

    public new static string HelpText =
        "Send a raw S->C packet to your client. opcodeHex is one hex byte (e.g. 4F); bodyHex is an " +
        "optional contiguous hex string (e.g. 0A0B0201). Examples: /sendraw 21  |  /sendraw 47 0005";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (!HexBytes.TryParseByte(args[0], out var opcode))
            return Fail($"opcode '{args[0]}' is not a hex byte (00-FF).");

        var body = Array.Empty<byte>();
        if (args.Length > 1 && !HexBytes.TryParseBytes(args[1], out body))
            return Fail($"body '{args[1]}' is not a valid hex string (even number of hex digits).");

        user.Enqueue(new RawServerPacket(opcode, body));

        GameLog.Info("CAPTURE {Name}: S->C 0x{Opcode:X2}, {Length} body bytes: {Hex}",
            user.Name, opcode, body.Length, Convert.ToHexString(body));

        return Success($"Sent S->C 0x{opcode:X2} with {body.Length}-byte body [{Convert.ToHexString(body)}].");
    }
}
