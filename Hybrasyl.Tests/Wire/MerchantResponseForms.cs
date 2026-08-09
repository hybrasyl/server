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

using Hybrasyl.Objects;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     HS-1577 — that every merchant menu callback parses the C&#8594;S 0x39 response form its menu
///     type actually implies.
/// </summary>
/// <remarks>
///     <para>
///         The 0x39 tail is not self-describing: its shape depends on the menu the server last
///         displayed, not on any byte in the packet. DALib therefore binds 0x39 to the bare
///         <c>NpcMainMenuSelectPacket</c> and drops the tail, and each registration declares the
///         form it expects. That declaration and the menu type the send side offers the item under
///         are <strong>two independent statements of the same fact, living in different files</strong>,
///         so diffing them is free evidence.
///     </para>
///     <para>
///         This has already gone wrong once. this delta's live regression —
///         <c>MerchantMenuHandler_BuyItemWithQuantity</c> reading one <c>string8</c> too many —
///         was exactly this failure class, and it survived because the legacy in-place dialog
///         transform left slack in the buffer, so the over-read got a <c>0x00</c> length byte back
///         and quietly succeeded.
///     </para>
///     <para>
///         <strong>Where the expectations come from, and why not from the handlers.</strong> Every
///         entry in <see cref="ExpectedForms" /> was read off the <em>send</em> site in
///         <c>User.cs</c> — which <c>MerchantMenu</c> overload offers that item as its
///         <c>Id</c> — and then mapped to a form through the protocol reference's
///         <c>docs/protocol/client/0x39-npc-main-menu.md</c> §"Response tail forms", which is
///         Ghidra-verified from the retail client's eleven C&#8594;S 0x39 emitters. Deriving them
///         from Hybrasyl's own handler bodies instead would be the exact circularity this test
///         exists to break: the table would agree with the code by construction and could not fail
///         for the reason it is here to catch.
///     </para>
///     <para>
///         <strong>Names do not settle it; only the offering site does.</strong>
///         <c>SellItemQuantity</c>, <c>SendParcelQuantity</c> and <c>DepositItemQuantity</c> all
///         read an <em>option</em> despite their names, because they are reached from inventory
///         lists where the option <em>is</em> the slot. A scrape that trusted names would have
///         enshrined three wrong pairings in the very guard meant to prevent them.
///     </para>
///     <para>
///         <strong>What this does not cover.</strong> It pins the receive side against the
///         protocol. It does not execute the send side, so it cannot catch a
///         <c>Show…Menu</c> being changed to offer an item under a different menu type — the
///         expectation table would then be stale in a way only re-reading <c>User.cs</c> reveals.
///         That link remains the hand audit recorded on HS-1577.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class MerchantResponseForms
{
    public MerchantResponseForms(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    /// <summary>
    ///     Item &#8594; the form its menu type implies. Derived from the send site in
    ///     <c>User.cs</c>, never from the handler bodies. The comment on each group names the
    ///     <c>MerchantMenu</c> overload that offers those items.
    /// </summary>
    public static readonly Dictionary<MerchantMenuItem, MerchantResponseForm> ExpectedForms = new()
    {
        // MerchantInput -> NpcMenuType.TextEntry -> form B. A typed reply.
        { MerchantMenuItem.BuyItemAccept, MerchantResponseForm.Text },
        { MerchantMenuItem.SellItem, MerchantResponseForm.Text },
        { MerchantMenuItem.SendParcelRecipient, MerchantResponseForm.Text },
        { MerchantMenuItem.SendParcelAccept, MerchantResponseForm.Text },
        { MerchantMenuItem.DepositGoldQuantity, MerchantResponseForm.Text },
        { MerchantMenuItem.WithdrawGoldQuantity, MerchantResponseForm.Text },
        { MerchantMenuItem.DepositItem, MerchantResponseForm.Text },
        { MerchantMenuItem.WithdrawItem, MerchantResponseForm.Text },

        // MerchantShopItems -> NpcMenuType.ItemList -> form B. The reply is the item's name.
        { MerchantMenuItem.BuyItemQuantity, MerchantResponseForm.Text },
        { MerchantMenuItem.WithdrawItemQuantity, MerchantResponseForm.Text },

        // MerchantSkills/MerchantSpells -> Skill/SpellList -> form B. The reply is the castable's name.
        { MerchantMenuItem.LearnSkill, MerchantResponseForm.Text },
        { MerchantMenuItem.LearnSpell, MerchantResponseForm.Text },

        // UserInventoryItems -> NpcMenuType.PlayerItemList -> form E. The option is the slot.
        { MerchantMenuItem.SellItemQuantity, MerchantResponseForm.Option },
        { MerchantMenuItem.SendParcelQuantity, MerchantResponseForm.Option },
        { MerchantMenuItem.DepositItemQuantity, MerchantResponseForm.Option },
        { MerchantMenuItem.RepairItem, MerchantResponseForm.Option },

        // UserSkillBook/UserSpellBook -> PlayerSkill/SpellList -> form E. The option is the slot.
        { MerchantMenuItem.ForgetSkillAccept, MerchantResponseForm.Option },
        { MerchantMenuItem.ForgetSpellAccept, MerchantResponseForm.Option },

        // MerchantOptions -> NpcMenuType.Options -> form A. The prefix carries the choice.
        { MerchantMenuItem.LearnSkillAgree, MerchantResponseForm.Select },
        { MerchantMenuItem.LearnSkillDisagree, MerchantResponseForm.Select },
        { MerchantMenuItem.LearnSkillAccept, MerchantResponseForm.Select },
        { MerchantMenuItem.LearnSpellAgree, MerchantResponseForm.Select },
        { MerchantMenuItem.LearnSpellDisagree, MerchantResponseForm.Select },
        { MerchantMenuItem.LearnSpellAccept, MerchantResponseForm.Select },
        { MerchantMenuItem.SellItemAccept, MerchantResponseForm.Select },
        { MerchantMenuItem.RepairItemAccept, MerchantResponseForm.Select },
        { MerchantMenuItem.RepairAllItemsAccept, MerchantResponseForm.Select },
        { MerchantMenuItem.MainMenu, MerchantResponseForm.Select },

        // Merchant pursuit entries — offered from the merchant's own options menu rather than from
        // User.cs, so likewise form A.
        { MerchantMenuItem.BuyItemMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.SellItemMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.LearnSkillMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.LearnSpellMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.ForgetSkillMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.ForgetSpellMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.SendParcelMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.ReceiveParcel, MerchantResponseForm.Select },
        { MerchantMenuItem.WithdrawItemMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.WithdrawGoldMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.DepositGoldMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.DepositItemMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.RepairItemMenu, MerchantResponseForm.Select },
        { MerchantMenuItem.RepairAllItems, MerchantResponseForm.Select },

        // Registered with empty bodies and no offering site anywhere. Declared Select because that
        // is the reading that consumes nothing; if one of these is ever implemented, its form must
        // be re-derived from the menu it ends up offered under.
        { MerchantMenuItem.ForgetSkill, MerchantResponseForm.Select },
        { MerchantMenuItem.ForgetSpell, MerchantResponseForm.Select },
        { MerchantMenuItem.SendParcel, MerchantResponseForm.Select },
        { MerchantMenuItem.SendParcelFailure, MerchantResponseForm.Select }
    };

    /// <summary>
    ///     The pairing itself: each registered callback parses what its menu type implies.
    /// </summary>
    [Fact]
    public void EveryRegisteredCallbackParsesItsMenuTypesForm()
    {
        var mismatches = Game.World.MerchantMenuHandlers
            .Where(predicate: kv => ExpectedForms.TryGetValue(kv.Key, out var expected) && kv.Value.Form != expected)
            .Select(selector: kv => $"{kv.Key}: registered {kv.Value.Form}, menu implies {ExpectedForms[kv.Key]}")
            .ToList();

        Assert.Empty(mismatches);
    }

    /// <summary>
    ///     Completeness in the direction that matters: a callback added without deciding its form
    ///     fails here rather than silently misparsing in production. Without this, the pairing test
    ///     above would pass vacuously for anything missing from the table.
    /// </summary>
    [Fact]
    public void EveryRegisteredItemDeclaresAnExpectedForm()
    {
        var undeclared = Game.World.MerchantMenuHandlers.Keys
            .Where(predicate: item => !ExpectedForms.ContainsKey(item))
            .ToList();

        Assert.Empty(undeclared);
    }

    /// <summary>
    ///     And the other direction, so the table cannot rot: an entry for an item nobody registers
    ///     is either a stale row or a registration that got dropped.
    /// </summary>
    [Fact]
    public void EveryExpectedFormHasARegistration()
    {
        var unregistered = ExpectedForms.Keys
            .Where(predicate: item => !Game.World.MerchantMenuHandlers.ContainsKey(item))
            .ToList();

        Assert.Empty(unregistered);
    }

    /// <summary>
    ///     Pins the denominator. HS-1577 was written against "47 pairings", which counted the
    ///     commented-out <c>BuyItem</c> line; there are 46 live registrations. A count that drifts
    ///     silently is how an audit's completeness claim goes stale.
    /// </summary>
    [Fact]
    public void TheRegistrationCountIsWhatTheAuditCovered()
    {
        Assert.Equal(46, Game.World.MerchantMenuHandlers.Count);
    }
}
