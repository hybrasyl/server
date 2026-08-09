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

using DALib.Networking.Packets.Client;
using Hybrasyl.Networking;
using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Xml.Objects;
using System;
using System.Buffers.Binary;
using System.Linq;
using Xunit;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     The handler-side guards that survive a <em>parse</em> the receive-path tests already cover.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="MovementAndCombatReceivePath" /> and <see cref="DialogResponseVariants" /> pin
///         what DALib hands back for a given body. Neither invokes a handler, so both were green
///         with the guard that consumes the parse result deleted — the decision each of those
///         parses exists to feed was owned by nothing.
///     </para>
///     <para>
///         These drive the real registered handler through
///         <c>WorldPacketHandlers</c>, the way <see cref="MerchantDispatchWiring" /> does, and
///         assert the side effect the guard produces rather than the shape DALib returned.
///     </para>
/// </remarks>
[Collection("Hybrasyl")]
public class ReceivePathHandlerGuards
{
    public ReceivePathHandlerGuards(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    private HybrasylFixture Fixture { get; }

    private const string Sentinel = "no dialog callback ran";

    private static void Dispatch(User user, byte opcode, byte[] body) =>
        Game.World.WorldPacketHandlers[opcode].Invoke(user, new InboundPacket(opcode, body));

    /// <summary>
    ///     0x0F with no target tail. The client sends the bare slot byte for a self/group cast, and
    ///     the handler's <c>Args.Length >= 4</c> check is the only thing standing between that and a
    ///     read off an empty span.
    /// </summary>
    /// <remarks>
    ///     The legacy positional read threw <c>IndexOutOfRangeException</c> here and the queue
    ///     consumer swallowed it, so the cast silently never happened and <c>Casting</c> stayed set.
    ///     Clearing it is what lets the next packet through the consumer's
    ///     <c>user.Condition.Casting</c> cancel branch.
    /// </remarks>
    [Fact]
    public void BareCast_CompletesAndClearsCasting()
    {
        Fixture.ResetTestUserStats();
        var slot = Fixture.TestUser.SpellBook.FindEmptyPrimarySlot();
        Assert.Null(Fixture.TestUser.SpellBook[slot]);

        Fixture.TestUser.Condition.Casting = true;

        Dispatch(Fixture.TestUser, 0x0F, [slot]);

        Assert.False(Fixture.TestUser.Condition.Casting);
    }

    /// <summary>
    ///     0x0F with a target tail, asserted on the <em>damage</em> and not on the parse: a handler
    ///     that dropped the serial and passed 0 would leave the monster untouched, because a
    ///     <see cref="SpellUseType.Target" /> intent returns early without a resolvable target.
    /// </summary>
    /// <remarks>
    ///     This is the assertion direction <see cref="BareCast_CompletesAndClearsCasting" /> cannot
    ///     reach. Clearing <c>Casting</c> happens either way, so on its own the bare case is
    ///     satisfied by a handler that never reads <c>Args</c> at all.
    /// </remarks>
    [Fact]
    public void TargetedCast_TheSerialInArgsReachesUseSpell()
    {
        Fixture.ResetTestUserStats();
        // ard srad costs 2530mp; the reset leaves 1000, and UseCastable would refuse before the
        // target ever mattered.
        Fixture.TestUser.Stats.BaseMp = 10000;
        Fixture.TestUser.Stats.Mp = 10000;

        var castable = Game.World.WorldData.GetByIndex<Castable>("ard srad");
        Assert.NotNull(castable);
        Assert.Equal(SpellUseType.Target, castable.Intents[0].UseType);

        var template = Game.World.WorldData.Get<Xml.Objects.Creature>("Honey Bee");
        Assert.NotNull(template);

        var bait = new Monster(template, SpawnFlags.AiDisabled, 99)
        {
            Stats = { BaseHp = 50000, Hp = 50000 },
            Name = "Cast Target Bait",
            X = (byte) (Fixture.TestUser.X - 1),
            Y = Fixture.TestUser.Y
        };

        var map = Fixture.TestUser.Location.Map!;
        Game.World.Insert(bait);
        map.Insert(bait, bait.X, bait.Y);

        try
        {
            Assert.True(Fixture.TestUser.SpellBook.Add(castable), "couldn't add ard srad to the spellbook");
            var slot = Fixture.TestUser.SpellBook.SlotOf(castable.Name);

            // [u8 slot][u32 BE target][u16 BE x][u16 BE y] — hand-assembled from the layout, so a
            // wrong understanding of it fails here rather than agreeing with DALib's writer.
            var body = new byte[9];
            body[0] = slot;
            BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(1), bait.Id);
            BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(5), bait.X);
            BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(7), bait.Y);

