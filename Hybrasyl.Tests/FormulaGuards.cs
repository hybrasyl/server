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

using Hybrasyl.Subsystems.Formulas;
using Xunit;

namespace Hybrasyl.Tests;

/// <summary>
///     HS-1506: guard rails on designer-authored formulas. Every consumer of
///     <see cref="FormulaParser.Eval" /> casts the returned double to a narrower unsigned
///     type, where .NET *saturates* rather than erroring — (uint)infinity is 4294967295,
///     indistinguishable from a legitimate award. These pin that no formula can produce a
///     non-finite or silently-wrapped result.
/// </summary>
/// <remarks>
///     DAMAGE is used as the zero divisor because Parameterize always defines it and it
///     defaults to 0 on an empty FormulaEvaluation, so these need no world state.
/// </remarks>
[Collection("Hybrasyl")]
public class FormulaGuards
{
    public FormulaGuards(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    public HybrasylFixture Fixture { get; set; }

    [Fact]
    public void DivisionByZeroReturnsZeroRatherThanInfinity()
    {
        // Pre-guard: NCalc returned ∞, which a (uint) cast saturates to 4294967295.
        Assert.Equal(0.0, FormulaParser.Eval("100 / DAMAGE", new FormulaEvaluation()));
    }

    [Fact]
    public void NegativeDivisionByZeroReturnsZeroRatherThanNegativeInfinity()
    {
        Assert.Equal(0.0, FormulaParser.Eval("-100 / DAMAGE", new FormulaEvaluation()));
    }

    [Fact]
    public void ZeroDividedByZeroReturnsZeroRatherThanNaN()
    {
        // OverflowProtection does NOT catch this one; only the IsFinite check does.
        Assert.Equal(0.0, FormulaParser.Eval("DAMAGE / DAMAGE", new FormulaEvaluation()));
    }

    [Fact]
    public void PowOverflowReturnsZeroRatherThanInfinity()
    {
        // Also not caught by OverflowProtection — doubles do not throw on overflow.
        Assert.Equal(0.0, FormulaParser.Eval("Pow(10, 400)", new FormulaEvaluation()));
    }

    [Fact]
    public void IntegerOverflowReturnsZeroRatherThanWrappingNegative()
    {
        // Pre-guard this wrapped to -294967296 and was applied as a real value.
        Assert.Equal(0.0, FormulaParser.Eval("2000000000 * 2", new FormulaEvaluation()));
    }

    [Fact]
    public void RoundUsesAwayFromZeroNotBankers()
    {
        // .NET's default is round-half-to-even, so this was 2.
        Assert.Equal(3.0, FormulaParser.Eval("Round(2.5, 0)", new FormulaEvaluation()));
        // Unchanged by the option; pins that only the .5 case moved.
        Assert.Equal(4.0, FormulaParser.Eval("Round(3.5, 0)", new FormulaEvaluation()));
        Assert.Equal(2.0, FormulaParser.Eval("Round(2.4, 0)", new FormulaEvaluation()));
    }

    [Theory]
    // The shipped default formulas must be bit-identical under the new options. If enabling
    // OverflowProtection or RoundAwayFromZero ever changes a real formula, this fails.
    [InlineData("Pow(SOURCELEVEL,3)*250", 242574750.0)]
    [InlineData("SOURCESTR+(SOURCELEVEL/4) + 48", 327.75)]
    [InlineData("(SOURCESTR+(SOURCELEVEL/4) + 48)/2", 163.875)]
    [InlineData("25 / 100", 0.25)]
    public void RealFormulasAreUnaffectedByTheGuards(string formula, double expected)
    {
        Fixture.ResetTestUserStats();
        Fixture.TestUser.Stats.Level = 99;
        Fixture.TestUser.Stats.BaseStr = 255;

        try
        {
            var eval = new FormulaEvaluation { Source = Fixture.TestUser };
            Assert.Equal(expected, FormulaParser.Eval(formula, eval));
        }
        finally
        {
            Fixture.ResetTestUserStats();
        }
    }
}
