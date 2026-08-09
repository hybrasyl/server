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

using DALib.Networking.Packets.Server;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Xml.Objects;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     HS-1577's send half — that the menus <c>User.Show…</c> actually puts on the wire offer each
///     merchant item under a menu type whose reply that item's callback parses.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="MerchantResponseForms" /> pins the receive side against the protocol, but its
///         expectations were read off <c>User.cs</c> by hand and go stale the moment a
///         <c>Show…Menu</c> offers an item under a different menu type — the one link HS-1577 was
///         left carrying as a human audit. Driving the real send methods and reading the menu type
///         off the emitted packet closes it: there is no table here to fall out of date.
///     </para>
///     <para>
///         The sweep is deliberately not exhaustive over <c>Show…</c> — some need pending merchant
///         state to reach their menu — so <see cref="MenuTypesTheSweepMustReach" /> asserts which
///         shapes it did reach. Without that, a fixture change that stopped emitting menus would
///         leave this passing on an empty set.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class MerchantMenuFormsAtTheWire(HybrasylFixture fixture)
{
    /// <summary>
    ///     Every menu shape a merchant flow can put on the wire. A sweep that misses one is not
    ///     evidence about that shape's pairing, so it fails rather than passing quietly.
    /// </summary>
    private static readonly NpcMenuType[] MenuTypesTheSweepMustReach =
    [
        NpcMenuType.Options,
        NpcMenuType.TextEntry,
        NpcMenuType.ItemList,
        NpcMenuType.PlayerItemList,
        NpcMenuType.SkillList,
        NpcMenuType.SpellList,
        NpcMenuType.PlayerSkillList,
        NpcMenuType.PlayerSpellList
    ];

    private HybrasylFixture Fixture { get; } = fixture;

    private Merchant GetTestMerchant()
    {
        var merchant = Fixture.Map.Objects.OfType<Merchant>().FirstOrDefault(predicate: x => x.Name == "Maria");
        Assert.NotNull(merchant);
        return merchant;
    }

    /// <summary>
    ///     The menus a merchant conversation reaches without first parking pending state on the user.
    /// </summary>
    private static IEnumerable<(string Label, Action<User, Merchant> Show)> Flows()
    {
        // Through the interface: DisplayPursuits is a sealed default member on IPursuitable, and it
        // is the one merchant menu built without going through User.MerchantMenu.
        yield return ("pursuit menu", (u, m) => ((IPursuitable) m).DisplayPursuits(u));
        yield return ("go back", (u, m) => u.ShowMerchantGoBack(m, "back"));
        yield return ("buy", (u, m) => u.ShowBuyMenu(m));
        yield return ("buy quantity", (u, m) => u.ShowBuyMenuQuantity(m, "Sausages"));
        yield return ("sell", (u, m) => u.ShowSellMenu(m));
        yield return ("sell quantity", (u, m) => u.ShowSellQuantity(m, 1));
        yield return ("learn skill", (u, m) => u.ShowLearnSkillMenu(m));
        yield return ("learn spell", (u, m) => u.ShowLearnSpellMenu(m));
        yield return ("forget skill", (u, m) => u.ShowForgetSkillMenu(m));
        yield return ("forget spell", (u, m) => u.ShowForgetSpellMenu(m));
        yield return ("send parcel", (u, m) => u.ShowMerchantSendParcel(m));
        yield return ("send parcel recipient", (u, m) => u.ShowMerchantSendParcelRecipient(m));
        yield return ("deposit gold", (u, m) => u.ShowDepositGoldMenu(m));
        yield return ("withdraw gold", (u, m) => u.ShowWithdrawGoldMenu(m));
        yield return ("deposit item", (u, m) => u.ShowDepositItemMenu(m));
        yield return ("deposit item quantity", (u, m) => u.ShowDepositItemQuantity(m, 1));
        yield return ("withdraw item", (u, m) => u.ShowWithdrawItemMenu(m));
        yield return ("repair item", (u, m) => u.ShowRepairItemMenu(m));
        yield return ("repair one", (u, m) => u.ShowRepairItem(m, 1));
        yield return ("repair all", (u, m) => u.ShowRepairAllItems(m));
    }

    /// <summary>
    ///     Give the user a stackable item in slot 1, so the flows that offer a quantity prompt for a
    ///     held item reach their menu rather than returning early.
    /// </summary>
    private void GiveStackableItem()
    {
        Assert.True(Game.World.WorldData.TryGetValueByIndex("Sausages", out Item sausages),
            "Couldn't find sausages in test items");
        var stack = new ItemObject(sausages, Fixture.TestUser.World.Guid) { Count = 5 };
        Assert.True(Fixture.TestUser.AddItem(stack), "Couldn't add item to inventory");
    }

    [Fact]
    public void NoMerchantMenuOffersAnItemItsCallbackCannotParse()
    {
        Fixture.ResetTestUserStats();
        var user = Fixture.TestUser;
        var merchant = GetTestMerchant();
        var client = HybrasylFixture.AttachTestClient(user, out var restore);

        var mismatches = new List<string>();
        var reached = new HashSet<NpcMenuType>();

        try
        {
            GiveStackableItem();
            user.Stats.Gold = 1000;

            foreach (var (label, show) in Flows())
            {
                while (client.ClientState.SendBufferTake(out _)) { }

                show(user, merchant);

                while (client.ClientState.SendBufferTake(out var sent))
                {
                    if (sent.Packet is not NpcMenuPacket menu) continue;
                    reached.Add(menu.MenuType);
                    mismatches.AddRange(
                        MerchantResponseFormCheck.Mismatches(menu, Game.World.MerchantMenuHandlers)
                            .Select(selector: m => $"{label}: {m}"));
                }
            }
        }
        finally
        {
            restore.Dispose();
        }

        Assert.Empty(mismatches);
        Assert.Empty(MenuTypesTheSweepMustReach.Except(reached));
    }

    /// <summary>
    ///     The mismatch the sweep exists to catch, stated directly: an item whose callback reads an
    ///     option byte, offered under a menu type whose reply carries a name instead.
    /// </summary>
    [Fact]
    public void AnItemOfferedUnderTheWrongMenuTypeIsReported()
    {
        var menu = new NpcMenuPacket
        {
            MenuType = NpcMenuType.ItemList,
            Menu = new ItemListMenu { PursuitId = (ushort) MerchantMenuItem.RepairItem, Items = [] }
        };

        var mismatch = Assert.Single(MerchantResponseFormCheck.Mismatches(menu, Game.World.MerchantMenuHandlers));
        Assert.Contains("RepairItem", mismatch);
    }

    /// <summary>
    ///     A menu shape the check cannot read is reported rather than contributing no pursuits and
    ///     so passing — the reading that would make a newly-emitted shape silently exempt.
    /// </summary>
    [Fact]
    public void AMenuShapeTheCheckCannotReadIsReported()
    {
        var menu = new NpcMenuPacket
        {
            MenuType = NpcMenuType.OptionsWithArgument,
            Menu = new OptionsWithArgumentMenu { Argument = "arg", Options = [] }
        };

        Assert.Single(MerchantResponseFormCheck.Mismatches(menu, Game.World.MerchantMenuHandlers));
    }

    /// <summary>
    ///     That the send path <em>runs</em> the cross-check, not merely that the cross-check works.
    /// </summary>
    /// <remarks>
    ///     Every other test here calls <see cref="MerchantResponseFormCheck" /> itself, so deleting
    ///     the call in <c>User.Enqueue</c> leaves them all green — and the flows this sweep cannot
    ///     reach have no guard but that call. Enqueuing a deliberately mismatched menu and reading
    ///     the log back is what makes the invocation load-bearing.
    /// </remarks>
    [Fact]
    public void TheSendPathRunsTheCrossCheck()
    {
        var user = Fixture.TestUser;
        var client = HybrasylFixture.AttachTestClient(user, out var restore);
        var sink = new CapturingSink();
        GameLog.Loggers.TryGetValue(LogType.General, out var previous);

        try
        {
            GameLog.Loggers[LogType.General] = new HybrasylLogger
            {
                Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger()
            };

            user.Enqueue(new NpcMenuPacket
            {
                MenuType = NpcMenuType.ItemList,
                Menu = new ItemListMenu { PursuitId = (ushort) MerchantMenuItem.RepairItem, Items = [] }
            });

            Assert.Contains(sink.Messages, filter: m => m.Contains("RepairItem"));
        }
        finally
        {
            if (previous is null) GameLog.Loggers.Remove(LogType.General);
            else GameLog.Loggers[LogType.General] = previous;
            while (client.ClientState.SendBufferTake(out _)) { }
            restore.Dispose();
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<string> Messages { get; } = [];

        public void Emit(LogEvent logEvent) => Messages.Add(logEvent.RenderMessage());
    }

    /// <summary>
    ///     Script pursuits share the options menu with merchant items and are not merchant items;
    ///     the check must split them the same way the 0x39 handler's dispatch does, or every
    ///     <c>DisplayPursuits</c> would report as unregistered.
    /// </summary>
    [Fact]
    public void WorldDataPursuitIdsAreNotTreatedAsMerchantItems()
    {
        var menu = new NpcMenuPacket
        {
            MenuType = NpcMenuType.Options,
            Menu = new OptionsMenu { Options = [new NpcMenuOption("a scripted pursuit", 1)] }
        };

        Assert.Empty(MerchantResponseFormCheck.Mismatches(menu, Game.World.MerchantMenuHandlers));
    }
}
