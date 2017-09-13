/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */


using Hybrasyl.Items;
using Hybrasyl.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Creatures;

namespace Hybrasyl.Objects
{
    public class Merchant : Creature
    {
        public bool Ready;
        //public npc Data;
        public NpcRoleList Roles { get; set; }
        public MerchantJob Jobs { get; set; }
        public Dictionary<string, Item> Inventory { get; private set; }

        public Merchant()
            : base()
        {
            Ready = false;
            //Roles = Game.World.WorldData.Get<Npc>(Name).Roles;
            Inventory = new Dictionary<string, Item>();
            //Jobs = new List<MerchantJob>();
            //if(Roles.Bank != null) Jobs.Add(MerchantJob.Bank);
            //if(Roles.Post != null) Jobs.Add(MerchantJob.Post);
            //if(Roles.Repair != null) Jobs.Add(MerchantJob.Repair);
            //if(Roles.Vend != null) Jobs.Add(MerchantJob.Vend);
            //if (Roles.Train == null) return;
            //if (Roles.Train.Count > 0 && Roles.Train.FirstOrDefault(x => x.Type == "skill") != null) Jobs.Add(MerchantJob.Skills);
            //if (Roles.Train.Count > 0 && Roles.Train.FirstOrDefault(x => x.Type == "spell") != null) Jobs.Add(MerchantJob.Spells);
        }
        


        public void OnSpawn()
        {
            // Do we have a script? 
            Script = World.ScriptProcessor.GetScript(Name);
            if (Script != null)
            {
                Script.AssociateScriptWithObject(this);
                if (Script.InstantiateScriptable())
                {
                    Script.ExecuteScriptableFunction("OnSpawn");
                    Ready = true;
                }
            }
        }

        public override void OnClick(User invoker)
        {
            if (!Ready)
                OnSpawn();

            if (Script != null)
            {
                Script.ExecuteScriptableFunction("OnClick", new HybrasylUser(invoker));
            }
            else
            {
                DisplayPursuits(invoker);
            }
        }

        public override void AoiEntry(VisibleObject obj)
        {
            if (Script != null)
            {
                Script.ExecuteScriptableFunction("OnEntry", new HybrasylWorldObject(obj));
            }
        }