            Dispatch(Fixture.TestUser, 0x0F, body);

            Assert.True(bait.Stats.Hp < 50000,
                "the cast never reached the monster: the handler did not read the target serial out " +
                $"of Args. Last system message: '{Fixture.TestUser.LastSystemMessage}'");
            Assert.False(Fixture.TestUser.Condition.Casting);
        }
        finally
        {
            Fixture.TestUser.SpellBook.Remove(Fixture.TestUser.SpellBook.SlotOf(castable.Name));
            map.Remove(bait);
            Game.World.Remove(bait);
        }
    }

    /// <summary>
    ///     0x3A arriving in the wrong response shape for the dialog the server has open: an options
    ///     dialog is active and the client submits text.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The legacy positional read had no way to see this — it took the byte after the prefix
    ///         as the option index and dispatched on whatever it happened to be. The converted
    ///         handler branches on DALib's discriminated variant, refuses the mismatch, and clears
    ///         dialog state.
    ///     </para>
    ///     <para>
    ///         The two assertions are separable and both are needed. State-cleared alone is also
    ///         produced by a fall-through whose callback fails, and callback-suppressed alone is
    ///         produced by a handler that ignored the packet entirely. The option carries a
    ///         <see cref="JumpDialog" /> to a sequence that does not exist, so running it is loud —
    ///         it replaces the sentinel with the scripting-error message — while the guarded path
    ///         sends no system message at all.
    ///     </para>
    /// </remarks>
    [Fact]
    public void OptionsDialogActive_TextResponseIsRefusedWithoutRunningTheCallback()
    {
        Fixture.ResetTestUserStats();

        var merchant = Game.World.Objects.Values.OfType<Merchant>().FirstOrDefault(x => x.Name == "Maria");
        Assert.NotNull(merchant);

        var options = new OptionsDialog("Which one?");
        options.AddDialogOption(new DialogOption
        {
            OptionText = "The first one",
            JumpDialog = new JumpDialog("no-such-sequence")
        });

        var pursuitId = (uint) Game.ActiveConfiguration.Constants.DialogSequenceShared + 1;
        var sequence = new DialogSequence("MismatchedResponseProbe") { Id = pursuitId };
        sequence.AddDialog(options);

        Fixture.TestUser.DialogState.EndDialog();
        Assert.True(Fixture.TestUser.DialogState.StartDialog(merchant, sequence), "couldn't start the probe dialog");
        Assert.True(Fixture.TestUser.DialogState.InDialog);
        Assert.Equal(0, Fixture.TestUser.DialogState.CurrentPursuitIndex);

        Fixture.TestUser.SendSystemMessage(Sentinel);

        try
        {
            // Prefix [u8 objectType][u32 BE objectId][u16 BE pursuitId][u16 BE pursuitIndex], then
            // the text tail [0x02][u8 len][latin-1]. Index is current+1 so this reads as an advance
            // rather than a close (same id and index) or a prev (index-1).
            var body = new byte[9];
            body[0] = DialogUsePacket.ObjectTypeCreature;
            BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(1), merchant.Id);
            BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(5), (ushort) pursuitId);
            BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(7), 1);

            Dispatch(Fixture.TestUser, 0x3A, [.. body, DialogUsePacket.TagText, 0x02, .. "no"u8]);

            Assert.False(Fixture.TestUser.DialogState.InDialog,
                "the mismatched response left dialog state intact");
            Assert.Equal(Sentinel, Fixture.TestUser.LastSystemMessage);
        }
        finally
        {
            Fixture.TestUser.DialogState.EndDialog();
        }
    }
}
