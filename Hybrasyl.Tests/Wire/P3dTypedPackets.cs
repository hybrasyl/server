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
using DALib.Networking.Wire;
using Xunit;
using MerchantMenuItem = Hybrasyl.Objects.MerchantMenuItem;
using LegacyServerPacket = Hybrasyl.Tests.Wire.LegacyBodyWriter;
// The test project has its own DisplayUserPacket (the send-site regression), which shadows
// DALib's from inside Hybrasyl.Tests.Wire.
using DalibDisplayUserPacket = DALib.Networking.Packets.Server.DisplayUserPacket;
// Hybrasyl.Subsystems.Dialogs defines its own OptionsDialog/TextDialog; alias the DALib 0x30
// body records for symmetry with how the dialog subsystem imports them.
using DalibNpcDialogPacket = DALib.Networking.Packets.Server.NpcDialogPacket;
using DalibOptionsDialog = DALib.Networking.Packets.Server.OptionsDialog;
using DalibTextDialog = DALib.Networking.Packets.Server.TextDialog;
using DalibTextInputDialog = DALib.Networking.Packets.Server.TextInputDialog;
using DalibNpcMenuPacket = DALib.Networking.Packets.Server.NpcMenuPacket;

namespace Hybrasyl.Tests.Wire;

/// <summary>
///     Phase 3d (hard set) send-path coverage. Everything here is pinned byte-identical against the
///     verbatim pre-conversion emit — no deltas were registered for this batch.
/// </summary>
public class P3dTypedPackets
{
    private static byte[] Body(DALib.Networking.Wire.ServerPacket record)
    {
        var writer = new PacketWriter();
        record.WriteBody(writer);
        return writer.WrittenSpan.ToArray();
    }

