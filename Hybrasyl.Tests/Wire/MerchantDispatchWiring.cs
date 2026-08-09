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
using Hybrasyl.Xml.Objects;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     That a 0x39 arriving at the world handler actually reaches the registered merchant callback,
///     carrying the value its declared form parses out.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="MerchantResponseForms" /> reads the registration table; it proves each
///         callback <em>declares</em> the right form and says nothing about whether anything
///         dispatches to it. That gap was real: commenting out the <c>handler.Invoke</c> call in
///         <c>PacketHandler_0x39_NPCMainMenu</c> left the entire suite green. Every merchant test
///         drives <c>User.Show…</c> or reflects onto a callback directly, so all 46 registrations
///         and the dispatch that reads them were unexercised.
///     </para>
///     <para>
///         That is the same shape as the two P5a failures — components correct, chain unwired, suite
///         green — which is why this asserts the chain rather than another component. The
///         <c>MerchantMenuHandlers</c> table was also empty in every test run until the fixture was
///         taught to call <c>SetMerchantMenuHandlers</c>, so nothing here could have worked before.
///     </para>
///     <para>
///         Bodies are hand-assembled rather than built with DALib's writer, so a writer bug cannot
///         make a broken chain look wired. Layout is the protocol reference's 0x39 prefix
///         <c>[u8 objectType][u32 objectId][u16 pursuitId]</c> — multi-byte fields big-endian — then
///         the form's tail.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class MerchantDispatchWiring
{
    public MerchantDispatchWiring(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    private const string Sentinel = "no merchant callback ran";

    private Merchant GetTestMerchant()
    {
        var merchant = Fixture.Map.Objects.OfType<Merchant>().FirstOrDefault(predicate: x => x.Name == "Maria");
        Assert.NotNull(merchant);
        return merchant;
    }

    /// <summary>The 0x39 prefix, then whatever tail the caller's form carries.</summary>
    private static byte[] Body(uint objectId, MerchantMenuItem item, params byte[] tail)
    {
        var body = new byte[7 + tail.Length];
        body[0] = 0x01; // ObjectTypeCreature
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(1, 4), objectId);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(5, 2), (ushort)item);
        tail.CopyTo(body, 7);
        return body;
    }

    private static byte[] String8(string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        var tail = new byte[bytes.Length + 1];
        tail[0] = (byte)bytes.Length;
        bytes.CopyTo(tail, 1);
        return tail;
    }

    private void Dispatch(InboundPacket packet)
    {
        var handler = typeof(World).GetMethod("PacketHandler_0x39_NPCMainMenu",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(handler);
        handler.Invoke(Game.World, [Fixture.TestUser, packet]);
    }

    /// <summary>
    ///     Text form (B), asserted on the <em>content</em> of the string and not merely on the
    ///     callback having run.
    /// </summary>
    /// <remarks>
    ///     The first version of this dispatched an invalid quantity and asserted the rejection
    ///     message. It passed with the parse mutated to hand the callback <c>string.Empty</c> —
    ///     because empty is rejected too, so nothing about it depended on the text surviving the
    ///     chain. Depositing a specific amount makes the value load-bearing: drop the text and no
    ///     gold moves.
    /// </remarks>
    [Fact]
    public void TextFormReachesItsCallbackWithTheTypedString()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();

        Fixture.TestUser.Stats.Gold = 1000;
        var vaultBefore = Fixture.TestUser.Vault.CurrentGold;

        var body = Body(merchant.Id, MerchantMenuItem.DepositGoldQuantity, String8("123"));
        Dispatch(new InboundPacket(0x39, body));

        Assert.Equal(vaultBefore + 123, Fixture.TestUser.Vault.CurrentGold);
        Assert.Equal(1000u - 123, Fixture.TestUser.Stats.Gold);
    }

    /// <summary>
    ///     Option form (E). The slot byte must arrive intact, so this asserts the value and not just
    ///     that something happened — a chain that dropped the tail and passed 0 would still "work".
    /// </summary>
    [Fact]
    public void OptionFormReachesItsCallbackWithTheSlotByte()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();

        Assert.True(Game.World.WorldData.TryGetValueByIndex("Epee", out Item epee),
            "Couldn't find epee in test items");
        var first = new ItemObject(epee, Fixture.TestUser.World.Guid);
        var second = new ItemObject(epee, Fixture.TestUser.World.Guid);
        Assert.True(Fixture.TestUser.AddItem(first), "Couldn't add item to inventory");
        Assert.True(Fixture.TestUser.AddItem(second), "Couldn't add second item to inventory");
        first.Durability /= 2;
        second.Durability /= 2;

        // Park the pending slot on 2 through the public path, so asserting 1 below proves the
        // dispatched option byte moved it rather than finding it already there.
        Fixture.TestUser.ShowRepairItem(merchant, 2);
        Assert.Equal(2, Fixture.TestUser.PendingRepairSlot);

        var body = Body(merchant.Id, MerchantMenuItem.RepairItem, 1);
        Dispatch(new InboundPacket(0x39, body));

        Assert.Equal(1, Fixture.TestUser.PendingRepairSlot);
    }

    /// <summary>
    ///     The job gate still bites: a merchant without the required job never reaches the callback.
    ///     Without this, a dispatch that skipped the gate would look identical to one that honoured
    ///     it in the two tests above, since Maria holds every job.
    /// </summary>
    [Fact]
    public void AMerchantWithoutTheRequiredJobDoesNotReachTheCallback()
    {
        Fixture.ResetTestUserStats();
        var merchant = GetTestMerchant();
        var jobs = merchant.Jobs;
        Fixture.TestUser.SendSystemMessage(Sentinel);

        try
        {
            merchant.Jobs = MerchantJob.Repair; // anything but Bank
            var body = Body(merchant.Id, MerchantMenuItem.DepositGoldQuantity, String8("notanumber"));
            Dispatch(new InboundPacket(0x39, body));
        }
        finally
        {
            merchant.Jobs = jobs;
        }

        Assert.Equal(Sentinel, Fixture.TestUser.LastSystemMessage);
    }
}
