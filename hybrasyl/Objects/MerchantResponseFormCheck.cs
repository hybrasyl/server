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
using System.Collections.Generic;

namespace Hybrasyl.Objects;

/// <summary>
///     Reports merchant menu items that an outgoing S&#8594;C 0x2F menu offers under a menu type whose
///     C&#8594;S 0x39 reply the item's registered callback does not parse.
/// </summary>
/// <remarks>
///     <para>
///         The send side fixes a menu's type; the receive side declares the form its callback parses
///         (<see cref="MerchantMenuHandler.Form" />). Those are two independent statements about the
///         same reply, and the client resolves the disagreement by sending a tail the handler will
///         misread — silently, because 0x39 carries no discriminator.
///     </para>
///     <para>
///         Running this against the packet rather than against a table is the point. A table of
///         item&#8594;form pairings goes stale the moment a <c>Show…Menu</c> offers an item under a
///         different menu type, and nothing says so; reading the menu type off the packet on its way
///         to the wire cannot go stale, and covers emit sites that build their menu inline.
///     </para>
///     <para>
///         <strong>Provenance of the type&#8594;form mapping.</strong> The menu type selects the client's
///         dialog class (Ghidra RTTI, the protocol reference at
///         <c>023d886</c>): types 0/1 &#8594; <c>NPC_Merchant_TextMenu</c>, 2/3 &#8594;
///         <c>NPC_Merchant_TextInputMenu</c>, 4/10 &#8594; <c>NPCServerItemMenu</c>, 5/11 &#8594;
///         <c>NPCClientItemMenu</c>, 6/7 &#8594; <c>NPCServerSkillSpellMenu</c>, 8/9 &#8594;
///         <c>NPCClientSpellMenu</c>/<c>NPCClientSkillMenu</c>. The <em>Server</em>/<em>Client</em>
///         split is what decides the form: a Server* class lists the NPC's own catalog and echoes the
///         chosen row's name (form B), a Client* class lists what the player owns and echoes the slot
///         (form E). Forms and their tails are from
///         <c>docs/protocol/client/0x39-npc-main-menu.md</c> at <c>3230079</c>, which classifies all
///         twenty-one <c>6a 39</c> emit sites in the retail binary.
///     </para>
/// </remarks>
internal static class MerchantResponseFormCheck
{
    /// <summary>
    ///     The 0x39 response form a menu of this type produces, or <c>null</c> for a type no
    ///     Hybrasyl callback shape covers.
    /// </summary>
    /// <remarks>
    ///     The <c>*WithArgument</c> types are unmapped deliberately: retail wraps their option byte in
    ///     <c>0x01</c> markers, which none of the three callback shapes reads. The two alias types are
    ///     mapped because the client treats them as their originals.
    /// </remarks>
    public static MerchantResponseForm? ResponseFormFor(NpcMenuType menuType) => menuType switch
    {
        NpcMenuType.Options => MerchantResponseForm.Select,
        NpcMenuType.TextEntry => MerchantResponseForm.Text,
        NpcMenuType.ItemList or NpcMenuType.ItemListAlternate => MerchantResponseForm.Text,
        NpcMenuType.SpellList or NpcMenuType.SkillList => MerchantResponseForm.Text,
        NpcMenuType.PlayerItemList or NpcMenuType.PlayerItemListAlternate => MerchantResponseForm.Option,
        NpcMenuType.PlayerSpellList or NpcMenuType.PlayerSkillList => MerchantResponseForm.Option,
        _ => null
    };

    /// <summary>
    ///     Every way this menu's reply could be misparsed, one description each. Empty means the menu
    ///     is consistent with the registrations.
    /// </summary>
    public static IReadOnlyList<string> Mismatches(NpcMenuPacket packet,
        IReadOnlyDictionary<MerchantMenuItem, MerchantMenuHandler> handlers)
    {
        var offered = OfferedPursuits(packet.Menu);

        // A shape this method cannot read would otherwise contribute no pursuits and so pass, which
        // is the reading that makes a new menu shape silently exempt.
        if (offered is null)
            return [$"menu body {packet.Menu.GetType().Name} is a shape this check cannot read"];

        var expected = ResponseFormFor(packet.MenuType);
        var mismatches = new List<string>();

        foreach (var pursuitId in offered)
        {
            // The same discriminator the 0x39 handler dispatches on, so the two cannot disagree
            // about which ids are merchant items rather than world-data pursuits.
            if (pursuitId < Game.ActiveConfiguration.Constants.DialogSequenceHardcoded) continue;

            var item = (MerchantMenuItem) pursuitId;

            if (!handlers.TryGetValue(item, out var handler))
                mismatches.Add($"{item} offered under {packet.MenuType} has no registered handler");
            else if (expected is null)
                mismatches.Add($"{item} offered under {packet.MenuType}, whose 0x39 reply form is unknown");
            else if (handler.Form != expected)
                mismatches.Add(
                    $"{item} offered under {packet.MenuType} replies in form {expected}, but its handler parses {handler.Form}");
        }

        return mismatches;
    }

    /// <summary>
    ///     The pursuit ids a menu body offers, or <c>null</c> if the body is not one of the shapes
    ///     Hybrasyl emits.
    /// </summary>
    private static List<ushort>? OfferedPursuits(NpcMenu menu)
    {
        switch (menu)
        {
            case OptionsMenu options:
                var pursuits = new List<ushort>(options.Options.Count);
                foreach (var option in options.Options) pursuits.Add(option.Pursuit);
                return pursuits;
            case TextEntryMenu entry: return [entry.PursuitId];
            case ItemListMenu items: return [items.PursuitId];
            case PlayerItemListMenu items: return [items.PursuitId];
            case SpellListMenu spells: return [spells.PursuitId];
            case SkillListMenu skills: return [skills.PursuitId];
            case PlayerSpellListMenu book: return [book.PursuitId];
            case PlayerSkillListMenu book: return [book.PursuitId];
            default: return null;
        }
    }
}
