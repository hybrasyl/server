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
using Hybrasyl.Networking;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

// Probe commands for the DALib networking run: push specific dormant-but-client-live S→C opcodes to
// your own client to confirm the consumer traces by eye. Bytes match the verified client deserializer
// layouts (see DALib Packets.Server + the samhail consumer findings). GM-only. Use /sendraw for
// arbitrary opcodes.
internal class ProbeCommand : ChatCommand
{
    public new static string Command = "probe";

    // Allowed arg counts {1,2,3}: <what>, <what> <a>, <what> <a> <b>.
    public new static string ArgumentText = "<what> | <what> <a> | <what> <a> <b>";

    public new static string HelpText =
        "Send a known dormant S->C opcode to your client. what = mapedit [tileId] | selfsave | " +
        "screenshot [byte] | levelpoint [a] [b] | windowchange [byte] | badguy [real]. " +
        "badguy defaults to a safe wrong-magic send (gate fails, nothing written).";

    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        switch (args[0].ToLowerInvariant())
        {
            case "selfsave": // 0x21 SelfSave — expect a "Saved." popup
                user.Enqueue(new ServerPacket(0x21));
                return Success("Sent 0x21 SelfSave (expect a \"Saved.\" popup).");

            case "screenshot": // 0x6B — consumer opens TownMapPane (RTTI SScreenShot is a misnomer)
            {
                byte b = 0;
                if ((args.Length > 1) && !byte.TryParse(args[1], out b))
                    return Fail("screenshot: byte must be 0-255.");
                var p = new ServerPacket(0x6B);
                p.WriteByte(b);
                user.Enqueue(p);
                return Success($"Sent 0x6B Screenshot byte={b} (expect TownMapPane to open).");
            }

            case "windowchange": // 0x3E — switches a panel to a mode (byte 0-4 -> 0/3/1/5/7)
            {
                byte b = 0;
                if ((args.Length > 1) && !byte.TryParse(args[1], out b))
                    return Fail("windowchange: byte must be 0-255.");
                var p = new ServerPacket(0x3E);
                p.WriteByte(b);
                user.Enqueue(p);
                return Success($"Sent 0x3E WindowChange byte={b} (modes 0-4 map to 0/3/1/5/7).");
            }

            case "levelpoint": // 0x3D — unspent-points indicator; notify effect fires when b != 0
            {
                byte a = 1, b = 1;
                if ((args.Length > 1) && !byte.TryParse(args[1], out a))
                    return Fail("levelpoint: a must be 0-255.");
                if ((args.Length > 2) && !byte.TryParse(args[2], out b))
                    return Fail("levelpoint: b must be 0-255.");
                var p = new ServerPacket(0x3D);
                p.WriteByte(a);
                p.WriteByte(b);
                user.Enqueue(p);
                return Success($"Sent 0x3D LevelPoint a={a} b={b} (notify effect fires when b != 0).");
            }

            case "mapedit": // 0x06 — patch a 1x3 column at your tile so width/height orientation is visible
            {
                ushort tileId = 1;
                if ((args.Length > 1) && !ushort.TryParse(args[1], out tileId))
                    return Fail("mapedit: tileId must be 0-65535.");

                byte startX = user.X, startY = user.Y, width = 1, height = 3;
                var p = new ServerPacket(0x06);
                p.WriteByte(startX);
                p.WriteByte(startY);
                p.WriteByte(width);
                p.WriteByte(height);
                for (var i = 0; i < (width * height); i++)
                {
                    p.WriteUInt16(tileId); // Background
                    p.WriteUInt16(0);      // LeftForeground
                    p.WriteUInt16(0);      // RightForeground
                }

                user.Enqueue(p);
                return Success(
                    $"Sent 0x06 MapEdit: {width}x{height} column at ({startX},{startY}) bg={tileId} (watch the tiles change).");
            }

            case "badguy": // 0x4A — covert anti-tamper. Default = WRONG magic so the gate fails (nothing written).
            {
                var real = (args.Length > 1) && args[1].Equals("real", StringComparison.OrdinalIgnoreCase);
                const byte type = 0x00;
                const byte payload = 0x42;
                var magic = real ? 0x7D3AFF99u : 0x00000000u;

                var p = new ServerPacket(0x4A);
                p.WriteByte(type);
                p.WriteByte(payload);
                p.WriteUInt32(magic);
                user.Enqueue(p);

                return real
                    ? Success(
                        "Sent 0x4A BadGuy with REAL magic 0x7D3AFF99 — on an ELEVATED client this drops the hidden " +
                        "%System32%\\Mscfg.dll marker (silently fails without admin). Use a disposable VM only.")
                    : Success(
                        "Sent 0x4A BadGuy with safe wrong-magic (gate fails, no file written). " +
                        "Append 'real' to fire the actual marker-drop.");
            }

            default:
                return Fail(
                    "Unknown probe. what = mapedit | selfsave | screenshot | levelpoint | windowchange | badguy.");
        }
    }
}
