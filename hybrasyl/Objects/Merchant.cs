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
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using Hybrasyl.Enums;
using Hybrasyl.Properties;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Objects
{
    public class Merchant : Monster
    {
        public bool Ready;
        //public npc Data;
        public MerchantJob Jobs { get; set; }
        public Dictionary<string, XML.Items.ItemType> Inventory { get; private set; }

        public Merchant()
            : base()
        {
            Ready = false;
            //Jobs = (MerchantJob).jobs;
            Inventory = new Dictionary<string, XML.Items.ItemType>();
            //foreach (var item in data.inventory)
            //{
            //   Inventory.Add(item.name, item);
            //}
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
        Train = 0x04,
        Repair = 0x08,
        Post = 0x10
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
        SellItem = 0xFF12,
        SellItemQuantity = 0xFF13,
        SellItemAccept = 0xFF14,

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

        RepairItem = 0xFF40,
        RepairItemAccept = 0xFF41,
        RepairAllItemsAccept = 0xFF43,

        SendParcel = 0xFF50,
        SendParcelRecipient = 0xFF51,
        SendLetter = 0xFF52,
        SendLetterRecipient = 0xFF53
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
