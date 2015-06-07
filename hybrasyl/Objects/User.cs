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

using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Utility;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Hybrasyl.Objects
{
    public class User : Creature
    {
        public static new readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Client Client { get; set; }

        public Sex Sex { get; private set; }
        private accounts Account { get; set; }

        public byte HairStyle { get; set; }
        public byte HairColor { get; set; }
        public Class Class { get; set; }
        public bool IsMaster { get; set; }

        public bool Dead { get; set; }



        public bool Grouping { get; set; }
        public UserStatus GroupStatus { get; set; }
        public byte[] PortraitData { get; set; }
        public string ProfileText { get; set; }

        public string Title { get; set; }
        public string Guild { get; set; }
        public string GuildRank { get; set; }

        public nations Citizenship { get; private set; }
        public List<legend_marks> LegendMarks { get; private set; }

        public DateTime LoginTime { get; private set; }
        public DateTime LogoffTime { get; private set; }

        public DialogState DialogState { get; set; }

        private Dictionary<String, String> UserFlags { get; set; }
        private Dictionary<String, String> UserSessionFlags { get; set; }

        public Exchange ActiveExchange { get; set; }
        public PlayerStatus Status { get; set; }

        public bool IsAvailableForExchange
        {
            get
            {
                return Status == PlayerStatus.Alive;
            }
        }

        public uint ExpToLevel
        {
            get
            {
                if (Level == 99)
                {
                    return 0;
                }
                else
                {
                    return (uint)Math.Pow(Level, 3) * 250;
                }
            }
        }

        public uint LevelPoints = 0;

        public byte CurrentMusicTrack { get; set; }

        public bool IsPrivileged
        {
            get
            {
                return IsExempt || Flags.ContainsKey("gamemaster");
            }
        }

        public bool IsExempt
        {
            get
            {
                return Name == "wren" || (Account != null && Account.Email == "dm8824163@gmail.com");
            }
        }

        public double SinceLastLogin
        {
            get
            {
                var span = (LoginTime - LogoffTime);
                if (span.TotalSeconds < 0)
                {
                    return 0;
                }
                else
                {
                    return span.TotalSeconds;
                }
            }
        }

        public long LastSpoke { get; set; }
        public string LastSaid { get; set; }
        public int NumSaidRepeated { get; set; }

        public static readonly Dictionary<String, String> EntityFrameworkMapping = new Dictionary<String, String>
        {
            { "Name", "name" },
            { "Sex", "sex" },
            { "HairStyle", "hairstyle" },
            { "HairColor", "haircolor" },
            { "Class", "class_type" },
            { "Level", "level" },
            { "LevelPoints", "level_points" },
            { "Experience", "exp" },
            { "Ability", "ab" },
            { "MapId", "map_id" },
            { "MaximumStack", "max_stack" },
            { "MapX", "map_x" },
            { "MapY", "map_y" },
            { "AbilityExp", "ab_exp" },
            { "BaseHp", "max_hp" },
            { "BaseMp", "max_mp" },
            { "Hp", "cur_hp" },
            { "Mp", "cur_mp" },
            { "BaseStr", "str" },
            { "BaseInt", "int" },
            { "BaseWis", "wis" },
            { "BaseCon", "con" },
            { "BaseDex", "dex" },
            { "Gold", "gold" }
        };

        public void Enqueue(ServerPacket packet)
        {
            Logger.DebugFormat("Sending {0:X2} to {1}", packet.Opcode, Name);
            Client.Enqueue(packet);
        }

        public override void AoiEntry(VisibleObject obj)
        {
            Logger.DebugFormat("Showing {0} to {1}", Name, obj.Name);
            obj.ShowTo(this);
        }

        public override void AoiDeparture(VisibleObject obj)
        {
            Logger.DebugFormat("Removing item with ID {0}", obj.Id);
            var removePacket = new ServerPacket(0x0E);
            removePacket.WriteUInt32(obj.Id);
            Enqueue(removePacket);
        }

        public void AoiDeparture(VisibleObject obj, int transmitDelay)
        {
            Logger.DebugFormat("Removing item with ID {0}", obj.Id);
            var removePacket = new ServerPacket(0x0E);
            removePacket.TransmitDelay = transmitDelay;
            removePacket.WriteUInt32(obj.Id);
            Enqueue(removePacket);
        }

        public override void Attack()
        {
            var target = Map.GetInfront(this, 1);

#warning TODO: implement assail damage formula
            var damage = (Hit + Dmg) + (Level + Dex * 0.2) * Str * 2;

            if (target != null)
            {
                if (target is Creature || target is Merchant)
                {
                    (target as Creature).Damage(damage, OffensiveElement, DamageType.Physical);
                    (target as Creature).UpdateAttributes(StatUpdateFlags.Current);

                    (target as Creature).ShowHealthBar();

                    foreach (var mapobj in Map.EntityTree.OfType<User>())
                    {
                        if (mapobj != null)
                        {
                            (target as Creature).ShowHealthBar();
                        }
                    }
                }
            }
        }

        public Dictionary<string, bool> Flags { get; private set; }

        public bool IsMuted { get; set; }
        public bool IsIgnoringWhispers { get; set; }
        public bool IsAtWorldMap { get; set; }

        public string GroupText
        {
            get
            {
                if (Grouping)
                {
                    return "Grouped!";
                }
                return "Adventuring Alone";
            }
        }

        public ushort CurrentWeight
        {
            get
            {
                return (ushort)(Inventory.Weight + Equipment.Weight);
            }
        }

        public ushort MaximumWeight
        {
            get
            {
                return (ushort)(BaseStr + Level / 4 + 48);
            }
        }

        private void _initializeUser(string playername = "")
        {
            Inventory = new Inventory(59);
            Equipment = new Inventory(18);
            IsAtWorldMap = false;
            Title = String.Empty;
            Guild = String.Empty;
            GuildRank = String.Empty;
            LegendMarks = null;
            LastSaid = String.Empty;
            LastSpoke = 0;
            NumSaidRepeated = 0;
            PortraitData = new byte[0];
            ProfileText = string.Empty;
            DialogState = new DialogState(this);
            UserFlags = new Dictionary<String, String>();
            UserSessionFlags = new Dictionary<String, String>();
            Status = PlayerStatus.Alive;


            if (!string.IsNullOrEmpty(playername))
            {
                Name = playername;
            }
        }

        public void GiveExperience(uint exp)
        {
            var levelsGained = 0;

            while (exp + Experience >= ExpToLevel)
            {
                var tolevel = ExpToLevel - Experience;
                exp = exp - tolevel;
                Experience = Experience + tolevel;
                levelsGained++;
                Level++;
                LevelPoints = LevelPoints + 2;
            }
            Experience = Experience + exp;

            if (levelsGained > 0)
            {
                Client.SendMessage("A rush of insight fills you!", MessageTypes.SYSTEM);
                Effect(50, 250);
                UpdateAttributes(StatUpdateFlags.Full);
            }

            UpdateAttributes(StatUpdateFlags.Experience);
        }

        public void TakeExperience(uint exp)
        {
        }

        public User(World world, long connectionId, string playername = "")
        {
            World = world;
            Client client;
            if (GlobalConnectionManifest.ConnectedClients.TryGetValue(connectionId, out client))
            {
                Client = client;
            }
            _initializeUser(playername);
        }

        public User(World world, Client client, string playername = "")
        {
            World = world;
            Client = client;
            _initializeUser(playername);
        }

        /// <summary>
        /// Given a specified item, apply the given bonuses to the player.
        /// </summary>
        /// <param name="toApply">The item used to calculate bonuses.</param>
        public void ApplyBonuses(Item toApply)
        {
            Logger.DebugFormat("Bonuses are: {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}",
                toApply.BonusHp, toApply.BonusHp, toApply.BonusStr, toApply.BonusInt, toApply.BonusWis,
                toApply.BonusCon, toApply.BonusDex, toApply.BonusHit, toApply.BonusDmg, toApply.BonusAc,
                toApply.BonusMr, toApply.BonusRegen);

            BonusHp += toApply.BonusHp;
            BonusMp += toApply.BonusMp;
            BonusStr += toApply.BonusStr;
            BonusInt += toApply.BonusInt;
            BonusWis += toApply.BonusWis;
            BonusCon += toApply.BonusCon;
            BonusDex += toApply.BonusDex;
            BonusHit += toApply.BonusHit;
            BonusDmg += toApply.BonusDmg;
            BonusAc += toApply.BonusAc;
            BonusMr += toApply.BonusMr;
            BonusRegen += toApply.BonusRegen;
            switch (toApply.EquipmentSlot)
            {
                case (byte)ItemSlots.Necklace:
                    OffensiveElement = toApply.Element;
                    break;
                case (byte)ItemSlots.Waist:
                    DefensiveElement = toApply.Element;
                    break;
            }
            Logger.DebugFormat("Player {0}: stats now {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}",
            BonusHp, BonusHp, BonusStr, BonusInt, BonusWis,
            BonusCon, BonusDex, BonusHit, BonusDmg, BonusAc,
            BonusMr, BonusRegen, OffensiveElement, DefensiveElement);
        }
        /// <summary>
        /// Check to see if a player is squelched, considering a given object
        /// </summary>
        /// <param name="opcode">The byte opcode to check</param>
        /// <param name="obj">The object for comparison</param>
        /// <returns></returns>
        public bool CheckSquelch(byte opcode, object obj)
        {
            ThrottleInfo tinfo;
            if (Client.Throttle.TryGetValue(opcode, out tinfo))
            {
                if (tinfo.IsSquelched(obj))
                {
                    if (tinfo.SquelchCount > tinfo.Throttle.DisconnectAfter)
                    {
                        Logger.WarnFormat("cid {0}: reached squelch count for {1}: disconnected", Client.ConnectionId, opcode);
                        Client.Connected = false;
                    }
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Given a specified item, remove the given bonuses from the player.
        /// </summary>
        /// <param name="toRemove"></param>
        public void RemoveBonuses(Item toRemove)
        {
            BonusHp -= toRemove.BonusHp;
            BonusMp -= toRemove.BonusMp;
            BonusStr -= toRemove.BonusStr;
            BonusInt -= toRemove.BonusInt;
            BonusWis -= toRemove.BonusWis;
            BonusCon -= toRemove.BonusCon;
            BonusDex -= toRemove.BonusDex;
            BonusHit -= toRemove.BonusHit;
            BonusDmg -= toRemove.BonusDmg;
            BonusAc -= toRemove.BonusAc;
            BonusMr -= toRemove.BonusMr;
            BonusRegen -= toRemove.BonusRegen;
            switch (toRemove.EquipmentSlot)
            {
                case (byte)ItemSlots.Necklace:
                    OffensiveElement = Element.None;
                    break;
                case (byte)ItemSlots.Waist:
                    DefensiveElement = Element.None;
                    break;
            }
        }

        public override void OnClick(User invoker)
        {
            var profilePacket = new ServerPacket(0x34);

            profilePacket.WriteUInt32(Id);

            foreach (var tuple in Equipment.GetEquipmentDisplayList())
            {
                profilePacket.WriteUInt16(tuple.Item1);
                profilePacket.WriteByte(tuple.Item2);
            }

            profilePacket.WriteByte((byte)GroupStatus);
            profilePacket.WriteString8(Name);
            profilePacket.WriteByte((byte)Citizenship.Flag);
            profilePacket.WriteString8(Title);
            profilePacket.WriteByte((byte)(Grouping ? 1 : 0));
            profilePacket.WriteString8(GuildRank);
            profilePacket.WriteString8(Hybrasyl.Constants.REVERSE_CLASSES[(int)Class]);
            profilePacket.WriteString8(Guild);
            profilePacket.WriteByte((byte)LegendMarks.Count);
            foreach (var mark in LegendMarks)
            {
                profilePacket.WriteByte((byte)mark.Icon);
                profilePacket.WriteByte((byte)mark.Color);
                profilePacket.WriteString8(mark.Prefix);
                profilePacket.WriteString8(mark.Text);
            }
            profilePacket.WriteUInt16((ushort)(PortraitData.Length + ProfileText.Length + 4));
            profilePacket.WriteUInt16((ushort)PortraitData.Length);
            profilePacket.Write(PortraitData);
            profilePacket.WriteString16(ProfileText);

            invoker.Enqueue(profilePacket);
        }

        private void SetValue(PropertyInfo info, object instance, object value)
        {
            try
            {
                Logger.DebugFormat("Setting property value {0} to {1}", info.Name, value.ToString());
                info.SetValue(instance, Convert.ChangeType(value, info.PropertyType));
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Exception trying to set {0} to {1}", info.Name, value.ToString());
                Logger.ErrorFormat(e.ToString());
                throw;
            }
        }

        public void Save()
        {
        }

        public override void SendMapInfo()
        {
            var x15 = new ServerPacket(0x15);
            x15.WriteUInt16(Map.Id);
            x15.WriteByte(Map.X);
            x15.WriteByte(Map.Y);
            x15.WriteByte(Map.Flags);
            x15.WriteUInt16(0);
            x15.WriteByte((byte)(Map.Checksum % 256));
            x15.WriteByte((byte)(Map.Checksum / 256));
            x15.WriteString8(Map.Name);
            Enqueue(x15);

            var x22 = new ServerPacket(0x22);
            x22.WriteByte(0x00);
            Enqueue(x22);

            if (Map.Music != 0xFF && Map.Music != CurrentMusicTrack)
            {
                SendMusic(Map.Music);
            }
            if (!string.IsNullOrEmpty(Map.Message))
            {
                SendMessage(Map.Message, 18);
            }
        }
        public override void SendLocation()
        {
            var x04 = new ServerPacket(0x04);
            x04.WriteUInt16(X);
            x04.WriteUInt16(Y);
            x04.WriteUInt16(11);
            x04.WriteUInt16(11);
            Enqueue(x04);
        }


        public void DisplayIncomingWhisper(String charname, String message)
        {
            Client.SendMessage(String.Format("{0}\" {1}", charname, message), 0x0);
        }

        public void DisplayOutgoingWhisper(String charname, String message)
        {
            Client.SendMessage(String.Format("{0}> {1}", charname, message), 0x0);
        }

        public void SendWhisper(String charname, String message)
        {
            if (IsMuted)
            {
                Client.SendMessage("A strange voice says, \"Not for you.\"", 0x0);
                return;
            }

            var target = World.FindUser(charname);

            if (target == null)
            {
                Client.SendMessage("That Aisling is not in Temuair.", 0x0);
                return;
            }

            if (target.IsIgnoringWhispers)
            {
                Client.SendMessage("Sadly, that Aisling cannot hear whispers.", 0x0);
                return;
            }

            DisplayOutgoingWhisper(target.Name, message);
            target.DisplayIncomingWhisper(Name, message);
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                SendUpdateToUser(user.Client);
            }
            else
            {
                if (obj is Item)
                {
                    var item = obj as Item;
                    SendVisibleItem(item);
                }
            }
        }

        public void SendVisibleGold(Gold gold)
        {
            Logger.DebugFormat("Sending add visible item packet");
            var x07 = new ServerPacket(0x07);
            x07.WriteUInt16(1);
            x07.WriteUInt16(gold.X);
            x07.WriteUInt16(gold.Y);
            x07.WriteUInt32(gold.Id);
            x07.WriteUInt16((ushort)(gold.Sprite + 0x8000));
            x07.WriteInt32(0);
            Enqueue(x07);
        }

        public void SendVisibleItem(Item item)
        {
            Logger.DebugFormat("Sending add visible item packet");
            var x07 = new ServerPacket(0x07);
            x07.WriteUInt16(1);
            x07.WriteUInt16(item.X);
            x07.WriteUInt16(item.Y);
            x07.WriteUInt32(item.Id);
            x07.WriteUInt16((ushort)(item.EquipSprite + 0x8000));
            x07.WriteInt32(0);
            Enqueue(x07);
        }

        public void SendUpdateToUser(Client client)
        {
            var x33 = new ServerPacket(0x33);
            var offset = (byte)(Equipment.Armor != null ? Equipment.Armor.BodyStyle : 0);


            x33.WriteUInt16(X);
            x33.WriteUInt16(Y);
            x33.WriteByte((byte)Direction);
            x33.WriteUInt32(Id);

            var helmat = (Equipment.Helmet != null ? Equipment.Helmet.DisplaySprite : HairStyle);
            helmat = (Equipment.DisplayHelm != null ? Equipment.DisplayHelm.DisplaySprite : helmat);

            x33.WriteUInt16((ushort)helmat);

            x33.WriteByte((byte)(((byte)Sex * 16) + offset));

            x33.WriteUInt16((ushort)(Equipment.Armor != null ? Equipment.Armor.DisplaySprite : 0));
            x33.WriteByte((byte)(Equipment.Boots != null ? Equipment.Boots.DisplaySprite : 0));
            x33.WriteUInt16((ushort)(Equipment.Armor != null ? Equipment.Armor.DisplaySprite : 0));
            x33.WriteByte((byte)(Equipment.Shield != null ? Equipment.Shield.DisplaySprite : 0));
            x33.WriteUInt16((ushort)(Equipment.Weapon != null ? Equipment.Weapon.DisplaySprite : 0));
            x33.WriteByte(HairColor);
            x33.WriteByte((byte)(Equipment.Boots != null ? Equipment.Boots.Color : 0));
            x33.WriteByte(0x00);
            x33.WriteUInt16((ushort)(Equipment.FirstAcc != null ? Equipment.FirstAcc.DisplaySprite : 0));
            x33.WriteByte(0x00);
            x33.WriteUInt16((ushort)(Equipment.SecondAcc != null ? Equipment.SecondAcc.DisplaySprite : 0));
            x33.WriteByte(0x00);
            x33.WriteUInt16((ushort)(Equipment.ThirdAcc != null ? Equipment.ThirdAcc.DisplaySprite : 0));
            x33.WriteByte(0x00);
            x33.WriteByte(0x00);
            x33.WriteUInt16((ushort)(Equipment.Overcoat != null ? Equipment.Overcoat.DisplaySprite : 0));
            x33.WriteByte(0x00);
            x33.WriteByte(0x00);
            x33.WriteByte(0x00);
            x33.WriteByte(0x00);
            x33.WriteByte(0x00);
            x33.WriteString8(Name);
            x33.WriteString8(string.Empty);
            client.Enqueue(x33);
        }

        public override void SendId()
        {
            var x05 = new ServerPacket(0x05);
            x05.WriteUInt32(Id);
            x05.WriteByte(1);
            x05.WriteByte(213);
            x05.WriteByte(0x00);
            x05.WriteUInt16(0x00);
            Enqueue(x05);
        }

        /// <summary>
        /// Sends an equip item packet to the client, triggering an update of the detail window ('a').
        /// </summary>
        /// <param name="item">The item which will be equipped.</param>
        /// <param name="slot">The slot in which we are equipping.</param>
        public void SendEquipItem(Item item, int slot)
        {
            if (item == null)
            {
                SendRefreshEquipmentSlot(slot);
                return;
            }

            var equipPacket = new ServerPacket(0x37);
            equipPacket.WriteByte((byte)slot);
            equipPacket.WriteUInt16((ushort)(item.EquipSprite + 0x8000));
            equipPacket.WriteByte(0x00);
            equipPacket.WriteStringWithLength(item.Name);
            equipPacket.WriteByte(0x00);
            equipPacket.WriteUInt32(item.MaximumDurability);
            equipPacket.WriteUInt32(item.Durability);
            Enqueue(equipPacket);
        }

        /// <summary>
        /// Sends a clear item packet to the connected client for the specified slot. 
        /// Because the slots on the client side start with one, decrement the slot before sending.
        /// </summary>
        /// <param name="slot">The client side slot to clear.</param>
        public void SendClearItem(int slot)
        {
            var x10 = new ServerPacket(0x10);
            x10.WriteByte((byte)slot);
            x10.WriteUInt16(0x0000);
            x10.WriteByte(0x00);
            Enqueue(x10);
        }

        /// <summary>
        /// Send an item update packet (essentially placing the item in a given slot, as far as the client is concerned.
        /// </summary>
        /// <param name="item">The item we are sending to the user.</param>
        /// <param name="slot">The client's item slot.</param>
        public void SendItemUpdate(Item item, int slot)
        {
            if (item == null)
            {
                SendClearItem(slot);
                return;
            }

            Logger.DebugFormat("Adding {0} qty {1} to slot {2}",
                item.Name, item.Count, slot);
            var x0F = new ServerPacket(0x0F);
            x0F.WriteByte((byte)slot);
            x0F.WriteUInt16((ushort)(item.Sprite + 0x8000));
            x0F.WriteByte(0x00);
            x0F.WriteString8(item.Name);
            x0F.WriteInt32(item.Count);
            x0F.WriteBoolean(item.Stackable);
            x0F.WriteUInt32(item.MaximumDurability);
            x0F.WriteUInt32(item.Durability);
            x0F.WriteUInt32(0x00);
            Enqueue(x0F);
        }

        public void SetFlag(String flag, String value)
        {
            UserFlags[flag] = value;
        }

        public void SetSessionFlag(String flag, String value)
        {
            UserSessionFlags[flag] = value;
        }

        public String GetFlag(String flag)
        {
            String value;
            if (UserFlags.TryGetValue(flag, out value))
            {
                return value;
            }
            return String.Empty;
        }


        public String GetSessionFlag(String flag)
        {
            String value;
            if (UserSessionFlags.TryGetValue(flag, out value))
            {
                return value;
            }
            return String.Empty;
        }

        public override void UpdateAttributes(StatUpdateFlags flags)
        {
            var x08 = new ServerPacket(0x08);
            x08.WriteByte((byte)flags);
            if (flags.HasFlag(StatUpdateFlags.Primary))
            {
                x08.Write(new byte[] { 1, 0, 0 });
                x08.WriteByte(Level);
                x08.WriteByte(Ability);
                x08.WriteUInt32(MaximumHp);
                x08.WriteUInt32(MaximumMp);
                x08.WriteByte(Str);
                x08.WriteByte(Int);
                x08.WriteByte(Wis);
                x08.WriteByte(Con);
                x08.WriteByte(Dex);
                if (LevelPoints > 0)
                {
                    x08.WriteByte(1);
                    x08.WriteByte((byte)LevelPoints);
                }
                else
                {
                    x08.WriteByte(0);
                    x08.WriteByte(0);
                }
                x08.WriteUInt16(MaximumWeight);
                x08.WriteUInt16(CurrentWeight);
                x08.WriteUInt32(uint.MinValue);
            }
            if (flags.HasFlag(StatUpdateFlags.Current))
            {
                x08.WriteUInt32(Hp);
                x08.WriteUInt32(Mp);
            }
            if (flags.HasFlag(StatUpdateFlags.Experience))
            {
                x08.WriteUInt32(Experience);
                x08.WriteUInt32(ExpToLevel);
                x08.WriteUInt32(AbilityExp);
                x08.WriteUInt32(0);
                x08.WriteUInt32(0);
                x08.WriteUInt32(Gold);
            }
            if (flags.HasFlag(StatUpdateFlags.Secondary))
            {
                x08.WriteUInt32(uint.MinValue);
                x08.WriteUInt16(ushort.MinValue);
                x08.WriteByte((byte)OffensiveElement);
                x08.WriteByte((byte)DefensiveElement);
                x08.WriteSByte(Mr);
                x08.WriteByte(0);
                x08.WriteSByte(Ac);
                x08.WriteByte(Dmg);
                x08.WriteByte(Hit);
            }
            Enqueue(x08);
        }

        public override bool Walk(Direction direction)
        {
            int oldX = X, oldY = Y, newX = X, newY = Y;
            Rectangle arrivingViewport = Rectangle.Empty;
            Rectangle departingViewport = Rectangle.Empty;
            Rectangle commonViewport = Rectangle.Empty;
            var halfViewport = Constants.VIEWPORT_SIZE / 2;

            switch (direction)
            {
                // Calculate the differences (which are, in all cases, rectangles of height 12 / width 1 or vice versa)
                // between the old and new viewpoints. The arrivingViewport represents the objects that need to be notified
                // of this object's arrival (because it is now within the viewport distance), and departingViewport represents
                // the reverse. We later use these rectangles to query the quadtree to locate the objects that need to be 
                // notified of an update to their AOI (area of interest, which is the object's viewport calculated from its
                // current position).

                case Direction.North:
                    --newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                    departingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                    break;
                case Direction.South:
                    ++newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                    departingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                    break;
                case Direction.West:
                    --newX;
                    arrivingViewport = new Rectangle(newX - halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    departingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    break;
                case Direction.East:
                    ++newX;
                    arrivingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    departingViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    break;
            }

            // Now that we know where we are going, perform some sanity checks.
            // Is the player trying to walk into a wall, or off the map?

            if (newX > Map.X || newY > Map.Y || newX < 0 || newY < 0)
            {
                Refresh();
                return false;
            }
            else
            {
                // Is the player trying to walk into an occupied tile?
                foreach (var obj in Map.GetTileContents((byte)newX, (byte)newY))
                {
                    Logger.DebugFormat("Collsion check: found obj {0}", obj.Name);
                    if (obj is Creature)
                    {
                        Logger.DebugFormat("Walking prohibited: found {0}", obj.Name);
                        Refresh();
                        return false;
                    }
                }
                // Is this user entering a forbidden (by level or otherwise) warp?
                foreach (var warp in Map.Warps)
                {
                    if (warp.X == newX && warp.Y == newY)
                    {
                        if (warp.MinimumLevel > Level)
                        {
                            Client.SendMessage("You're too afraid to even approach it!", 3);
                            Refresh();
                            return false;
                        }
                        else if (warp.MaximumLevel < Level)
                        {
                            Client.SendMessage("Your honor forbids you from entering.", 3);
                            Refresh();
                            return false;
                        }
                    }
                }
            }

            // Calculate the common viewport between the old and new position

            commonViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, Constants.VIEWPORT_SIZE, Constants.VIEWPORT_SIZE);
            commonViewport.Intersect(new Rectangle(newX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, Constants.VIEWPORT_SIZE));
            Logger.DebugFormat("Moving from {0},{1} to {2},{3}", oldX, oldY, newX, newY);
            Logger.DebugFormat("Arriving viewport is a rectangle starting at {0}, {1}", arrivingViewport.X, arrivingViewport.Y);
            Logger.DebugFormat("Departing viewport is a rectangle starting at {0}, {1}", departingViewport.X, departingViewport.Y);
            Logger.DebugFormat("Common viewport is a rectangle starting at {0}, {1} of size {2}, {3}", commonViewport.X,
                commonViewport.Y, commonViewport.Width, commonViewport.Height);

            X = (byte)newX;
            Y = (byte)newY;
            Direction = direction;

            // Transmit update to the moving client, as we are actually walking now

            var x0B = new ServerPacket(0x0B);
            x0B.WriteByte((byte)direction);
            x0B.WriteUInt16((byte)oldX);
            x0B.WriteUInt16((byte)oldY);
            x0B.WriteUInt16(0x0B);
            x0B.WriteUInt16(0x0B);
            x0B.WriteByte(0x01);
            Enqueue(x0B);

            var x32 = new ServerPacket(0x32);
            x32.WriteByte(0x00);
            Enqueue(x32);

            // Objects in the common viewport receive a "walk" (0x0C) packet
            // Objects in the arriving viewport receive a "show to" (0x33) packet
            // Objects in the departing viewport receive a "remove object" (0x0E) packet

            foreach (var obj in Map.EntityTree.GetObjects(commonViewport))
            {
                if (obj != this && obj is User)
                {

                    var user = obj as User;
                    Logger.DebugFormat("Sending walk packet for {0} to {1}", Name, user.Name);
                    var x0C = new ServerPacket(0x0C);
                    x0C.WriteUInt32(Id);
                    x0C.WriteUInt16((byte)oldX);
                    x0C.WriteUInt16((byte)oldY);
                    x0C.WriteByte((byte)direction);
                    x0C.WriteByte(0x00);
                    user.Enqueue(x0C);
                }
            }

            foreach (var obj in Map.EntityTree.GetObjects(arrivingViewport))
            {
                obj.AoiEntry(this);
                AoiEntry(obj);
            }

            foreach (var obj in Map.EntityTree.GetObjects(departingViewport))
            {
                obj.AoiDeparture(this);
                AoiDeparture(obj);
            }

            foreach (var warp in Map.Warps)
            {
                if (warp.X == newX && warp.Y == newY)
                {
                    // Spin a bit so the client actually animates into the frame as opposed
                    // to flashing
                    Thread.Sleep(250);
                    Teleport(warp.DestinationMap, warp.DestinationX, warp.DestinationY);
                    return false;
                }
            }

            // how about we do it like this instead

            var tupleKey = new Tuple<byte, byte>((byte)newX, (byte)newY);
            WorldWarp wwarp;

            if (Map.WorldWarps.TryGetValue(tupleKey, out wwarp))
            {
                Remove();
                SendWorldMap(wwarp.DestinationWorldMap);
                World.Maps[Hybrasyl.Constants.LAG_MAP].Insert(this, 5, 5, false);
                return false;
            }
            HasMoved = true;
            Map.EntityTree.Move(this);
            return true;
        }

        public bool AddGold(Gold gold)
        {
            return AddGold(gold.Amount);
        }
        public bool AddGold(uint amount)
        {
            if (Gold + amount > Constants.MAXIMUM_GOLD)
            {
                Client.SendMessage("You cannot carry any more gold.", 3);
                return false;
            }

            Logger.DebugFormat("Attempting to add {0} gold", amount);

            Gold += amount;

            UpdateAttributes(StatUpdateFlags.Experience);
            return true;
        }

        public bool RemoveGold(Gold gold)
        {
            return RemoveGold(gold.Amount);
        }
        public bool RemoveGold(uint amount)
        {
            Logger.DebugFormat("Removing {0} gold", amount);

            if (Gold < amount)
            {
                Logger.ErrorFormat("I don't have {0} gold. I only have {1}", amount, Gold);
                return false;
            }

            Gold -= amount;

            UpdateAttributes(StatUpdateFlags.Experience);
            return true;
        }

        public bool AddItem(Item item, bool updateWeight = true)
        {
            if (Inventory.IsFull)
            {
                SendSystemMessage("You cannot carry any more items.");
                Map.Insert(item, X, Y);
                return false;
            }
            return AddItem(item, Inventory.FindEmptySlot(), updateWeight);
        }

        public bool AddItem(Item item, byte slot, bool updateWeight = true)
        {
            if (item.Weight + CurrentWeight > MaximumWeight)
            {
                SendSystemMessage("It's too heavy.");
                Map.Insert(item, X, Y);
                return false;
            }

            var inventoryItem = Inventory.Find(item.Name);

            if (inventoryItem != null && item.Stackable)
            {
                if (item.Count + inventoryItem.Count > inventoryItem.MaximumStack)
                {
                    item.Count = (inventoryItem.Count + item.Count) - inventoryItem.MaximumStack;
                    inventoryItem.Count = inventoryItem.MaximumStack;
                    SendSystemMessage(String.Format("You can't carry any more {0}", item.Name));
                    Map.Insert(item, X, Y);
                    return false;
                }
                inventoryItem.Count += item.Count;
                item.Count = 0;
                SendItemUpdate(inventoryItem, Inventory.SlotOf(inventoryItem.Name));
                World.Remove(item);
                return true;
            }

            Logger.DebugFormat("Attempting to add item to inventory slot {0}", slot);


            if (!Inventory.Insert(slot, item))
            {
                Logger.DebugFormat("Slot was invalid or not null");
                Map.Insert(item, X, Y);
                return false;
            }

            SendItemUpdate(item, slot);
            if (updateWeight)
            {
                UpdateAttributes(StatUpdateFlags.Primary);
            }
            return true;
        }

        public bool RemoveItem(byte slot, bool updateWeight = true)
        {
            if (Inventory.Remove(slot))
            {
                SendClearItem(slot);
                if (updateWeight)
                {
                    UpdateAttributes(StatUpdateFlags.Primary);
                }
                return true;
            }

            return false;
        }

        public bool IncreaseItem(byte slot, int quantity)
        {
            if (Inventory.Increase(slot, quantity))
            {
                SendItemUpdate(Inventory[slot], slot);
                return true;
            }
            return false;
        }
        public bool DecreaseItem(byte slot, int quantity)
        {
            if (Inventory.Decrease(slot, quantity))
            {
                SendItemUpdate(Inventory[slot], slot);
                return true;
            }
            return false;
        }

        public bool AddEquipment(Item item, byte slot, bool sendUpdate = true)
        {
            Logger.DebugFormat("Adding equipment to slot {0}", slot);

            if (!Equipment.Insert(slot, item))
            {
                Logger.DebugFormat("Slot wasn't null, aborting");
                return false;
            }

            SendEquipItem(item, slot);
            Client.SendMessage(string.Format("Equipped {0}", item.Name), 3);
            ApplyBonuses(item);
            UpdateAttributes(StatUpdateFlags.Stats);
            if (sendUpdate)
            {
                Show();
            }
            return true;
        }
        public bool RemoveEquipment(byte slot, bool sendUpdate = true)
        {
            var item = Equipment[slot];
            if (Equipment.Remove(slot))
            {
                SendRefreshEquipmentSlot(slot);
                Client.SendMessage(string.Format("Unequipped {0}", item.Name), 3);
                RemoveBonuses(item);
                UpdateAttributes(StatUpdateFlags.Stats);
                if (sendUpdate)
                {
                    Show();
                }
                return true;
            }
            return false;
        }

        public void SendRefreshEquipmentSlot(int slot)
        {
            var refreshPacket = new ServerPacket(0x38);
            refreshPacket.WriteByte((byte)slot);
            Enqueue(refreshPacket);
        }

        public override void Refresh()
        {
            SendMapInfo();
            SendLocation();

            foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
            {
                AoiEntry(obj);
                obj.AoiEntry(this);
            }
        }

        public void SwapItem(byte oldSlot, byte newSlot)
        {
            Inventory.Swap(oldSlot, newSlot);
            SendItemUpdate(Inventory[oldSlot], oldSlot);
            SendItemUpdate(Inventory[newSlot], newSlot);
        }

        /// <summary>
        /// Send a player's profile to themselves (e.g. click on self or hit Y for group info)
        /// </summary>
        public void SendProfile()
        {
            var profilePacket = new ServerPacket(0x39);
            profilePacket.WriteByte((byte)Citizenship.Flag);
            profilePacket.WriteString8(GuildRank);
            profilePacket.WriteString8(Title);
            profilePacket.WriteString8(GroupText);
            profilePacket.WriteBoolean(Grouping);
            profilePacket.WriteByte(0);
            profilePacket.WriteByte((byte)Class);
            profilePacket.WriteByte(1);
            profilePacket.WriteByte(0);
            profilePacket.WriteString8(Hybrasyl.Constants.REVERSE_CLASSES[(int)Class]);
            profilePacket.WriteString8(Guild);
            profilePacket.WriteByte((byte)LegendMarks.Count);
            foreach (var mark in LegendMarks)
            {
                profilePacket.WriteByte((byte)mark.Icon);
                profilePacket.WriteByte((byte)mark.Color);
                profilePacket.WriteString8(mark.Prefix);
                profilePacket.WriteString8(mark.Text);
            }

            Enqueue(profilePacket);
        }

        /// <summary>
        /// Update a player's last login time in the database and the live object.
        /// </summary>
        public void UpdateLoginTime()
        {
            LoginTime = DateTime.Now;
        }

        /// <summary>
        /// Update a player's last logoff time in the database and the live object.
        /// </summary>
        public void UpdateLogoffTime()
        {
            LogoffTime = DateTime.Now;
        }

        public void SendWorldMap(WorldMap map)
        {
            var x2E = new ServerPacket(0x2E);
            x2E.Write(map.GetBytes());
            IsAtWorldMap = true;
            Enqueue(x2E);
        }

        public void SendMotion(uint id, byte motion, short speed)
        {
            var x1A = new ServerPacket(0x1A);
            x1A.WriteUInt32(id);
            x1A.WriteByte(motion);
            x1A.WriteInt16(speed);
            x1A.WriteByte(0xFF);
            Enqueue(x1A);
        }

        public void SendEffect(uint id, ushort effect, short speed)
        {
            var x29 = new ServerPacket(0x29);
            x29.WriteUInt32(id);
            x29.WriteUInt32(id);
            x29.WriteUInt16(effect);
            x29.WriteUInt16(ushort.MinValue);
            x29.WriteInt16(speed);
            x29.WriteByte(0x00);
            Enqueue(x29);
        }
        public void SendEffect(uint targetId, ushort targetEffect, uint srcId, ushort srcEffect, short speed)
        {
            var x29 = new ServerPacket(0x29);
            x29.WriteUInt32(targetId);
            x29.WriteUInt32(srcId);
            x29.WriteUInt16(targetEffect);
            x29.WriteUInt16(srcEffect);
            x29.WriteInt16(speed);
            x29.WriteByte(0x00);
            Enqueue(x29);
        }
        public void SendEffect(short x, short y, ushort effect, short speed)
        {
            var x29 = new ServerPacket(0x29);
            x29.WriteUInt32(uint.MinValue);
            x29.WriteUInt16(effect);
            x29.WriteInt16(speed);
            x29.WriteInt16(x);
            x29.WriteInt16(y);
            Enqueue(x29);
        }

        public void SendMusic(byte track)
        {
            CurrentMusicTrack = track;

            var x19 = new ServerPacket(0x19);
            x19.WriteByte(0xFF);
            x19.WriteByte(track);
            Enqueue(x19);
        }

        public void SendSound(byte sound)
        {
            var x19 = new ServerPacket(0x19);
            x19.WriteByte(sound);
            Enqueue(x19);
        }

        public void SendDoorUpdate(byte x, byte y, bool state, bool leftright)
        {
            var doorPacket = new ServerPacket(0x32);
            doorPacket.WriteByte(1);
            doorPacket.WriteByte(x);
            doorPacket.WriteByte(y);
            doorPacket.WriteBoolean(state);
            doorPacket.WriteBoolean(leftright);
            Enqueue(doorPacket);
        }

        public void ShowBuyMenu(Merchant merchant)
        {
            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x04);
            x2F.WriteByte(0x01);
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x00);
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16("What would you like to buy?");
            x2F.WriteUInt16((ushort)MerchantMenuItem.BuyItem);
            x2F.WriteUInt16((ushort)merchant.Inventory.Count);
            foreach (var item in merchant.Inventory.Values)
            {
                x2F.WriteUInt16((ushort)(0x8000 + item.Sprite));
                x2F.WriteByte((byte)item.Color);
                x2F.WriteUInt32((uint)item.Value);
                x2F.WriteString8(item.Name);
                x2F.WriteString8(string.Empty);
            }
            Enqueue(x2F);
        }

        public void ShowBuyMenuQuantity(Merchant merchant, string name)
        {
            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x03);
            x2F.WriteByte(0x01);
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x00);
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16(string.Format("How many {0} would you like to buy?", name));
            x2F.WriteString8(name);
            x2F.WriteUInt16((ushort)MerchantMenuItem.BuyItemQuantity);
            Enqueue(x2F);
        }

        public void ShowSellMenu(Merchant merchant)
        {
            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x05);
            x2F.WriteByte(0x01);
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x00);
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16("What would you like to sell?");
            x2F.WriteUInt16((ushort)MerchantMenuItem.SellItem);

            var position = x2F.Position;
            x2F.WriteByte(0);
            var count = 0;

            for (int i = 1; i <= Inventory.Size; ++i)
            {
                if (Inventory[(byte)i] == null || !merchant.Inventory.ContainsKey(Inventory[(byte)i].Name))
                {
                    continue;
                }
                x2F.WriteByte((byte)i);
                ++count;
            }

            x2F.Seek(position, PacketSeekOrigin.Begin);
            x2F.WriteByte((byte)count);

            Enqueue(x2F);
        }
        public void ShowSellQuantity(Merchant merchant, byte slot)
        {
            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x03);
            x2F.WriteByte(0x01);
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x00);
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16("How many are you selling?");
            x2F.WriteByte(1);
            x2F.WriteByte(slot);
            x2F.WriteUInt16((ushort)MerchantMenuItem.SellItemQuantity);
            Enqueue(x2F);
        }
        public void ShowSellConfirm(Merchant merchant, byte slot, int quantity)
        {
            var item = Inventory[slot];
            var offer = Math.Round(item.Value * 0.50) * quantity;

            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x01);
            x2F.WriteByte(0x01);
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x00);
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16(string.Format("I'll give you {0} gold for {1}.", offer, quantity == 1 ? "that" : "those"));
            x2F.WriteByte(2);
            x2F.WriteByte(slot);
            x2F.WriteByte((byte)quantity);
            x2F.WriteByte(2);
            x2F.WriteString8("Accept");
            x2F.WriteUInt16((ushort)MerchantMenuItem.SellItemAccept);
            x2F.WriteString8("Decline");
            x2F.WriteUInt16((ushort)MerchantMenuItem.SellItemMenu);
            Enqueue(x2F);
        }

        public void ShowMerchantGoBack(Merchant merchant, string message, MerchantMenuItem menuItem = MerchantMenuItem.MainMenu)
        {
            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x01);
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x01);
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00);
            x2F.WriteByte(0x00);
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16(message);
            x2F.WriteByte(1);
            x2F.WriteString8("Go back");
            x2F.WriteUInt16((ushort)menuItem);
            Enqueue(x2F);
        }

        public void SendMessage(string message, byte type)
        {
            var x0A = new ServerPacket(0x0A);
            x0A.WriteByte(type);
            x0A.WriteString16(message);
            Enqueue(x0A);
        }

        public void SendWorldMessage(string sender, string message)
        {
            var x0A = new ServerPacket(0x0A);
            x0A.WriteByte(0x00);
            var transmit = String.Format("{{=c[{0}] {1}", sender, message);
            if (transmit.Length > 67)
            {
                transmit = transmit.Substring(0, 67);
            }
            x0A.WriteString16(transmit);
            Enqueue(x0A);
        }

        public void SendRedirectAndLogoff(World world, Login login, string name)
        {
            Client.Redirect(new Redirect(Client, world, Game.Login, name, Client.EncryptionSeed, Client.EncryptionKey));
            GlobalConnectionManifest.DeregisterClient(Client);
        }

        public bool IsHeartbeatValid(byte a, byte b)
        {
            return Client.IsHeartbeatValid(a, b);
        }

        public bool IsHeartbeatValid(int localTickCount, int clientTickCount)
        {
            return Client.IsHeartbeatValid(localTickCount, clientTickCount);
        }

        public bool IsHeartbeatExpired()
        {
            return Client.IsHeartbeatExpired();
        }

        public void Logoff()
        {
            UpdateLogoffTime();
            Client.Disconnect();
        }

        public void SetEncryptionParameters(byte[] key, byte seed, string name)
        {
            Client.EncryptionKey = key;
            Client.EncryptionSeed = seed;
            Client.GenerateKeyTable(name);
        }

        /// <summary>
        /// Send an exchange initiation request to the client (open exchange window)
        /// </summary>
        /// <param name="requestor">The user requesting the trade</param>
        public void SendExchangeInitiation(User requestor)
        {
            if (Status.HasFlag(PlayerStatus.InExchange) && requestor.Status.HasFlag(PlayerStatus.InExchange))
            {
                var x42 = new ServerPacket(0x42);
                x42.WriteByte(0);
                x42.WriteUInt32(requestor.Id);
                x42.WriteString8(requestor.Name);
                Enqueue(x42);
            }
        }

        /// <summary>
        /// Send a quantity prompt request to the client (when dealing with stacked items)
        /// </summary>
        /// <param name="itemSlot">The item slot containing a stacked item that will be split (client side)</param>
        public void SendExchangeQuantityPrompt(byte itemSlot)
        {
            if (Status.HasFlag(PlayerStatus.InExchange))
            {
                var x42 = new ServerPacket(0x42);
                x42.WriteByte(1);
                x42.WriteByte(itemSlot);
                Enqueue(x42);
            }
        }
        /// <summary>
        /// Send an exchange update packet for an item to an active exchange participant.
        /// </summary>
        /// <param name="toAdd">Item to add to the exchange window</param>
        /// <param name="slot">Byte indicating the exchange window slot to be updated</param>
        /// <param name="source">Boolean indicating which "side" of the transaction will be updated (source / "left side" == true)</param>
        public void SendExchangeUpdate(Item toAdd, byte slot, bool source = true)
        {
            if (Status.HasFlag(PlayerStatus.InExchange))
            {
                var x42 = new ServerPacket(0x42);
                x42.WriteByte(2);
                x42.WriteByte((byte)(source ? 0 : 1));
                x42.WriteByte(slot);
                x42.WriteUInt16((ushort)(0x8000 + toAdd.Sprite));
                x42.WriteByte(toAdd.Color);
                x42.WriteString8(toAdd.Name);
                Enqueue(x42);
            }
        }

        /// <summary>
        /// Send an exchange update packet for gold to an active exchange participant.
        /// </summary>
        /// <param name="gold">The amount of gold to be added to the window.</param>
        /// <param name="source">Boolean indicating which "side" of the transaction will be updated (source / "left side" == true)</param>
        public void SendExchangeUpdate(uint gold, bool source = true)
        {
            if (Status.HasFlag(PlayerStatus.InExchange))
            {
                var x42 = new ServerPacket(0x42);
                x42.WriteByte(3);
                x42.WriteByte((byte)(source ? 0 : 1));
                x42.WriteUInt32(gold);
                Enqueue(x42);
            }
        }

        /// <summary>
        /// Send a cancellation notice for an exchange.
        /// </summary>
        /// <param name="source">The "side" responsible for cancellation (source / "left side" == true)</param>
        public void SendExchangeCancellation(bool source = true)
        {
            if (Status.HasFlag(PlayerStatus.InExchange))
            {
                var x42 = new ServerPacket(0x42);
                x42.WriteByte(4);
                x42.WriteByte((byte)(source ? 0 : 1));
                x42.WriteString8("Exchange was cancelled.");
                Enqueue(x42);
            }
        }

        /// <summary>
        /// Send a confirmation notice for an exchange.
        /// </summary>
        /// <param name="source">The "side" responsible for confirmation (source / "left side" == true)</param>
        public void SendExchangeConfirmation(bool source = true)
        {
            if (Status.HasFlag(PlayerStatus.InExchange))
            {
                var x42 = new ServerPacket(0x42);
                x42.WriteByte(5);
                x42.WriteByte((byte)(source ? 0 : 1));
                x42.WriteString8("You exchanged.");
                Enqueue(x42);
            }
        }

        public bool IsInViewport(VisibleObject obj)
        {
            return Map.EntityTree.GetObjects(GetViewport()).Contains(obj);
        }


        public void SendSystemMessage(string p)
        {
            Client.SendMessage(p, 3);
        }

        public DateTime LastAssail { get; set; }

        internal bool LoadFromDB(bool updateInventory = false)
        {
            using (var ctx = new DB())
            {
                var playerquery = ctx.players.Where(player => player.Name.ToLower().Equals(Name.ToLower())).SingleOrDefault();
                if (playerquery == null)
                {
                    return false;
                }

                //Load Player Information
                X = (byte)playerquery.Map_x;
                Y = (byte)playerquery.Map_y;
                MapX  = (byte)playerquery.Map_x;
                MapY  = (byte)playerquery.Map_y;
                MapId = (ushort)playerquery.Map_id;
                Map = World.Maps[MapId];

                BaseHp = (uint)playerquery.Cur_hp;
                BaseMp = (uint)playerquery.Cur_mp;
                BaseStr = (byte)playerquery.Str;
                BaseInt = (byte)playerquery.Int;
                BaseWis = (byte)playerquery.Wis;
                BaseCon = (byte)playerquery.Con;
                BaseDex = (byte)playerquery.Dex;

                Ability = (byte)playerquery.Ab;
                AbilityExp = (uint)playerquery.Ab_exp;
                Class = (Enums.Class)playerquery.Class_type;
                Account = ctx.accounts.Where(i => i.Id == playerquery.Account_id).FirstOrDefault();
                Name = playerquery.Name;
                Sex = (Enums.Sex)playerquery.Sex;
                HairColor = (byte)playerquery.Haircolor;
                HairStyle = (byte)playerquery.Hairstyle;
                Level     = (byte)playerquery.Level;
                LevelPoints = 0;
                Experience = (uint)playerquery.Exp;
                Gold = (uint)playerquery.Gold;

                //get flags
                Flags = ctx.GetAll<flags>().ToDictionary(v => v.Name, v => true);

                //nations
                var nation = ctx.nations.FirstOrDefault(i => i.Id == playerquery.Nation_id);

                if (nation != null)
                    Citizenship = nation;
                else Citizenship = World.Nations[Hybrasyl.Constants.DEFAULT_CITIZENSHIP];
 

                LogoffTime = playerquery.Last_logoff ?? DateTime.Now;

                // Legend marks
                LegendMarks = ctx.GetAll<legend_marks>().Where(i => i.Player_id == playerquery.Id).ToList();

                // Are we updating inventory & equipment?
                if (updateInventory)
                {
                    var inventory = JArray.Parse((string)playerquery.Inventory);
                    var equipment = JArray.Parse((string)playerquery.Equipment);

                    foreach (var obj in inventory)
                    {
                        Logger.DebugFormat("Inventory found");
                        PrettyPrinter.PrettyPrint(obj);

                        var itemId = (int)obj["item_id"];
                        int itemSlot = (int)obj["slot"];
                        var variantId = obj.Value<int?>("variant_id") ?? -1;

                        if (variantId > 0)
                            itemId = ctx.item_variants.Where(i => i.Id == variantId).FirstOrDefault().Id;
                                
                                //Game.World.Items[itemId].Variants[variantId].id;

                        var item = Game.World.CreateItem(itemId);
                        
                        if (item != null)
                        {
                            item.Count = obj.Value<int?>("count") ?? 1;
                            Game.World.Insert(item);
                            AddItem(item, (byte)itemSlot);
                        }
                        Logger.DebugFormat("Item is {0}", itemId);
                    }

                    foreach (var obj in equipment)
                    {
                        var itemId = (int)obj["item_id"];
                        var itemSlot = (int)obj["slot"];
                        var variantId = obj.Value<int?>("variant_id") ?? -1;

                        if (variantId > 0)
                            itemId = ctx.item_variants.Where(i => i.Id == variantId).FirstOrDefault().Id;

                        var item = Game.World.CreateItem(itemId);

                        if (item != null)
                        {
                            Logger.DebugFormat("Adding equipment: {0} to {1}", item.Name, itemSlot);
                            Game.World.Insert(item);
                            AddEquipment(item, (byte)itemSlot, false);
                        }
                        Logger.DebugFormat("Equipment is {0}", itemId);
                    }
                }
            }
            return true;
        }
    }
}
