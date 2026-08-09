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

using System.Collections.Generic;
using DALib.Enums;
using DALib.Networking.Packets.Server;
using DALib.Networking.Wire;
using Xunit;
using LegacyServerPacket = Hybrasyl.Tests.Wire.LegacyBodyWriter;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Inventory, equipment, stats and skill/spell pane send-path coverage.
///     MATCH opcodes are pinned byte-identical against the verbatim pre-conversion emit;
///     slack-family opcodes (0x0F trailing u32, 0x10 trailing 3 bytes) assert the
///     typed body equals the legacy emit minus exactly the signed-off slack bytes.
/// </summary>
public class PanesAndStatsSendPath
{
    private static byte[] Body(DALib.Networking.Wire.ServerPacket record)
    {
        var writer = new PacketWriter();
        record.WriteBody(writer);
        return writer.WrittenSpan.ToArray();
    }

    // --- MATCH: byte-identical conversions ---

    [Fact]
    public void AddSkill_MatchesLegacyBody()
    {
        // 0x2C: Slot, Icon u16, String8(composed name incl. mastery suffix)
        const string name = "Cleave (Lev:50/100)";
        var legacy = new LegacyServerPacket(0x2C);
        legacy.WriteByte(5);
        legacy.WriteUInt16(0x1234);
        legacy.WriteString8(name);

        var typed = Body(new AddSkillPacket { Slot = 5, Icon = 0x1234, Name = name });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void RemoveSkill_MatchesLegacyBody()
    {
        // 0x2D: Slot only
        var legacy = new LegacyServerPacket(0x2D);
        legacy.WriteByte(7);

        var typed = Body(new RemoveSkillPacket { Slot = 7 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void RemoveSpell_MatchesLegacyBody()
    {
        // 0x18: Slot only
        var legacy = new LegacyServerPacket(0x18);
        legacy.WriteByte(9);

        var typed = Body(new RemoveSpellPacket { Slot = 9 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void AddSpell_MatchesLegacyBody_PreservesNullPrompt()
    {
        // 0x17: Slot, Icon u16, UseType u8, String8(Name), String8(Prompt="\0"), CastLines u8.
        // Prompt="\0" is preserved (2 wire bytes: len=1, 0x00); dropping it would shift CastLines.
        const string name = "Fireball";
        var legacy = new LegacyServerPacket(0x17);
        legacy.WriteByte(3);
        legacy.WriteUInt16(0x00AB); // Hybrasyl AddSpell.Icon was a byte; high byte 0x00
        legacy.WriteByte(2);        // UseType (Target)
        legacy.WriteString8(name);
        legacy.WriteString8("\0");
        legacy.WriteByte(4);        // CastLines

        var typed = Body(new AddSpellPacket
        {
            Slot = 3,
            Icon = 0x00AB,
            UseType = (SpellUseType)2,
            Name = name,
            Prompt = "\0",
            CastLines = 4
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void AddEquipment_MatchesLegacyBody()
    {
        // 0x37: Slot, Sprite u16 (+0x8000 applied by caller), Color, StringWithLength(Name)==String8,
        // 0x00 (Unknown1), MaxDur u32, CurDur u32.
        const string name = "Claidheamh";
        var legacy = new LegacyServerPacket(0x37);
        legacy.WriteByte((byte)EquipmentSlot.Weapon);
        legacy.WriteUInt16(0x8042);
        legacy.WriteByte(3);
        legacy.WriteStringWithLength(name);
        legacy.WriteByte(0x00);
        legacy.WriteUInt32(1000);
        legacy.WriteUInt32(500);

        var typed = Body(new AddEquipmentPacket
        {
            Slot = EquipmentSlot.Weapon,
            Sprite = 0x8042,
            Color = 3,
            Name = name,
            MaxDurability = 1000,
            CurrentDurability = 500
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    // --- Slack family: typed body == legacy minus exactly the signed-off slack bytes ---

    [Fact]
    public void AddItem_MatchesCanonicalNoTrailingForm()
    {
        // 0x0F: Slot, Sprite u16 (+0x8000), Color, String8(Name), Count u32, Stackable bool,
        // MaxDur u32, CurDur u32. This is the no-trailing form emitted by SendInventorySlot /
        // SendInventory; SendItemUpdate is normalized to this too.
        const string name = "Potion";
        var legacy = new LegacyServerPacket(0x0F);
        legacy.WriteByte(4);
        legacy.WriteUInt16(0x8042);
        legacy.WriteByte(0);
        legacy.WriteString8(name);
        legacy.WriteInt32(7);
        legacy.WriteBoolean(true);
        legacy.WriteUInt32(255);
        legacy.WriteUInt32(255);

        var typed = Body(new AddItemPacket
        {
            Slot = 4,
            Sprite = 0x8042,
            Color = 0,
            Name = name,
            Count = 7,
            Stackable = true,
            MaxDurability = 255,
            CurrentDurability = 255
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void AddItem_DropsW21TrailingSlack()
    {
        // SendItemUpdate's legacy form appended a trailing u32(0) (marked //?).
        // The typed body must equal that legacy emit minus exactly the trailing 4 bytes.
        const string name = "Potion";
        var legacyWithSlack = new LegacyServerPacket(0x0F);
        legacyWithSlack.WriteByte(4);
        legacyWithSlack.WriteUInt16(0x8042);
        legacyWithSlack.WriteByte(0);
        legacyWithSlack.WriteString8(name);
        legacyWithSlack.WriteInt32(7);
        legacyWithSlack.WriteBoolean(true);
        legacyWithSlack.WriteUInt32(255);
        legacyWithSlack.WriteUInt32(255);
        legacyWithSlack.WriteUInt32(0x00); // the dropped trailing slack

        var typed = Body(new AddItemPacket
        {
            Slot = 4,
            Sprite = 0x8042,
            Color = 0,
            Name = name,
            Count = 7,
            Stackable = true,
            MaxDurability = 255,
            CurrentDurability = 255
        });

        var legacyBytes = legacyWithSlack.BodyMemory.ToArray();
        Assert.Equal(legacyBytes.Length - 4, typed.Length);
        Assert.Equal(legacyBytes[..^4], typed);
    }

    [Fact]
    public void RemoveItem_DropsW22TrailingSlack()
    {
        // SendClearItem's legacy form emitted Slot + u16(0) + u8(0) = 3 trailing slack bytes.
        // The typed body must equal that legacy emit minus exactly the trailing 3 bytes (Slot only).
        var legacyWithSlack = new LegacyServerPacket(0x10);
        legacyWithSlack.WriteByte(4);
        legacyWithSlack.WriteUInt16(0x0000);
        legacyWithSlack.WriteByte(0x00);

        var typed = Body(new RemoveItemPacket { Slot = 4 });

        var legacyBytes = legacyWithSlack.BodyMemory.ToArray();
        Assert.Equal(legacyBytes.Length - 3, typed.Length);
        Assert.Equal(legacyBytes[..^3], typed);
    }

    // --- 0x08 Attributes: byte-identical across flag/section combinations. The legacy emit wrote
    //     (byte)flags then each section under `if (flags.HasFlag(...))`; the typed record re-derives
    //     the flag byte from populated sections + standalone bits, so these pin that they agree. ---

    [Fact]
    public void Attributes_AllSections_MatchesLegacyBody()
    {
        // flags = Primary|Current|Experience|Secondary = 0x3C
        var legacy = new LegacyServerPacket(0x08);
        legacy.WriteByte(0x3C);
        // Primary
        legacy.Write(new byte[] { 1, 0, 0 });
        legacy.WriteByte(50);   // Level
        legacy.WriteByte(10);   // Ability
        legacy.WriteUInt32(1000);
        legacy.WriteUInt32(500);
        legacy.WriteByte(20);
        legacy.WriteByte(15);
        legacy.WriteByte(12);
        legacy.WriteByte(18);
        legacy.WriteByte(14);
        legacy.WriteByte(1);    // LevelPoints > 0
        legacy.WriteByte(3);
        legacy.WriteUInt16(100);
        legacy.WriteUInt16(45);
        legacy.WriteUInt32(uint.MinValue);
        // Current
        legacy.WriteUInt32(800);
        legacy.WriteUInt32(300);
        // Experience
        legacy.WriteUInt32(123456);
        legacy.WriteUInt32(200000);
        legacy.WriteUInt32(5000);
        legacy.WriteUInt32(0);
        legacy.WriteUInt32(0);
        legacy.WriteUInt32(9999);
        // Secondary
        legacy.WriteByte(0);
        legacy.WriteByte(0x08); // Blinded
        legacy.WriteByte(0);
        legacy.WriteByte(0);
        legacy.WriteByte(0);
        legacy.WriteByte(1);    // MailStatus
        legacy.WriteByte(2);    // OffElem
        legacy.WriteByte(3);    // DefElem
        legacy.WriteByte(7);    // MrRating
        legacy.WriteByte(0);    // fast move
        legacy.WriteSByte(-5);  // Ac
        legacy.WriteByte(4);    // DmgRating
        legacy.WriteByte(6);    // HitRating

        var typed = Body(new AttributesPacket
        {
            Primary = new PrimaryAttributes
            {
                Level = 50, Ability = 10, MaxHp = 1000, MaxMp = 500,
                Str = 20, Int = 15, Wis = 12, Con = 18, Dex = 14,
                UnspentPoints = 3, MaxWeight = 100, CurrentWeight = 45
            },
            Current = new CurrentAttributes { Hp = 800, Mp = 300 },
            Experience = new ExperienceAttributes
            {
                Experience = 123456, ExpToLevel = 200000, AbilityExp = 5000, Gold = 9999
            },
            Secondary = new SecondaryAttributes
            {
                Blinded = 0x08, MailStatus = 1, OffensiveElement = 2, DefensiveElement = 3,
                MrRating = 7, Ac = -5, DmgRating = 4, HitRating = 6
            }
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void Attributes_CurrentOnly_MatchesLegacyBody()
    {
        // flags = Current = 0x10 (narrow update)
        var legacy = new LegacyServerPacket(0x08);
        legacy.WriteByte(0x10);
        legacy.WriteUInt32(800);
        legacy.WriteUInt32(300);

        var typed = Body(new AttributesPacket { Current = new CurrentAttributes { Hp = 800, Mp = 300 } });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void Attributes_PrimaryWithGameMasterA_MapsToMovementMode()
    {
        // flags = Primary|GameMasterA = 0x60; GameMasterA (bit 6) must survive as MovementMode=1.
        // LevelPoints == 0 exercises the zero-unspent branch.
        var legacy = new LegacyServerPacket(0x08);
        legacy.WriteByte(0x60);
        legacy.Write(new byte[] { 1, 0, 0 });
        legacy.WriteByte(50);
        legacy.WriteByte(10);
        legacy.WriteUInt32(1000);
        legacy.WriteUInt32(500);
        legacy.WriteByte(20);
        legacy.WriteByte(15);
        legacy.WriteByte(12);
        legacy.WriteByte(18);
        legacy.WriteByte(14);
        legacy.WriteByte(0);    // LevelPoints == 0
        legacy.WriteByte(0);
        legacy.WriteUInt16(100);
        legacy.WriteUInt16(45);
        legacy.WriteUInt32(uint.MinValue);

        var typed = Body(new AttributesPacket
        {
            MovementMode = 1,
            Primary = new PrimaryAttributes
            {
                Level = 50, Ability = 10, MaxHp = 1000, MaxMp = 500,
                Str = 20, Int = 15, Wis = 12, Con = 18, Dex = 14,
                UnspentPoints = 0, MaxWeight = 100, CurrentWeight = 45
            }
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void Attributes_UnreadMailOnly_MatchesLegacyBody()
    {
        // flags = UnreadMail = 0x01 (standalone bit, no sections)
        var legacy = new LegacyServerPacket(0x08);
        legacy.WriteByte(0x01);

        var typed = Body(new AttributesPacket { UnreadMail = true });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    // --- 0x2E WorldMap: retail-true SFieldMap layout, NOT byte-identical to the legacy
    //     %255-quadrant + Int64-hash emit. `expected` (primitive writers) and `typed` (DALib
    //     WorldMapPacket.WriteBody) are independent code paths, so equality pins DALib's field order.
    //     Node values mirror the darkages-741 field001 capture's Loures node: screen (344,250),
    //     checksum 0, map_id 0x0BC4, dest (14,10) -> routing tail 00 00 0B C4 00 0E 00 0A, which is
    //     exactly the retail C->S 0x3F echo. ---

    [Fact]
    public void WorldMap_EmitsRetailTrueLayout()
    {
        // SFieldMap: string8 field_name, u8 node_count, u8 current_node_index, then per node:
        // u16 screen_x, u16 screen_y, string8 name, u16 checksum, u16 map_id, u16 map_x, u16 map_y
        // (all big-endian). screen_x = 344 exercises the >=255 case the legacy %255 split corrupted.
        var expected = new LegacyServerPacket(0x2E);
        expected.WriteString8("field001");
        expected.WriteByte(1);        // node_count
        expected.WriteByte(0);        // current_node_index
        expected.WriteUInt16(344);    // screen_x
        expected.WriteUInt16(250);    // screen_y
        expected.WriteString8("Loures");
        expected.WriteUInt16(0);      // checksum
        expected.WriteUInt16(0x0BC4); // map_id
        expected.WriteUInt16(14);     // map_x
        expected.WriteUInt16(10);     // map_y

        var typed = Body(new WorldMapPacket
        {
            FieldName = "field001",
            ImageIndex = 0,
            Nodes = new List<WorldMapNode>
            {
                new()
                {
                    X = 344, Y = 250, Text = "Loures",
                    CheckSum = 0, MapId = 0x0BC4, DestinationX = 14, DestinationY = 10
                }
            }
        });

        Assert.Equal(expected.BodyMemory.ToArray(), typed);
    }

    // --- 0x42 Exchange: byte-identical, all six actions. The legacy builder wrote the party
    //     byte as `Side ? 0 : 1`, so the typed mapping is RightSide = !source. Retail semantics
    //     (darkages-741 066-0x42): party 0x00 == "You", nonzero == "Them"; action 5 sets one
    //     per-party ack flag and closes only when both are set. ---

    [Fact]
    public void ExchangeStart_MatchesLegacyBody()
    {
        // 0x42 action 0: u32 requestor id, string8 requestor name. No party byte.
        var legacy = new LegacyServerPacket(0x42);
        legacy.WriteByte(0x00);
        legacy.WriteUInt32(0xDEADBEEF);
        legacy.WriteString8("Kedian");

        var typed = Body(new StartExchangeResponsePacket
            { OtherUserId = 0xDEADBEEF, OtherUserName = "Kedian" });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void ExchangeQuantityPrompt_MatchesLegacyBody()
    {
        // 0x42 action 1: u8 inventory slot. No party byte.
        var legacy = new LegacyServerPacket(0x42);
        legacy.WriteByte(0x01);
        legacy.WriteByte(12);

        var typed = Body(new RequestExchangeAmountPacket { SourceSlot = 12 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(true, 0)]   // source side ("You")  -> wire 0
    [InlineData(false, 1)]  // partner side ("Them") -> wire 1
    public void ExchangeItemUpdate_MatchesLegacyBody(bool source, byte party)
    {
        // 0x42 action 2: party, exchange index, u16 sprite (+0x8000), color, string8 name.
        const string name = "Stone of Ard Ioc [3]";
        var legacy = new LegacyServerPacket(0x42);
        legacy.WriteByte(0x02);
        legacy.WriteByte(party);
        legacy.WriteByte(4);
        legacy.WriteUInt16(0x8000 + 0x0123);
        legacy.WriteByte(7);
        legacy.WriteString8(name);

        var typed = Body(new AddExchangeItemResponsePacket
        {
            RightSide = !source,
            ExchangeIndex = 4,
            Sprite = 0x8000 + 0x0123,
            Color = 7,
            Name = name
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void ExchangeGoldUpdate_MatchesLegacyBody(bool source, byte party)
    {
        // 0x42 action 3: party, u32 gold.
        var legacy = new LegacyServerPacket(0x42);
        legacy.WriteByte(0x03);
        legacy.WriteByte(party);
        legacy.WriteUInt32(1_000_000);

        var typed = Body(new SetExchangeGoldResponsePacket { RightSide = !source, GoldAmount = 1_000_000 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void ExchangeCancel_MatchesLegacyBody(bool source, byte party)
    {
        // 0x42 action 4: party, string8 message. Legacy message text preserved verbatim.
        var legacy = new LegacyServerPacket(0x42);
        legacy.WriteByte(0x04);
        legacy.WriteByte(party);
        legacy.WriteString8("Exchange was cancelled.");

        var typed = Body(new CancelExchangeResponsePacket
            { RightSide = !source, Message = "Exchange was cancelled." });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(true, 0)]   // both sides confirmed -> "You" -> client closes the window
    [InlineData(false, 1)]  // partner confirmed    -> "Them" -> window stays open
    public void ExchangeConfirm_MatchesLegacyBody(bool source, byte party)
    {
        // 0x42 action 5: party, string8 message. This is the confirm-flow value that was queried;
        // the legacy encoding is preserved exactly.
        var legacy = new LegacyServerPacket(0x42);
        legacy.WriteByte(0x05);
        legacy.WriteByte(party);
        legacy.WriteString8("You exchanged.");

        var typed = Body(new AcceptExchangeResponsePacket
            { RightSide = !source, Message = "You exchanged." });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }
}
