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

using System;
using System.Globalization;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

/// <summary>
///     Parses the hex text the raw-probe commands take from chat, with an optional <c>0x</c>
///     prefix. Rejects odd digit counts rather than padding: a dropped nibble that silently
///     becomes a byte would be indistinguishable from a client behaviour.
/// </summary>
internal static class HexBytes
{
    public static bool TryParseByte(string text, out byte value)
    {
        var trimmed = Strip(text);

        return byte.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseBytes(string text, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        var trimmed = Strip(text);
        if (trimmed.Length == 0)
            return true;
        if (trimmed.Length % 2 != 0)
            return false;

        try
        {
            bytes = Convert.FromHexString(trimmed);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Strip(string text)
    {
        var trimmed = text.Trim();

        return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? trimmed[2..] : trimmed;
    }
}
