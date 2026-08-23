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

using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Xml.Objects;
using System;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     A merchant menu may only be driven against a merchant the player could have clicked.
/// </summary>
/// <remarks>
///     <c>World.Objects</c> is keyed globally and the 0x39 handler resolved its target from the
///     client-supplied id alone, with no check of map or distance. That is the substrate the
///     economy findings are reached through: every banking, vending and repair operation on the
///     menu path was reachable from anywhere in the world against any merchant in it.
/// </remarks>
[Collection("Hybrasyl")]
public class MerchantInteractionRange
{
    public MerchantInteractionRange(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    /// <summary>The 0x39 prefix: <c>[u8 objectType][u32 BE objectId][u16 BE pursuitId]</c>.</summary>
    private static byte[] Body(uint objectId, MerchantMenuItem item, Action<LegacyBodyWriter>? tail = null)
    {
        var body = new LegacyBodyWriter(0x39);
        body.WriteByte(0x01);
        body.WriteUInt32(objectId);
        body.WriteUInt16((ushort) item);
        tail?.Invoke(body);

        return body.BodyMemory.ToArray();
    }

    [Fact]
    public void AMerchantOnAnotherMap_CannotBeBanked()
    {
        Fixture.ResetTestUserStats();

        var here = Fixture.Map.Objects.OfType<Merchant>().Single(x => x.Name == "Maria");
        var elsewhere = Game.World.Objects.Values.OfType<Merchant>()
            .FirstOrDefault(x => x.Name == "Maria" && x.Location.Map != Fixture.Map);

        Assert.True(elsewhere is not null,
            "precondition: a second Maria must exist on another map for this to mean anything");
        Assert.NotEqual(here.Id, elsewhere!.Id);

        Fixture.TestUser.Stats.Gold = 1000;
        var vaultBefore = Fixture.TestUser.Vault.CurrentGold;

        Game.World.WorldPacketHandlers[0x39].Invoke(Fixture.TestUser,
            new InboundBody(0x39, Body(elsewhere.Id, MerchantMenuItem.DepositGoldQuantity,
                b => b.WriteString8("123"))));

        Assert.Equal(vaultBefore, Fixture.TestUser.Vault.CurrentGold);
        Assert.Equal(1000u, Fixture.TestUser.Stats.Gold);
    }

    /// <summary>
    ///     The same operation against the merchant standing next to the player still works, so the
    ///     test above is about range and not about the menu being broken outright.
    /// </summary>
    [Fact]
    public void AMerchantInViewport_CanStillBeBanked()
    {
        Fixture.ResetTestUserStats();

        var merchant = Fixture.Map.Objects.OfType<Merchant>().Single(x => x.Name == "Maria");
        Assert.True(Fixture.TestUser.GetViewport().Contains(merchant.X, merchant.Y),
            "precondition: the fixture's merchant must be inside the user's viewport");

        Fixture.TestUser.Stats.Gold = 1000;
        var vaultBefore = Fixture.TestUser.Vault.CurrentGold;

        Game.World.WorldPacketHandlers[0x39].Invoke(Fixture.TestUser,
            new InboundBody(0x39, Body(merchant.Id, MerchantMenuItem.DepositGoldQuantity,
                b => b.WriteString8("123"))));

        Assert.Equal(vaultBefore + 123, Fixture.TestUser.Vault.CurrentGold);
        Assert.Equal(1000u - 123, Fixture.TestUser.Stats.Gold);
    }

    /// <summary>
    ///     0x3A carries the same defect and needed its own test: removing the range check from the
    ///     dialog handler left every other test in the suite green.
    /// </summary>
    /// <remarks>
    ///     A dialog session tracks a pursuit id and index, not the object the pursuit belongs to, so
    ///     an out-of-range object supplied on the wire was accepted as the thing being clicked and
    ///     drove a session opened against something else entirely.
    /// </remarks>
    [Fact]
    public void AnOutOfRangeObject_CannotDriveADialogSessionOpenedAgainstAnother()
    {
        Fixture.ResetTestUserStats();

        var here = Fixture.Map.Objects.OfType<Merchant>().Single(x => x.Name == "Maria");
        var elsewhere = Game.World.Objects.Values.OfType<Merchant>()
            .First(x => x.Name == "Maria" && x.Location.Map != Fixture.Map);

        var options = new OptionsDialog("Which one?");
        options.AddDialogOption(new DialogOption { OptionText = "The first one" });

        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = new DialogSequence("OutOfRangeProbe") { Id = pursuitId };
        sequence.AddDialog(options);

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(here, sequence), "couldn't start the probe dialog");
        var indexBefore = Fixture.TestUser.DialogState.CurrentPursuitIndex;

        try
        {
            // Prefix [u8 objectType][u32 BE objectId][u16 BE pursuitId][u16 BE pursuitIndex], then
            // an option tail. The id names the merchant on the other map.
            var writer = new LegacyBodyWriter(0x3A);
            writer.WriteByte(0x01);
            writer.WriteUInt32(elsewhere.Id);
            writer.WriteUInt16((ushort) pursuitId);
            writer.WriteUInt16((ushort) (indexBefore + 1));
            writer.WriteByte(0x01);
            writer.WriteByte(0x01);
            var wire = writer.BodyMemory.ToArray();
            Game.World.WorldPacketHandlers[0x3A].Invoke(Fixture.TestUser, new InboundBody(0x3A, wire));

            Assert.True(Fixture.TestUser.DialogState.InDialog,
                "the out-of-range click ended a session it should not have been able to touch");
            Assert.Equal(indexBefore, Fixture.TestUser.DialogState.CurrentPursuitIndex);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
        }
    }
}
