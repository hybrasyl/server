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
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

// Assemble an S->C body across several chat lines, then send it. The retail chat input is far
// shorter than a populated packet body, so /sendraw cannot carry one. Like /sendraw this stays
// byte-level on purpose: a command taking semantic arguments would build the body from our
// current understanding of a layout, which is the thing these probes exist to test. GM-only.
internal class RawBufferCommand : ChatCommand
{
    public new static string Command = "rawbuf";

    // ChatCommandHandler derives the allowed arg counts by splitting this on '|' and counting '<'
    // per option, so a '|' inside a placeholder silently yields the wrong set. This reads {1, 2};
    // the subcommand names live in HelpText.
    public new static string ArgumentText = "<subcommand> | <subcommand> <hex>";

    public new static string HelpText =
        "Assemble a raw S->C body across lines. /rawbuf add <hex> appends; /rawbuf send <opcodeHex> " +
        "emits the buffer under that opcode and keeps it, so the same stimulus can be repeated; " +
        "/rawbuf sendclear <opcodeHex> emits and clears; /rawbuf show reports it; /rawbuf clear " +
        "discards it. Example: /rawbuf add 0100000001 then /rawbuf send 4F";

    public new static bool Privileged = true;

    // A retail frame carries a u16 length, so a body past this could never be sent whole; refusing
    // at append time reports the overflow at the line that caused it.
    private const int MaxBodyLength = ushort.MaxValue;

    // Keyed by character guid so two GMs assembling probes at once cannot interleave into one
    // body. Chat commands run on the world thread today; the concurrent map is here so that stops
    // being load-bearing.
    private static readonly ConcurrentDictionary<Guid, List<byte>> Buffers = new();

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        switch (args[0].ToLowerInvariant())
        {
            case "clear":
                Buffers.TryRemove(user.Guid, out _);

                return Success("Raw buffer cleared.");

            case "show":
            {
                var current = Buffers.GetValueOrDefault(user.Guid, []);

                return Success(current.Count == 0
                    ? "Raw buffer is empty."
                    : $"Raw buffer: {current.Count} bytes [{Convert.ToHexString(current.ToArray())}]");
            }

            case "add":
            {
                if (args.Length < 2)
                    return Fail("add: needs a hex string.");
                if (!HexBytes.TryParseBytes(args[1], out var chunk))
                    return Fail($"add: '{args[1]}' is not a valid hex string (even number of hex digits).");

                var buffer = Buffers.GetOrAdd(user.Guid, _ => []);
                if (buffer.Count + chunk.Length > MaxBodyLength)
                    return Fail($"add: buffer would exceed {MaxBodyLength} bytes; not appended.");

                buffer.AddRange(chunk);

                return Success($"Appended {chunk.Length}; buffer is {buffer.Count} bytes.");
            }

            // send keeps the buffer so one stimulus can be repeated verbatim - re-provoking with
            // identical bytes is how a response that varies gets told from one that doesn't.
            case "send":
            case "sendclear":
            {
                var clearAfter = args[0].Equals("sendclear", StringComparison.OrdinalIgnoreCase);

                if (args.Length < 2)
                    return Fail($"{args[0]}: needs an opcode.");
                if (!HexBytes.TryParseByte(args[1], out var opcode))
                    return Fail($"{args[0]}: opcode '{args[1]}' is not a hex byte (00-FF).");

                var body = clearAfter
                    ? Buffers.TryRemove(user.Guid, out var removed) ? removed.ToArray() : []
                    : Buffers.TryGetValue(user.Guid, out var kept) ? kept.ToArray() : [];

                user.Enqueue(new RawServerPacket(opcode, body));

                // At Info, and in the same transcript as the inbound capture, so a 0x54 that
                // follows can be paired with the exact bytes that provoked it. Logged every send,
                // repeats included: two identical lines are the record of two stimuli.
                GameLog.Info("CAPTURE {Name}: S->C 0x{Opcode:X2}, {Length} body bytes: {Hex}",
                    user.Name, opcode, body.Length, Convert.ToHexString(body));

                return Success($"Sent S->C 0x{opcode:X2} with {body.Length}-byte body; " +
                               $"buffer {(clearAfter ? "cleared" : $"kept ({body.Length} bytes)")}.");
            }

            default:
                return Fail(
                    $"rawbuf: unknown subcommand '{args[0]}' (clear | show | add | send | sendclear).");
        }
    }
}
