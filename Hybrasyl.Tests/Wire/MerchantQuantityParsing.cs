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

using Hybrasyl.Servers;
using System;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The merchant quantity prompts are plain client text fields, so anything can arrive.
///     <c>World.TryReadQuantity</c> replaced <c>Convert.ToUInt32</c>, which threw on bad input and
///     surfaced as an unhandled packet-handler exception. These pin that the replacement
///     <strong>accepts exactly what Convert accepted</strong> and merely rejects — rather than
///     throws on — everything else, so no previously-working input was narrowed.
/// </summary>
/// <remarks>
///     The oracle here is <see cref="Convert.ToUInt32(string)" /> itself rather than a restatement
///     of what it is believed to do; that independence is the point (see this delta's post-mortem).
/// </remarks>
public class MerchantQuantityParsing
{
    // The real parse, not a restatement of it — a local copy could not fail for the reason
    // these cases exist.
    private static bool TryRead(string text, out uint value) => World.TryParseQuantity(text, out value);

    [Theory]
    // Accepted by Convert, and still accepted — including the tolerances easy to drop by
    // reaching for NumberStyles.None.
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("37")]
    [InlineData("4294967295")]
    [InlineData(" 7")]
    [InlineData("7 ")]
    [InlineData("+7")]
    // Threw before (FormatException / OverflowException) — now rejected cleanly.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    [InlineData("jafksdjadisojfasdi")]
    [InlineData("1,000")]
    [InlineData("3.5")]
    [InlineData("0x10")]
    public void AcceptSetMatchesTheLegacyConvertExactly(string input)
    {
        uint expected = 0;
        var convertAccepted = true;
        try
        {
            expected = Convert.ToUInt32(input);
        }
        catch (FormatException)
        {
            convertAccepted = false;
        }
        catch (OverflowException)
        {
            convertAccepted = false;
        }

        var accepted = TryRead(input, out var actual);

        Assert.Equal(convertAccepted, accepted);
        if (convertAccepted)
            Assert.Equal(expected, actual);
    }

    [Fact]
    public void RejectedInputYieldsZeroSoTheHandlerCannotActOnGarbage()
    {
        Assert.False(TryRead("jafksdjadisojfasdi", out var value));
        Assert.Equal(0u, value);
    }
}