    [Fact]
    public void MetafileChecksums_MatchesLegacyBody()
    {
        // 0x6F op 1: the legacy site wrote WriteBoolean(all=true), which is the same 0x01
        // discriminator DALib's AllCheckSums uses. Then [u16 count]{[string8 name][u32 crc]}.
        var legacy = new LegacyServerPacket(0x6F);
        legacy.WriteBoolean(true);
        legacy.WriteUInt16(2);
        legacy.WriteString8("SClass1");
        legacy.WriteUInt32(0xDEADBEEF);
        legacy.WriteString8("ItemInfo0");
        legacy.WriteUInt32(0x12345678);

        var typed = Body(new MetafileChecksumsPacket
        {
            Entries =
            [
                new MetafileEntry("SClass1", 0xDEADBEEF),
                new MetafileEntry("ItemInfo0", 0x12345678)
            ]
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MetafileData_MatchesLegacyBody()
    {
        // 0x6F op 0: WriteBoolean(all=false) == the 0x00 DataByName discriminator, then
        // [string8 name][u32 crc][u16 len][payload].
        var payload = new byte[] { 0x78, 0x9C, 0x01, 0x02, 0x03 };
        var legacy = new LegacyServerPacket(0x6F);
        legacy.WriteBoolean(false);
        legacy.WriteString8("SClass1");
        legacy.WriteUInt32(0xDEADBEEF);
        legacy.WriteUInt16((ushort) payload.Length);
        legacy.Write(payload);

        var typed = Body(new MetafileDataPacket
        {
            Name = "SClass1",
            Checksum = 0xDEADBEEF,
            Data = payload
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(true, 1)]   // skill pane
    [InlineData(false, 0)]  // spell pane
    public void Cooldown_MatchesLegacyBody(bool isSkill, byte pane)
    {
        // 0x3F: [u8 pane][u8 slot][u32 seconds]. The legacy builder's Pane byte is DALib's
        // IsSkill bool (1 = skill pane, 0 = spell pane).
        var legacy = new LegacyServerPacket(0x3F);
        legacy.WriteByte(pane);
        legacy.WriteByte(4);
        legacy.WriteUInt32(30);

        var typed = Body(new CooldownPacket { IsSkill = isSkill, Slot = 4, Seconds = 30 });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void ManufactureOpen_MatchesLegacyBody()
    {
        // 0x50 subtype 0: [u8 type][u8 slot][u8 0][u8 recipeCount].
        var legacy = new LegacyServerPacket(0x50);
        legacy.WriteByte(2);
        legacy.WriteByte(60);
        legacy.WriteByte(0);
        legacy.WriteByte(7);

        var typed = Body(new OpenManufacturePacket
        {
            ManufactureType = 2,
            Slot = 60,
            RecipeCount = 7
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void ManufacturePage_MatchesLegacyBody()
    {
        // 0x50 subtype 1: prefix, then [u8 page][u16 sprite][string8 name][string16 desc]
        // [string16 ingredients][bool hasAddItem].
        const string desc = "A sturdy blade.";
        const string ingredients = "Iron Bar (2)\tLeather (1)";
        var legacy = new LegacyServerPacket(0x50);
        legacy.WriteByte(2);
        legacy.WriteByte(60);
        legacy.WriteByte(1);
        legacy.WriteByte(3);
        legacy.WriteUInt16(0x8000 + 0x0123);
        legacy.WriteString8("Claidheamh");
        legacy.WriteString16(desc);
        legacy.WriteString16(ingredients);
        legacy.WriteBoolean(true);

        var typed = Body(new ManufacturePagePacket
        {
            ManufactureType = 2,
            Slot = 60,
            PageIndex = 3,
            Sprite = 0x8000 + 0x0123,
            RecipeName = "Claidheamh",
            Description = desc,
            Ingredients = ingredients,
            HasAddItem = true
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(0, "Listen to whispers  :ON")]
    [InlineData(6, "Show my group  :OFF")]
    public void SettingsMessage_MatchesLegacyBody(byte number, string displayString)
    {
        // 0x0A type 7 (UserOptions). The legacy builder wrote the setting number as an ASCII
        // digit and the display string separately, hand-computing the string16 length as
        // DisplayString.Length + 1 — i.e. the length of the combined text.
        var legacy = new LegacyServerPacket(0x0A);
        legacy.WriteByte(0x07);
        legacy.WriteByte(0x00);
        legacy.WriteByte((byte) (displayString.Length + 1));
        legacy.WriteByte((byte) (number + 0x30));
        legacy.WriteString(displayString);

        var typed = Body(new SystemMessagePacket
        {
            MessageType = SystemMessageType.UserOptions,
            Message = (char) (number + 0x30) + displayString
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    // 0x33 DisplayUser body prefix, shared by both appearance forms.
    private static void WriteDisplayUserPrefix(LegacyServerPacket legacy)
    {
        legacy.WriteUInt16(12);
        legacy.WriteUInt16(34);
        legacy.WriteByte(2);
        legacy.WriteUInt32(0xCAFEBABE);
    }

    [Fact]
    public void DisplayUserEquipment_MatchesLegacyBody()
    {
        // The legacy builder wrote the armor sprite twice: they are two depth-distinct
        // body-armor passes (paperdoll layers 7 and 5), which is what DALib models as
        // ArmorSprite1/ArmorSprite2. Same value in both slots, same bytes.
        var legacy = new LegacyServerPacket(0x33);
        WriteDisplayUserPrefix(legacy);
        legacy.WriteUInt16(0x1122); // helmet / head sprite (also the discriminator)
        legacy.WriteByte((byte) (1 * 16 + 3)); // (byte)Gender * 16 + BodySpriteOffset
        legacy.WriteUInt16(0x0201); // armor, pass 1
        legacy.WriteByte(0x0A); // boots
        legacy.WriteUInt16(0x0201); // armor, pass 2
        legacy.WriteByte(0x0B); // shield
        legacy.WriteUInt16(0x0303); // weapon
        legacy.WriteByte(0x04); // hair color
        legacy.WriteByte(0x05); // boots color
        legacy.WriteByte(0x06);
        legacy.WriteUInt16(0x0111);
        legacy.WriteByte(0x07);
        legacy.WriteUInt16(0x0222);
        legacy.WriteByte(0x08);
        legacy.WriteUInt16(0x0333);
        legacy.WriteByte(0x02); // lantern size / light mask
        legacy.WriteByte(0x01); // rest position
        legacy.WriteUInt16(0x0444); // overcoat
        legacy.WriteByte(0x09); // overcoat color
        legacy.WriteByte(0x0C); // skin color
        legacy.WriteBoolean(true); // invisible (client-side: translucency)
        legacy.WriteByte(0x0D); // face shape
        legacy.WriteByte(0x01); // name style
        legacy.WriteString8("Dionysus");
        legacy.WriteString8("Recruiting!");

        var typed = Body(new DalibDisplayUserPacket
        {
            X = 12,
            Y = 34,
            Direction = DALib.Enums.Direction.South,
            Id = 0xCAFEBABE,
            Appearance = new EquipmentAppearance
            {
                HeadSprite = 0x1122,
                BodySprite = 1 * 16 + 3,
                ArmorSprite1 = 0x0201,
                BootsSprite = 0x0A,
                ArmorSprite2 = 0x0201,
                ShieldSprite = 0x0B,
                WeaponSprite = 0x0303,
                HeadColor = 0x04,
                BootsColor = 0x05,
                AccessoryColor1 = 0x06,
                AccessorySprite1 = 0x0111,
                AccessoryColor2 = 0x07,
                AccessorySprite2 = 0x0222,
                AccessoryColor3 = 0x08,
                AccessorySprite3 = 0x0333,
                LanternSize = 0x02,
                RestPosition = 0x01,
                OvercoatSprite = 0x0444,
                OvercoatColor = 0x09,
                BodyColor = 0x0C,
                IsHidden = true,
                FaceSprite = 0x0D
            },
            NameTagStyle = 0x01,
            Name = "Dionysus",
            GroupName = "Recruiting!"
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void DisplayUserCreatureForm_MatchesLegacyBody()
    {
        // 0xFFFF sentinel in place of the head sprite, then the monster sprite, two color
        // bytes, and six reserved zeroes the client parses but never consumes. The record
        // carries the sprite verbatim; the 0x4000 namespace tag is applied at the send site.
        var legacy = new LegacyServerPacket(0x33);
        WriteDisplayUserPrefix(legacy);
        legacy.WriteUInt16(0xFFFF);
        legacy.WriteUInt16(0x0405);
        legacy.WriteByte(0x04); // hair color
        legacy.WriteByte(0x05); // boots color
        for (var i = 0; i < 6; i++)
            legacy.WriteByte(0x00);
        legacy.WriteByte(0x01); // name style
        legacy.WriteString8("Dionysus");
        legacy.WriteString8(string.Empty);

        var typed = Body(new DalibDisplayUserPacket
        {
            X = 12,
            Y = 34,
            Direction = DALib.Enums.Direction.South,
            Id = 0xCAFEBABE,
            Appearance = new CreatureSpriteAppearance
            {
                Sprite = 0x0405,
                HeadColor = 0x04,
                BootsColor = 0x05
            },
            NameTagStyle = 0x01,
            Name = "Dionysus",
            GroupName = string.Empty
        });

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    // 0x2F body prefix as the legacy MerchantResponse builder wrote it: a hardcoded 0 color, a
    // hardcoded 1, a repeat of the speaker sprite and two more zeroes — i.e. the client's ignored
    // one-byte and four-byte groups, then the illustration index. Color1/Color2/Tile2/PortraitType
    // were settable on the legacy builder but never reached the wire.
    private static void WriteMerchantPrefix(LegacyServerPacket legacy, byte menuType, string text)
    {
        legacy.WriteByte(menuType);
        legacy.WriteByte(0x01); // object type: merchant
        legacy.WriteUInt32(0x0000ABCD);
        legacy.WriteByte(0x00);
        legacy.WriteInt16(0x4111); // speaker sprite (0x4000-tagged)
        legacy.WriteByte(0x00);
        legacy.WriteByte(0x01);
        legacy.WriteInt16(0x4111);
        legacy.WriteByte(0x00);
        legacy.WriteByte(0x00);
        legacy.WriteString8("Riona");
        legacy.WriteString16(text);
    }

    private static DalibNpcMenuPacket MerchantPacket(NpcMenuType type, string text, NpcMenu menu) =>
        new()
        {
            MenuType = type,
            SourceId = 0x0000ABCD,
            Sprite = 0x4111,
            Sprite2 = 0x4111,
            Name = "Riona",
            Text = text,
            Menu = menu
        };

    /// <summary>
    ///     ShowMerchantGoBack emitted its options menu inline with a hand-built ServerPacket
    ///     rather than through the MerchantMenu helper, so P3d's sweep missed it and it survived
    ///     as the last positional send site on the branch. Pins that routing it through the
    ///     helper in P5b changes nothing on the wire — it is a live path (every "go back" row in
    ///     a merchant flow).
    /// </summary>
    [Fact]
    public void MerchantGoBack_MatchesLegacyBody()
    {
        var legacy = new LegacyServerPacket(0x2F);
        WriteMerchantPrefix(legacy, 0x00, "Anything else?");
        legacy.WriteByte(1);
        legacy.WriteString8("Go back");
        legacy.WriteUInt16((ushort) MerchantMenuItem.MainMenu);

        var typed = MerchantPacket(NpcMenuType.Options, "Anything else?", new OptionsMenu
        {
            Options = [new NpcMenuOption("Go back", (ushort) MerchantMenuItem.MainMenu)]
        }).ToBody();

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MerchantOptions_MatchesLegacyBody()
    {
        const string text = "How can I help?";
        var legacy = new LegacyServerPacket(0x2F);
        WriteMerchantPrefix(legacy, 0, text);
        legacy.WriteByte(2);
        legacy.WriteString8("Buy");
        legacy.WriteUInt16(0xFF01);
        legacy.WriteString8("Sell");
        legacy.WriteUInt16(0xFF02);

        var typed = Body(MerchantPacket(NpcMenuType.Options, text, new OptionsMenu
        {
            Options = [new NpcMenuOption("Buy", 0xFF01), new NpcMenuOption("Sell", 0xFF02)]
        }));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MerchantTextEntry_MatchesLegacyBody()
    {
        const string text = "How many?";
        var legacy = new LegacyServerPacket(0x2F);
        WriteMerchantPrefix(legacy, 2, text);
        legacy.WriteUInt16(0xFF11);

        var typed = Body(MerchantPacket(NpcMenuType.TextEntry, text,
            new TextEntryMenu { PursuitId = 0xFF11 }));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MerchantItemList_MatchesLegacyBody()
    {
        // Hybrasyl's pursuit ids are all in the 0xFF00+ private range, so DALib's 0x4B
        // rich-item fork can never fire from a Hybrasyl emit.
        const string text = "Wares.";
        var legacy = new LegacyServerPacket(0x2F);
        WriteMerchantPrefix(legacy, 4, text);
        legacy.WriteUInt16(0xFF10);
        legacy.WriteUInt16(1);
        legacy.WriteUInt16(0x8123);
        legacy.WriteByte(0x02);
        legacy.WriteUInt32(500);
        legacy.WriteString8("Claidheamh");
        legacy.WriteString8("A sturdy blade.");

        var typed = Body(MerchantPacket(NpcMenuType.ItemList, text, new ItemListMenu
        {
            PursuitId = 0xFF10,
            Items = [new NpcMenuItem(0x8123, 0x02, 500, "Claidheamh", "A sturdy blade.")]
        }));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void MerchantPlayerItemList_MatchesLegacyBody()
    {
        // Likewise the 0x4E per-row-handle fork: unreachable from a 0xFF00+ pursuit.
        const string text = "What will you sell?";
        var legacy = new LegacyServerPacket(0x2F);
        WriteMerchantPrefix(legacy, 5, text);
        legacy.WriteUInt16(0xFF13);
        legacy.WriteByte(3);
        legacy.WriteByte(1);
        legacy.WriteByte(4);
        legacy.WriteByte(9);

        var typed = Body(MerchantPacket(NpcMenuType.PlayerItemList, text, new PlayerItemListMenu
        {
            PursuitId = 0xFF13,
            Slots = [1, 4, 9]
        }));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(6, false)] // spells
    [InlineData(7, true)] // skills
    public void MerchantCastableList_MatchesLegacyBody(byte menuType, bool isSkill)
    {
        const string text = "What would you learn?";
        var legacy = new LegacyServerPacket(0x2F);
        WriteMerchantPrefix(legacy, menuType, text);
        legacy.WriteUInt16(0xFF30);
        legacy.WriteUInt16(1);
        legacy.WriteByte(3); // icon type
        legacy.WriteUInt16(0x0042); // icon (a byte on the Hybrasyl struct, u16 on the wire)
        legacy.WriteByte(1); // color
        legacy.WriteString8("Assail");

        NpcMenu menu = isSkill
            ? new SkillListMenu { PursuitId = 0xFF30, Skills = [new NpcMenuCastable(3, 0x0042, 1, "Assail")] }
            : new SpellListMenu { PursuitId = 0xFF30, Spells = [new NpcMenuCastable(3, 0x0042, 1, "Assail")] };

        var typed = Body(MerchantPacket(isSkill ? NpcMenuType.SkillList : NpcMenuType.SpellList, text, menu));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Theory]
    [InlineData(8, false)] // spell book
    [InlineData(9, true)] // skill book
    public void MerchantPlayerBook_MatchesLegacyBody(byte menuType, bool isSkill)
    {
        // Types 8/9 carry the pursuit id and nothing else — an absent slot list means "all
        // learned entries", which is what Hybrasyl has always relied on.
        const string text = "What would you forget?";
        var legacy = new LegacyServerPacket(0x2F);
        WriteMerchantPrefix(legacy, menuType, text);
        legacy.WriteUInt16(0xFF35);

        NpcMenu menu = isSkill
            ? new PlayerSkillListMenu { PursuitId = 0xFF35 }
            : new PlayerSpellListMenu { PursuitId = 0xFF35 };

        var typed = Body(MerchantPacket(
            isSkill ? NpcMenuType.PlayerSkillList : NpcMenuType.PlayerSpellList, text, menu));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    // 0x30 body prefix as the legacy GenerateBasePacket wrote it. The second sprite/color pair
    // is the client's ignored four-byte secondary group, which the legacy site filled with a
    // repeat of the first pair.
    private static void WriteNpcDialogPrefix(LegacyServerPacket legacy, byte dialogType)
    {
        legacy.WriteByte(dialogType);
        legacy.WriteByte(0x01); // object type: creature
        legacy.WriteUInt32(0x00001234);
        legacy.WriteByte(0x00);
        legacy.WriteUInt16(0x4111); // sprite (0x4000-tagged creature)
        legacy.WriteByte(0x00); // color
        legacy.WriteByte(0x00);
        legacy.WriteUInt16(0x4111);
        legacy.WriteByte(0x00);
        legacy.WriteUInt16(0x0007); // pursuit id
        legacy.WriteUInt16(0x0002); // dialog index
        legacy.WriteBoolean(true); // has previous
        legacy.WriteBoolean(false); // has next
        legacy.WriteByte(0x00);
        legacy.WriteString8("Riona");
    }

    private static DalibNpcDialogPacket BasePacket(NpcDialogType type, NpcDialog body, string text) =>
        new()
        {
            DialogType = type,
            ObjectType = DalibNpcDialogPacket.ObjectTypeCreature,
            SourceId = 0x00001234,
            Sprite = 0x4111,
            Color = 0,
            Sprite2 = 0x4111,
            Color2 = 0,
            PursuitId = 0x0007,
            DialogId = 0x0002,
            HasPreviousButton = true,
            HasNextButton = false,
            Name = "Riona",
            Text = text,
            Body = body
        };

    [Fact]
    public void NpcDialogSimple_MatchesLegacyBody()
    {
        const string text = "Well met, aisling.";
        var legacy = new LegacyServerPacket(0x30);
        WriteNpcDialogPrefix(legacy, 0);
        legacy.WriteString16(text);

        var typed = Body(BasePacket(NpcDialogType.Normal, new DalibTextDialog(), text));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void NpcDialogOptions_MatchesLegacyBody()
    {
        const string text = "What do you need?";
        var legacy = new LegacyServerPacket(0x30);
        WriteNpcDialogPrefix(legacy, 2);
        legacy.WriteString16(text);
        legacy.WriteByte(2);
        legacy.WriteString8("Buy");
        legacy.WriteString8("Sell");

        var typed = Body(BasePacket(NpcDialogType.Options,
            new DalibOptionsDialog { Options = ["Buy", "Sell"] }, text));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void NpcDialogTextInput_MatchesLegacyBody()
    {
        const string text = "Name your price.";
        var legacy = new LegacyServerPacket(0x30);
        WriteNpcDialogPrefix(legacy, 4);
        legacy.WriteString16(text);
        legacy.WriteString8("Amount:");
        legacy.WriteByte(8);
        legacy.WriteString8("gold");

        var typed = Body(BasePacket(NpcDialogType.TextInput,
            new DalibTextInputDialog { TopCaption = "Amount:", InputLength = 8, BottomCaption = "gold" }, text));

        Assert.Equal(legacy.BodyMemory.ToArray(), typed);
    }

    [Fact]
    public void NpcDialogClose_DropsTrailingSlack()
    {
        // the legacy site wrote [0x0A][0x00]. The client returns from the deserializer
        // immediately after the type byte, so the body is the type byte alone.
        var typed = Body(new DalibNpcDialogPacket
        {
            DialogType = NpcDialogType.Close,
            Body = new CloseDialog()
        });

        Assert.Equal([0x0A], typed);
    }

    [Fact]
    public void NpcDialogEmptyText_StillEmitsTheStringField()
    {
        // the legacy site skipped the string16 entirely when the text was empty, which
        // truncated a body the client was still parsing — an options dialog would then read its
        // choice count out of the tail. The field is now always present, empty as [u16 0].
        var typed = Body(BasePacket(NpcDialogType.Options,
            new DalibOptionsDialog { Options = ["Yes"] }, string.Empty));

        var legacy = new LegacyServerPacket(0x30);
        WriteNpcDialogPrefix(legacy, 2);
        var prefixLength = legacy.BodyMemory.Length;

        // Prefix is untouched; the two length bytes of the empty string16 follow it.
        Assert.Equal(legacy.BodyMemory.ToArray(), typed[..prefixLength]);
        Assert.Equal(0x00, typed[prefixLength]);
        Assert.Equal(0x00, typed[prefixLength + 1]);
        Assert.Equal(0x01, typed[prefixLength + 2]); // option count, correctly aligned
    }
}
