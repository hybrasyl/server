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

using System;
using System.Globalization;
using Hybrasyl.Networking;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

// Probe command for the DALib networking run: send an arbitrary S→C packet to your own client,
// so the retail client's behavior for any opcode can be observed directly (used to verify the
// dormant-opcode consumer traces). The opcode byte is prepended by ServerPacket; the body bytes
// are written verbatim in the order given. GM-only. See /probe for named convenience wrappers.
internal class SendRawCommand : ChatCommand
{
    public new static string Command = "sendraw";

    // Two forms: <opcode> (no body) and <opcode> <bodyHex> (a contiguous hex body string).
    public new static string ArgumentText = "<opcodeHex> | <opcodeHex> <bodyHex>";

    public new static string HelpText =
        "Send a raw S->C packet to your client. opcodeHex is one hex byte (e.g. 06); bodyHex is an " +
        "optional contiguous hex string (e.g. 0A0B0201). Examples: /sendraw 21  |  /sendraw 47 0005";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (!TryParseHexByte(args[0], out var opcode))
            return Fail($"opcode '{args[0]}' is not a hex byte (00-FF).");

        var body = Array.Empty<byte>();
        if ((args.Length > 1) && !TryParseHexBytes(args[1], out body))
            return Fail($"body '{args[1]}' is not a valid hex string (even number of hex digits).");

        var packet = new ServerPacket(opcode);
        foreach (var b in body)
            packet.WriteByte(b);
        user.Enqueue(packet);

        return Success($"Sent S->C 0x{opcode:X2} with {body.Length}-byte body [{Convert.ToHexString(body)}].");
    }

    private static bool TryParseHexByte(string s, out byte value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseHexBytes(string s, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        if (s.Length == 0)
            return true;
        if ((s.Length % 2) != 0)
            return false;
        try
        {
            bytes = Convert.FromHexString(s);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
