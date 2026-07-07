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
///     TEST / PROBE — emit a raw S→C 0x51 BlockInput to lock (<c>on</c>) or release (<c>off</c>) player
///     input. 0x51 is a client-supported opcode no server normally sends: the retail client routes it to its
///     SBlockInput document class and toggles input (State 1 = block, 0 = release). Use this to observe what
///     a received BlockInput actually does in the client. See <c>User.SendBlockInputProbe</c> and
///     <c>samhail/binary/0x51-blockinput-recv</c>.
/// </summary>
internal class PinputCommand : ChatCommand
{
    public new static string Command = "pinput";
    public new static string ArgumentText = "<on|off>";

    public new static string HelpText =
        "Emit a raw 0x51 BlockInput to lock (on) or release (off) player input — a client-supported opcode " +
        "no server normally sends. Probe to observe the effect.";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (args.Length < 1)
            return Fail("Usage: /pinput <on|off>");

        bool block;

        switch (args[0].ToLowerInvariant())
        {
            case "on":
                block = true;
                break;
            case "off":
                block = false;
                break;
            default:
                return Fail("Usage: /pinput <on|off>");
        }

        user.SendBlockInputProbe(block);

        return Success($"Sent 0x51 BlockInput probe: input {(block ? "blocked (State 1)" : "released (State 0)")}.");
    }
}
