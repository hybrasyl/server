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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using Hybrasyl.Enums;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Objects
{
    public class Merchant : Monster
    {
        public bool Ready;
        public npcs Data;
        public MerchantJob Jobs { get; set; }
        public Dictionary<string, items> Inventory { get; private set; }

        public Merchant()
            : base()
        {
        }

        public Merchant(npcs data)
        {
            Data = data;
            X = (byte)data.Map_x;
            Y = (byte)data.Map_y;
            Sprite = (ushort)data.Sprite;
            Direction = (Direction)data.Direction;
            Name = data.Name;
            DisplayText = data.Display_text;
            Ready = false;
            Jobs = (MerchantJob)data.Jobs;
            Inventory = new Dictionary<string, items>();
        }

        public void OnSpawn()
        {
        }

        public override void Attack()
        {
        }

        public override void OnClick(User invoker)
        {
            if (!Ready)
            {
                OnSpawn();
            }
        }

        public override void AoiEntry(VisibleObject obj)
        {
        }

        public override void AoiDeparture(VisibleObject obj)
        {
        }

        

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                var npcPacket = new ServerPacket(0x07);
                npcPacket.WriteUInt16(0x01);
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

                npcPacket.WriteByte(2);
                npcPacket.WriteString8(Name);
                user.Enqueue(npcPacket);
            }
        }
    }

    [Flags]
    public enum MerchantJob
    {
        Banker = 0x02,
        Postman = 0x10,
        Repairer = 0x08,
        Trainer = 0x04,
        Vendor = 0x01
    }

    public enum MerchantMenuItem : ushort
    {
        BuyItem = 0xFF10,
        BuyItemMenu = 0xFF01,
        BuyItemQuantity = 0xFF11,
        DepositGoldMenu = 0xFF06,
        DepositItem = 0xFF22,
        DepositItemMenu = 0xFF05,
        DepositItemQuantity = 0xFF23,
        ForgetSkill = 0xFF34,
        ForgetSkillAccept = 0xFF35,
        ForgetSkillMenu = 0xFF09,
        ForgetSpell = 0xFF36,
        ForgetSpellAccept = 0xFF37,
        ForgetSpellMenu = 0xFF0A,
        LearnSkill = 0xFF30,
        LearnSkillAccept = 0xFF31,
        LearnSkillMenu = 0xFF07,
        LearnSpell = 0xFF32,
        LearnSpellAccept = 0xFF33,
        LearnSpellMenu = 0xFF08,
        MainMenu = 0xFF00,
        ReceiveParcel = 0xFF0F,
        RepairAllItems = 0xFF0C,
        RepairAllItemsAccept = 0xFF43,
        RepairItem = 0xFF40,
        RepairItemAccept = 0xFF41,
        RepairItemMenu = 0xFF0B,
        SellItem = 0xFF12,
        SellItemAccept = 0xFF14,
        SellItemMenu = 0xFF02,
        SellItemQuantity = 0xFF13,
        SendLetter = 0xFF52,
        SendLetterMenu = 0xFF0E,
        SendLetterRecipient = 0xFF53,
        SendParcel = 0xFF50,
        SendParcelMenu = 0xFF0D,
        SendParcelRecipient = 0xFF51,
        WithdrawGoldMenu = 0xFF04,
        WithdrawItem = 0xFF20,
        WithdrawItemMenu = 0xFF03,
        WithdrawItemQuantity = 0xFF21
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