        public override void AoiDeparture(VisibleObject obj)
        {
            if (Script != null)
            {
                Script.ExecuteScriptableFunction("OnLeave", new HybrasylWorldObject(obj));
            }
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                var npcPacket = new ServerPacket(0x07);
                npcPacket.WriteUInt16(0x01); // Number of mobs in this packet
                npcPacket.WriteUInt16(X);
                npcPacket.WriteUInt16(Y);
                npcPacket.WriteUInt32(Id);
                npcPacket.WriteUInt16((ushort)(Sprite + 0x4000));
                npcPacket.WriteByte(0);
                npcPacket.WriteByte(0);
                npcPacket.WriteByte(0);
                npcPacket.WriteByte(0);
                npcPacket.WriteByte((byte)Direction);
                npcPacket.WriteByte(0);

                npcPacket.WriteByte(2); // Dot color. 0 = monster, 1 = nonsolid monster, 2=NPC
                npcPacket.WriteString8(Name);
                user.Enqueue(npcPacket);
            }
        }
    }

    [Flags]
    public enum MerchantJob
    {
        Vend = 0x01,
        Bank = 0x02,
        Skills = 0x04,
        Spells = 0x08,
        Repair = 0x10,
        Post = 0x20
    }

    public enum MerchantMenuItem : ushort
    {
        MainMenu = 0xFF00,

        BuyItemMenu = 0xFF01,
        SellItemMenu = 0xFF02,

        WithdrawItemMenu = 0xFF03,
        WithdrawGoldMenu = 0xFF04,
        DepositItemMenu = 0xFF05,
        DepositGoldMenu = 0xFF06,

        LearnSkillMenu = 0xFF07,
        LearnSpellMenu = 0xFF08,
        ForgetSkillMenu = 0xFF09,
        ForgetSpellMenu = 0xFF0A,

        RepairItemMenu = 0xFF0B,
        RepairAllItems = 0xFF0C,

        SendParcelMenu = 0xFF0D,
        SendLetterMenu = 0xFF0E,
        ReceiveParcel = 0xFF0F,

        BuyItem = 0xFF10,
        BuyItemQuantity = 0xFF11,
        BuyItemAccept = 0xFF12,
        SellItem = 0xFF13,
        SellItemQuantity = 0xFF14,
        SellItemConfirm = 0xFF15,
        SellItemAccept = 0xFF16,

        WithdrawItem = 0xFF20,
        WithdrawItemQuantity = 0xFF21,
        DepositItem = 0xFF22,
        DepositItemQuantity = 0xFF23,

        LearnSkill = 0xFF30,
        LearnSkillAccept = 0xFF31,
        LearnSpell = 0xFF32,
        LearnSpellAccept = 0xFF33,
        ForgetSkill = 0xFF34,
        ForgetSkillAccept = 0xFF35,
        ForgetSpell = 0xFF36,
        ForgetSpellAccept = 0xFF37,
        LearnSkillAgree = 0xFF38,
        LearnSkillDisagree = 0xFF39,
        LearnSpellAgree = 0xFF3A,
        LearnSpellDisagree = 0xFF3B,


        RepairItem = 0xFF40,
        RepairItemAccept = 0xFF41,
        RepairAllItemsAccept = 0xFF43,

        SendParcel = 0xFF50,
        SendParcelRecipient = 0xFF51,
        SendLetter = 0xFF52,
        SendLetterRecipient = 0xFF53,
        SendParcelAccept = 0xFF54,
        SendParcelSuccess = 0xFF55,
        SendParcelFailure = 0xFF56,
        SendLetterAccept = 0xFF57,
        SendLetterSuccess = 0xFF59,
        SendLetterFailure = 0xFF59,

    }

    enum MerchantDialogType : byte
    {
        Options = 0,
        OptionsWithArgument = 1,
        Input = 2,
        InputWithArgument = 3,
        MerchantShopItems = 4,
        UserInventoryItems = 5,
        MerchantSpells = 6,
        MerchantSkills = 7,
        UserSpellBook = 8,
        UserSkillBook = 9
    }

    enum MerchantDialogObjectType : byte
    {
        Merchant = 1
    }

    public struct MerchantOptions
    {
        public byte OptionsCount;
        public List<MerchantDialogOption> Options;
    }

    public struct MerchantOptionsWithArgument
    {
        public byte ArgumentLength;
        public string Argument;
        public byte OptionsCount;
        public List<MerchantDialogOption> Options;
    }

    public struct MerchantDialogOption
    {
        public byte Length;
        public string Text;
        public ushort Id;
    }

    public struct MerchantInput
    {
        public ushort Id;
    }

    public struct MerchantInputWithArgument
    {
        public byte ArgumentLength;
        public string Argument;
        public ushort Id;
    }

    public struct MerchantShopItems
    {
        public ushort Id;
        public ushort ItemsCount;
        public List<MerchantShopItem> Items;
    }

    public struct MerchantShopItem
    {
        public ushort Tile;
        public byte Color;
        public uint Price;
        public byte NameLength;
        public string Name;
        public byte DescriptionLength;
        public string Description;
    }

    public struct UserInventoryItems
    {
        public ushort Id;
        public byte InventorySlotsCount;
        public List<byte> InventorySlots;
    }

    public struct UserSkillBook
    {
        public ushort Id;
    }

    public struct UserSpellBook
    {
        public ushort Id;
    }

    public struct MerchantSpells
    {
        public ushort Id;
        public ushort SpellsCount;
        public byte IconType;
        public List<MerchantSpell> Spells;
    }

    public struct MerchantSpell
    {
        public byte IconType;
        public byte Icon;
        public byte Color;
        public byte NameLength;
        public string Name;
    }

    public struct MerchantSkills
    {
        public ushort Id;
        public ushort SkillsCount;
        public byte IconType;
        public List<MerchantSkill> Skills;
    }

    public struct MerchantSkill
    {
        public byte IconType;
        public byte Icon;
        public byte Color;
        public byte NameLength;
        public string Name;
    }




    public delegate void MerchantMenuHandlerDelegate(User user, Merchant merchant, ClientPacket packet);

    public class MerchantMenuHandler
    {
        public MerchantJob RequiredJob { get; set; }
        public MerchantMenuHandlerDelegate Callback { get; set; }
        public MerchantMenuHandler(MerchantJob requiredJob, MerchantMenuHandlerDelegate callback)
        {
            RequiredJob = requiredJob;
            Callback = callback;
        }
    }
}
