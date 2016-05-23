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

using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using Hybrasyl.XSD;

namespace Hybrasyl.Objects
{

    [JsonObject]
    public class LegendMark
    {
        public String Prefix { get; set; }
        public int Color { get; set; }
        public int Icon { get; set; }
        public String Text { get; set; }
        public bool Public { get; set; }
        public DateTime Created { get; set; }
    }

    [JsonObject]
    public class GuildMembership
    {
        public String Title { get; set; }
        public String Name { get; set; }
        public String Rank { get; set; }
    }

    [JsonObject]
    public class Location
    {
        public ushort MapId { get; set; }
        public Direction Direction { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public bool WorldMap { get; set; }
    }

    [JsonObject]
    public class PasswordInfo
    {
        public String Hash { get; set; }
        public DateTime LastChanged { get; set; }
        public String LastChangedFrom { get; set; }
    }

    [JsonObject]
    public class LoginInfo
    {
        public DateTime LastLogin { get; set; }
        public DateTime LastLogoff { get; set; }
        public DateTime LastLoginFailure { get; set; }
        public String LastLoginFrom { get; set; }
        public Int64 LoginFailureCount { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class User : Creature
    {
        public bool IsSaving { get; set; }

        public new static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static string GetStorageKey(string name)
        {
            return string.Concat(typeof(User).Name, ':', name.ToLower());
        }

        public string StorageKey => string.Concat(GetType().Name, ':', Name.ToLower());
        private Client Client { get; set; }

        [JsonProperty]
        public Sex Sex { get; set; }
        //private account Account { get; set; }
        [JsonProperty]
        public byte HairStyle { get; set; }
        [JsonProperty]
        public byte HairColor { get; set; }
        [JsonProperty]
        public Enums.Class Class { get; set; }
        [JsonProperty]
        public bool IsMaster { get; set; }
        public UserGroup Group { get; set; }
        [JsonProperty]
        public bool Dead { get; set; }

        public Mailbox Mailbox => World.GetMailbox(Name);
        public bool UnreadMail => Mailbox.HasUnreadMessages;

        // Some structs helping us to define various metadata 
        [JsonProperty]
        public Location Location { get; set; }
        [JsonProperty]
        public LoginInfo Login { get; set; }
        [JsonProperty]
        public PasswordInfo Password { get; set; }
        [JsonProperty]
        public Book SkillBook { get; private set; }
        [JsonProperty]
        public Book SpellBook { get; private set; }

        [JsonProperty]
        public bool Grouping { get; set; }
        public UserStatus GroupStatus { get; set; }
        public byte[] PortraitData { get; set; }
        public string ProfileText { get; set; }

        [JsonProperty]
        public GuildMembership Guild { get; set; }

        private Nation _nation;

        public Nation Nation
        {
            get {
                return _nation ?? World.DefaultNation;
            }
            set
            {
                _nation = value;
                Citizenship = value.Name;
            }
        }

        [JsonProperty]
        private string Citizenship { get; set; }

        public string NationName
        {
            get
            {
                return Nation != null ? Nation.Name : string.Empty;
            }
        } 
        [JsonProperty]
        public List<LegendMark> Legend { get; set; }

        public DialogState DialogState { get; set; }

        [JsonProperty]
        private Dictionary<String, String> UserFlags { get; set; }
        private Dictionary<String, String> UserSessionFlags { get; set; }
        
        public Exchange ActiveExchange { get; set; }
        public PlayerStatus Status { get; set; }

        public bool IsAvailableForExchange
        {
            get { return Status == PlayerStatus.Alive; }
        }

        public uint ExpToLevel
        {
            get
            {
                if (Level == 99)
                    return 0;
                else
                    return (uint) (Math.Pow(Level, 3) * 250 - Experience);
            }
        }

        [JsonProperty]
        public uint LevelPoints = 0;

        public byte CurrentMusicTrack { get; set; }

        public void SetCitizenship()
        {
            if (Citizenship != null)
            {
                Nation theNation;
                Nation = World.Nations.TryGetValue(Citizenship, out theNation) ? theNation : World.DefaultNation;
            }
        }

        public bool IsPrivileged
        {
            get
            {
                if (Game.Config.Access.Privileged != null)
                {
                    return IsExempt || Flags.ContainsKey("gamemaster") || Game.Config.Access.Privileged.Contains(Name);
                }
                return IsExempt || Flags.ContainsKey("gamemaster");
            }
        }

        public bool IsExempt
        {
            get
            {
                // This is hax, obvs, and so can you
                return Name == "Kedian"; // ||(Account != null && Account.email == "baughj@discordians.net");
            }
        }

        public double SinceLastLogin
        {
            get
            {
                var span = (Login.LastLogin - Login.LastLogoff);
                return span.TotalSeconds < 0 ? 0 : span.TotalSeconds;
            }
        }

        // Throttling checks for messaging

        public long LastSpoke { get; set; }
        public string LastSaid { get; set; }
        public int NumSaidRepeated { get; set; }

        // Throttling checks for messaging
        public long LastBoardMessageSent { get; set; }
        public long LastMailboxMessageSent { get; set; }
        public Dictionary<string, bool> Flags { get; private set; }

        public DateTime LastAttack { get; set; }

        public bool Grouped
        {
            get { return Group != null; }
        }

    	[JsonProperty]
        public bool IsMuted { get; set; }
        [JsonProperty]
        public bool IsIgnoringWhispers { get; set; }
        [JsonProperty]
	    public bool IsAtWorldMap { get; set; }

        public void Enqueue(ServerPacket packet)
        {
            Logger.DebugFormat("Sending {0:X2} to {1}", packet.Opcode,Name);
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

        public string GroupText
        {
            get
            {
                // This also eventually needs to consider marriages
                if (Grouping)
                    return "Grouped!";
                return "Adventuring Alone";
            }
        }

        /**
         * Returns the current weight as perceived by the client. The actual inventory or equipment
         * weight may be less than zero, but this method will never return a negative value (negative
         * values will appear as zero as the client expects).
         */

        public ushort VisibleWeight
        {
            get { return (ushort) Math.Max(0, CurrentWeight); }
        }

        /**
         * Returns the true weight of the user's inventory + equipment, which could be negative.
         * Note that you should use VisibleWeight when communicating with the client since negative
         * weights should be invisible to users.
         */
        public int CurrentWeight
        {
            get { return (Inventory.Weight + Equipment.Weight); }
        }

        public ushort MaximumWeight
        {
            get { return (ushort) (BaseStr + Level/4 + 48); }
        }

        public bool VerifyPassword(String password)
        {
             return BCrypt.Net.BCrypt.Verify(password, Password.Hash);
        }

        public User()
        {
            _initializeUser();
        }

        private void _initializeUser(string playername = "")
        {
            Inventory = new Inventory(59);
            Equipment = new Inventory(18);
            SkillBook = new Book(90);
            SpellBook = new Book(90);
            IsAtWorldMap = false;
            Login = new LoginInfo();
            Password = new PasswordInfo();
            Location = new Location();
            Legend = new List<LegendMark>();
            Guild = new GuildMembership();
            LastSaid = String.Empty;
            LastSpoke = 0;
            NumSaidRepeated = 0;
            PortraitData = new byte[0];
            ProfileText = string.Empty;
            DialogState = new DialogState(this);
            UserFlags = new Dictionary<String, String>();
            UserSessionFlags = new Dictionary<String, String>();
            Status = PlayerStatus.Alive;
            Group = null;
            Flags = new Dictionary<string, bool>();
        }
        /**
         * Invites another user to this user's group. If this user isn't in a group,
         * create a new one.
         */
        public bool InviteToGroup(User invitee)
        {
            // If you're inviting others to group, you must have grouping enabled.
            // Enable it automatically if necessary.
            Grouping = true;

            if (!Grouped)
            {
                Group = new UserGroup(this);
            }

            return Group.Add(invitee);
        }

        /**
         * Distributes experience to a group if the user is in one, or to the
         * user directly if the user is ungrouped.
         */
        public void ShareExperience(uint exp)
        {
            if (Group != null)
            {
                Group.ShareExperience(this, (int)exp);
            }
            else
            {
                GiveExperience(exp);
            }
        }

        /**
         * Provides experience directly to the user that will not be distributed to
         * other members of the group (for example, for finishing a part of a quest).
         */
        public void GiveExperience(uint exp)
        {
            if (Level == 99 || exp < ExpToLevel)
            {
                Experience += exp;
            }
            else
            {
                // Apply one Level at a time

                var levelsGained = 0;
                Random random = new Random();

                while (exp > 0)
                {
                    uint expChunk = Math.Min(exp, ExpToLevel);

                    exp -= expChunk;
                    Experience += expChunk;

                    if (ExpToLevel == 0)
                    {
                        levelsGained++;
                        Level++;
                        LevelPoints = LevelPoints + 2;

                        #region Add Hp and Mp for each level gained

                        int hpGain = 0;
                        int mpGain = 0;
                        int bonusHp = 0;
                        int bonusMp = 0;
                        
                        double levelCircleModifier;  // Users get more Hp and Mp per level at higher Level "circles"

                        if (Level < LevelCircles.CIRCLE_1)
                        {
                            levelCircleModifier = StatGainConstants.LEVEL_CIRCLE_GAIN_MODIFIER_0;
                        }
                        else if (Level < LevelCircles.CIRCLE_2)
                        {
                            levelCircleModifier = StatGainConstants.LEVEL_CIRCLE_GAIN_MODIFIER_1;
                        }
                        else if (Level < LevelCircles.CIRCLE_3)
                        {
                            levelCircleModifier = StatGainConstants.LEVEL_CIRCLE_GAIN_MODIFIER_2;
                        }
                        else if (Level < LevelCircles.CIRCLE_4)
                        {
                            levelCircleModifier = StatGainConstants.LEVEL_CIRCLE_GAIN_MODIFIER_3;
                        }
                        else
                        {
                            levelCircleModifier = StatGainConstants.LEVEL_CIRCLE_GAIN_MODIFIER_4;
                        }

                        switch (Class)
                        {
                            case Enums.Class.Peasant:
                                hpGain = StatGainConstants.PEASANT_BASE_HP_GAIN;
                                mpGain = StatGainConstants.PEASANT_BASE_MP_GAIN;
                                bonusHp = StatGainConstants.PEASANT_BONUS_HP_GAIN;
                                bonusMp = StatGainConstants.PEASANT_BONUS_MP_GAIN;
                                break;

                            case Enums.Class.Warrior:
                                hpGain = StatGainConstants.WARRIOR_BASE_HP_GAIN;
                                mpGain = StatGainConstants.WARRIOR_BASE_MP_GAIN;
                                bonusHp = StatGainConstants.WARRIOR_BONUS_HP_GAIN;
                                bonusMp = StatGainConstants.WARRIOR_BONUS_MP_GAIN;
                                break;

                            case Enums.Class.Rogue:
                                hpGain = StatGainConstants.ROGUE_BASE_HP_GAIN;
                                mpGain = StatGainConstants.ROGUE_BASE_MP_GAIN;
                                bonusHp = StatGainConstants.ROGUE_BONUS_HP_GAIN;
                                bonusMp = StatGainConstants.ROGUE_BONUS_MP_GAIN;
                                break;

                            case Enums.Class.Monk:
                                hpGain = StatGainConstants.MONK_BASE_HP_GAIN;
                                mpGain = StatGainConstants.MONK_BASE_MP_GAIN;
                                bonusHp = StatGainConstants.MONK_BONUS_HP_GAIN;
                                bonusMp = StatGainConstants.MONK_BONUS_MP_GAIN;
                                break;

                            case Enums.Class.Priest:
                                hpGain = StatGainConstants.PRIEST_BASE_HP_GAIN;
                                mpGain = StatGainConstants.PRIEST_BASE_MP_GAIN;
                                bonusHp = StatGainConstants.PRIEST_BONUS_HP_GAIN;
                                bonusMp = StatGainConstants.PRIEST_BONUS_MP_GAIN;
                                break;

                            case Enums.Class.Wizard:
                                hpGain = StatGainConstants.WIZARD_BASE_HP_GAIN;
                                mpGain = StatGainConstants.WIZARD_BASE_MP_GAIN;
                                bonusHp = StatGainConstants.WIZARD_BONUS_HP_GAIN;
                                bonusMp = StatGainConstants.WIZARD_BONUS_MP_GAIN;
                                break;
                        }

                        // Each level, a user is guaranteed to increase his hp and mp by some base amount, per his Class.
                        // His hp and mp will increase further by a "bonus amount" that is accounted for by:
                        // - 50% Level circle
                        // - 50% Randomness
                        
                        int bonusHpGain = (int)Math.Round(bonusHp * 0.5 * levelCircleModifier + bonusHp * 0.5 * random.NextDouble(), MidpointRounding.AwayFromZero);
                        int bonusMpGain = (int)Math.Round(bonusMp * 0.5 * levelCircleModifier + bonusMp * 0.5 * random.NextDouble(), MidpointRounding.AwayFromZero);

                        BaseHp += (hpGain + bonusHpGain);
                        BaseMp += (mpGain + bonusMpGain);

                        #endregion
                    }
                }

                if (levelsGained > 0)
                {
                    Client.SendMessage("A rush of insight fills you!", MessageTypes.SYSTEM);
                    Effect(50, 250);
                    UpdateAttributes(StatUpdateFlags.Full);
                }
            }        

            UpdateAttributes(StatUpdateFlags.Experience);

        }

        public void TakeExperience(uint exp)
        {
            
        }

        public bool AssociateConnection(World world, long connectionId)
        {
            World = world;
            Client client;
            if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(connectionId, out client)) return false;
            Client = client;
            return true;
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

        public User(string playername, Sex sex, ushort targetMap, byte targetX, byte targetY)
        {
            Name = playername;
            Sex = sex;
            Location = new Location {MapId = targetMap, WorldMap = false, X = targetX, Y = targetY};
            _initializeUser(playername);
        }

        /// <summary>
        /// Given a specified item, apply the given bonuses to the player.
        /// </summary>
        /// <param name="toApply">The item used to calculate bonuses.</param>
        public void ApplyBonuses(Item toApply)
        {
            // Given an item, set our bonuses appropriately.
            // We might want to do this with reflection eventually?
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
                case (byte) ItemSlots.Necklace:
                    OffensiveElement = toApply.Element;
                    break;
                case (byte) ItemSlots.Waist:
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
                        Client.Disconnect();
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
                case (byte) ItemSlots.Necklace:
                    OffensiveElement = Enums.Element.None;
                    break;
                case (byte) ItemSlots.Waist:
                    DefensiveElement = Enums.Element.None;
                    break;
            }

        }

        public override void OnClick(User invoker)
        {

            // Return a profile packet (0x34) to the user who clicked.
            // This packet format is:
            // uint32 id, 18 equipment slots (uint16 sprite, byte color), byte namelength, string name,
            // byte nation, byte titlelength, string title, byte grouping, byte guildranklength, string guildrank,
            // byte classnamelength, string classname, byte guildnamelength, byte guildname, byte numLegendMarks (lame!),
            // numLegendMarks[byte icon, byte color, byte marklength, string mark]
            // This packet can also contain a portrait and profile text but we haven't even remotely implemented it yet.

            var profilePacket = new ServerPacket(0x34);

            profilePacket.WriteUInt32(Id);

            // Equipment block is 3 bytes per slot and contains 54 bytes (18 slots), which I believe is sprite+color
            // EXCEPT WHEN IT'S MUNGED IN SOME OBSCURE WAY BECAUSE REASONS
            foreach (var tuple in Equipment.GetEquipmentDisplayList())
            {
                profilePacket.WriteUInt16(tuple.Item1);
                profilePacket.WriteByte(tuple.Item2);
            }

            profilePacket.WriteByte((byte)GroupStatus);
            profilePacket.WriteString8(Name);
            profilePacket.WriteByte((byte)Nation.Flag); // This should pull from town / nation
            profilePacket.WriteString8(Guild.Title);
            profilePacket.WriteByte((byte)(Grouping ? 1 : 0));
            profilePacket.WriteString8(Guild.Rank);
            profilePacket.WriteString8(Hybrasyl.Constants.REVERSE_CLASSES[(int)Class]);
            profilePacket.WriteString8(Guild.Name);
            profilePacket.WriteByte((byte)Legend.Count);
            foreach (var mark in Legend)
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
            if (!IsSaving)
            {
                IsSaving = true;
                // Save location
                if (IsAtWorldMap)
                    Location.WorldMap = true;
                else if (Map != null)
                {
                    Location.MapId = Map.Id;
                    Location.X = X;
                    Location.Y = Y;
                }

                var cache = World.DatastoreConnection.GetDatabase();
                cache.Set(GetStorageKey(Name), JsonConvert.SerializeObject(this, new JsonSerializerSettings() { PreserveReferencesHandling = PreserveReferencesHandling.All }));
                IsSaving = false;
            }
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

            if (Map.Music != 0xFF && Map.Music != CurrentMusicTrack) SendMusic(Map.Music);
            if (!string.IsNullOrEmpty(Map.Message)) SendMessage(Map.Message, 18);
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
            var target = World.FindUser(charname);
            string err = String.Empty;

            if (CanTalkTo(target, out err))
            {
                // To implement: ACLs (ignore list)
                // To implement: loggging?
                DisplayOutgoingWhisper(target.Name, message);
                target.DisplayIncomingWhisper(Name, message);
            }
            else
            {
                Client.SendMessage(err, 0x0);
            }
        }

        /**
         * Send a whisper to all members of the group.
         */
        public void SendGroupWhisper(string message)
        {
            if (Group == null)
            {
                SendMessage("You must be in a group to group whisper.", MessageTypes.SYSTEM);
            }
            else
            {
                string err = String.Empty;
                foreach (var member in Group.Members)
                {
                    if (CanTalkTo(member, out err))
                    {
                        member.Client.SendMessage(String.Format("[!{0}] {1}", Name, message), MessageTypes.GROUP);
                    }
                    else
                    {
                        Client.SendMessage(err, 0x0);
                    }
                }
            }
        }

        public bool CanTalkTo(User target, out string msg)
        {
            // First, maake sure a) we can send a message and b) the target is not ignoring whispers.
            if (IsMuted)
            {
                msg = "A strange voice says, \"Not for you.\"";
                return false;
            }

            if (target == null)
            {
                msg = "That Aisling is not in Temuair.";
                return false;
            }

            if (target.IsIgnoringWhispers)
            {
                msg = "Sadly, that Aisling cannot hear whispers.";
                return false;
            }

            msg = String.Empty;
            return true;
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                SendUpdateToUser(user.Client);
            }
            else if (obj is Item)
            {
                var item = obj as Item;
                SendVisibleItem(item);

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
            x07.DumpPacket();
            Enqueue(x07);
        }

        internal void UseSkill(byte slot)
        {
            var castable = SkillBook[slot];

            if(castable.Effects.Damage != null)
            {
                //byte radius = castable.Intents.Intent.Where(x => x.;
                Direction playerFacing = this.Direction;
                byte maxTargets = 0;
                //this is an attack skill
                
                //now lets define how we want to do the attack
                //isclick should always be false for a skill (please correct me if I'm wrong)
                
            }
        }

        public void SendVisibleItem(Item item)
        {
            Logger.DebugFormat("Sending add visible item packet");
            var x07 = new ServerPacket(0x07);
            x07.WriteUInt16(1); // Anything but 0x0001 does nothing or makes client crash
            x07.WriteUInt16(item.X);
            x07.WriteUInt16(item.Y);
            x07.WriteUInt32(item.Id);
            x07.WriteUInt16((ushort)(item.Sprite + 0x8000));
            x07.WriteInt32(0); // Unknown what this is
            x07.DumpPacket();
            Enqueue(x07);
        }

        public void SendVisibleCreature(Creature creature)
        {
            Logger.DebugFormat("Sending add visible creature packet");
            var x07 = new ServerPacket(0x07);
            x07.WriteUInt16(1); // Anything but 0x0001 does nothing or makes client crash
            x07.WriteUInt16(creature.X);
            x07.WriteUInt16(creature.Y);
            x07.WriteUInt32(creature.Id);
            x07.WriteUInt16((ushort)(creature.Sprite + 0x4000));
            x07.WriteInt32(0); // Unknown what this is
            x07.DumpPacket();
            Enqueue(x07);
        }

        public void SendUpdateToUser(Client client)
        {
            // 0x33 <X> <Y> <Direction> <Player ID> <hat/hairstyle> <Offset for sex/status (includes dead/etc)>
            // <armor sprite> <boots> <armor sprite> <shield> <weapon> <hair color> <boot color> <acc1 color> <acc1>
            // <acc2 color> <acc2> <acc3 color> <acc3> <nfi> <nfi> <overcoat> <overcoat color> <skin color> <transparency>
            // <face> <name style (see Enums.NameStyles)> <name length> <name> <group name length> <group name> (shows up as hovering clickable bar)
            var x33 = new ServerPacket(0x33);
            byte offset = (byte)(Equipment.Armor != null ? Equipment.Armor.BodyStyle : 0);


            x33.WriteUInt16(X);
            x33.WriteUInt16(Y);
            x33.WriteByte((byte)Direction);
            x33.WriteUInt32(Id);

            // hacky until we rewrite it correctly
            var helmat = (Equipment.Helmet != null ? Equipment.Helmet.DisplaySprite : HairStyle);
            helmat = (Equipment.DisplayHelm != null ? Equipment.DisplayHelm.DisplaySprite : helmat);

            x33.WriteUInt16((ushort)helmat);

            // shit like this is really pissing me off
            x33.WriteByte((byte)(((byte)Sex * 16) + offset));

            x33.WriteUInt16((ushort)(Equipment.Armor != null ? Equipment.Armor.DisplaySprite : 0));
            x33.WriteByte((byte)(Equipment.Boots != null ? Equipment.Boots.DisplaySprite : 0));
            x33.WriteUInt16((ushort)(Equipment.Armor != null ? Equipment.Armor.DisplaySprite : 0));
            x33.WriteByte((byte)(Equipment.Shield != null ? Equipment.Shield.DisplaySprite : 0));
            x33.WriteUInt16((ushort)(Equipment.Weapon != null ? Equipment.Weapon.DisplaySprite : 0));
            x33.WriteByte(HairColor);
            x33.WriteByte((byte)(Equipment.Boots != null ? Equipment.Boots.Color : 0));
            x33.WriteByte(0x00); // accessory 1 color
            x33.WriteUInt16((ushort)(Equipment.FirstAcc != null ? Equipment.FirstAcc.DisplaySprite : 0)); // accessory 1
            x33.WriteByte(0x00); // accessory 2 color
            x33.WriteUInt16((ushort)(Equipment.SecondAcc != null ? Equipment.SecondAcc.DisplaySprite : 0)); // accessory 2
            x33.WriteByte(0x00); // accessory 3 color
            x33.WriteUInt16((ushort)(Equipment.ThirdAcc != null ? Equipment.ThirdAcc.DisplaySprite : 0)); // accessory 3
            x33.WriteByte(0x00); // lantern size
            x33.WriteByte(0x00); // rest position
            x33.WriteUInt16((ushort)(Equipment.Overcoat != null ? Equipment.Overcoat.DisplaySprite : 0)); // overcoat
            x33.WriteByte(0x00); // overcoat color
            x33.WriteByte(0x00); // skin color
            x33.WriteByte(0x00); // semi-trans
            x33.WriteByte(0x00); // face
            x33.WriteByte(0x00); // name style
            x33.WriteString8(Name);
            x33.WriteString8(string.Empty); // group name
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
            // Update the client.
            // ServerPacket type: 0x37
            // byte: index
            // Uint16: sprite offset (79 FF is actually a red scroll, 80 00 onwards are real items)
            // Byte: ??
            // Byte: Item Name length
            // String: Item Name
            // Uint32: Max Durability
            // Uint32: Min Durability

            if (item == null)
            {
                SendRefreshEquipmentSlot(slot);
                return;
            }

            var equipPacket = new ServerPacket(0x37);
            equipPacket.WriteByte((byte)slot);
            equipPacket.WriteUInt16((ushort)(item.Sprite + 0x8000));
            equipPacket.WriteByte(0x00);
            equipPacket.WriteStringWithLength(item.Name);
            equipPacket.WriteByte(0x00);
            equipPacket.WriteUInt32(item.MaximumDurability);
            equipPacket.WriteUInt32(item.Durability);
            equipPacket.DumpPacket();
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

        public void SendClearSkill(int slot)
        {
            var x2D = new ServerPacket(0x2D);
            x2D.WriteByte((byte)slot);
            Enqueue(x2D);
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
            x0F.WriteInt32(item.Count);  //amount
            x0F.WriteBoolean(item.Stackable);
            x0F.WriteUInt32(item.MaximumDurability);  //maxdura
            x0F.WriteUInt32(item.Durability);  //curdura
            x0F.WriteUInt32(0x00);  //?
            Enqueue(x0F);
        }

        public void SendSkillUpdate(Castable item, int slot)
        {
            if(item == null)
            {
                SendClearSkill(slot);
                return;
            }
            Logger.DebugFormat("Adding skill {0} to slot {2}",
                item.Name, slot);
            var x2C = new ServerPacket(0x2C);
            x2C.WriteByte((byte)slot);
            x2C.WriteUInt16((ushort)(item.Icon));
            x2C.WriteString8(item.Name);
            x2C.WriteByte(0); //current level
            x2C.WriteByte((byte)100); //this will need to be updated
            Enqueue(x2C);

        }

        public void SendSpellUpdate(Castable item, int slot)
        {
            if (item == null)
            {
                SendClearSkill(slot);
                return;
            }
            Logger.DebugFormat("Adding spell {0} to slot {2}",
                item.Name, slot);
            var x17 = new ServerPacket(0x17);
            x17.WriteByte((byte)slot);
            x17.WriteUInt16((ushort)(item.Icon));
            x17.WriteByte(0x00); //spell type? how are we determining this?
            x17.WriteString8(item.Name);
            x17.WriteString8(item.Name); //prompt? what is this?
            x17.WriteByte((byte)item.Lines);
            x17.WriteByte(0); //current level
            x17.WriteByte((byte)100); //this will need to be updated
            Enqueue(x17);

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
            if (UnreadMail)
            {
                flags |= StatUpdateFlags.UnreadMail;
            }

            if (IsPrivileged || IsExempt)
            {
                flags |= StatUpdateFlags.GameMasterA;
            }

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
                x08.WriteUInt16(VisibleWeight);
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
                x08.WriteUInt32(0); // Next AB
                x08.WriteUInt32(0); // "GP"
                x08.WriteUInt32(Gold);
            }
            if (flags.HasFlag(StatUpdateFlags.Secondary))
            {
                x08.WriteByte(0); //Unknown
                x08.WriteByte((byte) (Status.HasFlag(PlayerStatus.Blinded) ? 0x08 : 0x00));
                x08.WriteByte(0); // Unknown
                x08.WriteByte(0); // Unknown
                x08.WriteByte(0); // Unknown
                x08.WriteByte((byte) (Mailbox.HasUnreadMessages ? 0x10 : 0x00));
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
            Warp targetWarp;

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
            var isWarp = Map.Warps.TryGetValue(new Tuple<byte, byte>((byte)newX, (byte)newY), out targetWarp);

            // Now that we know where we are going, perform some sanity checks.
            // Is the player trying to walk into a wall, or off the map?

            if (newX > Map.X || newY > Map.Y || Map.IsWall[newX, newY] || newX < 0 || newY < 0)
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
                if (isWarp)
                {
                    if (targetWarp.MinimumLevel > Level)
                    {
                        Client.SendMessage("You're too afraid to even approach it!", 3);
                        Refresh();
                        return false;
                    }
                    else if (targetWarp.MaximumLevel < Level)
                    {
                        Client.SendMessage("Your honor forbids you from entering.", 3);
                        Refresh();
                        return false;
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

            if (isWarp)
            {
                return targetWarp.Use(this);
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

        public bool AddSkill(Castable castable)
        {
            if (SkillBook.IsFull)
            {
                SendSystemMessage("You cannot learn any more skills.");
                return false;
            }
            return AddSkill(castable, SkillBook.FindEmptySlot());
        }

        public bool AddSkill(Castable item, byte slot)
        {
            // Quantity check - if we already have an item with the same name, will
            // adding the MaximumStack)

            if(SkillBook.Contains(item.Id))
            {
                SendSystemMessage("You already know this skill.");
                return false;
            }

            Logger.DebugFormat("Attempting to add skill to skillbook slot {0}", slot);


            if (!SkillBook.Insert(slot, item))
            {
                Logger.DebugFormat("Slot was invalid or not null");
                return false;
            }

            SendSkillUpdate(item, slot);
            return true;
        }

        public bool AddSpell(Castable castable)
        {
            if (SpellBook.IsFull)
            {
                SendSystemMessage("You cannot learn any more spells.");
                return false;
            }
            return AddSkill(castable, SkillBook.FindEmptySlot());
        }

        public bool AddSpell(Castable item, byte slot)
        {
            // Quantity check - if we already have an item with the same name, will
            // adding the MaximumStack)

            if (SpellBook.Contains(item.Id))
            {
                SendSystemMessage("You already know this spell.");
                return false;
            }

            Logger.DebugFormat("Attempting to add spell to spellbook slot {0}", slot);


            if (!SpellBook.Insert(slot, item))
            {
                Logger.DebugFormat("Slot was invalid or not null");
                return false;
            }

            SendSpellUpdate(item, slot);
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
            // Weight check

            if (item.Weight + CurrentWeight > MaximumWeight)
            {
                SendSystemMessage("It's too heavy.");
                Map.Insert(item, X, Y);
                return false;
            }

            // Quantity check - if we already have an item with the same name, will
            // adding the MaximumStack)

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
                
                // Merge stack and destroy "added" item
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
            if (updateWeight) UpdateAttributes(StatUpdateFlags.Primary);
            return true;
        }

        public bool RemoveItem(byte slot, bool updateWeight = true)
        {
            if (Inventory.Remove(slot))
            {
                SendClearItem(slot);
                if (updateWeight) UpdateAttributes(StatUpdateFlags.Primary);
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
            if (sendUpdate) Show();

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
                if (sendUpdate) Show();
                return true;
            }
            return false;
        }

        public void SendRefreshEquipmentSlot(int slot)
        {
            // Like a normal refresh packet, except with a byte indicating which slot we wish to clear

            var refreshPacket = new ServerPacket(0x38);
            refreshPacket.WriteByte((byte)slot);
            Enqueue(refreshPacket);
        }

        public override void Refresh()
        {
            SendMapInfo();
            SendLocation();
            SendInventory();

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

        public override void Attack(Direction direction, Castable castObject = null, Creature target = null)
        {
            if (target != null)
            {
                var damage = castObject.Effects.Damage;

                Random rand = new Random();

                if (damage.Formula == null) //will need to be expanded. also will need to account for damage scripts
                {
                    var simple = damage.Simple;
                    var damageType = EnumUtil.ParseEnum<Enums.DamageType>(damage.Type.ToString(),
                        Enums.DamageType.Magical);
                    var dmg = rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
                        //these need to be set to integers as attributes. note to fix.
                    target.Damage(dmg, OffensiveElement, damageType, this);
                }
                else
                {
                    var formula = damage.Formula;
                    var damageType = EnumUtil.ParseEnum<Enums.DamageType>(damage.Type.ToString(),
                        Enums.DamageType.Magical);
                    FormulaParser parser = new FormulaParser(this, castObject, target);
                    var dmg = parser.Eval(formula);
                    if (dmg == 0) dmg = 1;
                    target.Damage(dmg, OffensiveElement, damageType, this);
                }
                //var dmg = rand.Next(Convert.ToInt32(simple.Min), Convert.ToInt32(simple.Max));
                    //these need to be set to integers as attributes. note to fix.
                //target.Damage(dmg, OffensiveElement, damage.Type, this);
            }
            else
            {
                //var formula = damage.Formula;
            }
        }

        public override void Attack(Castable castObject, Creature target = null)
        {
            base.Attack(castObject, target);
        }

        public override void Attack(Castable castObject)
        {
            base.Attack(castObject);
        }

        public void AssailAttack(Direction direction, Creature target = null)
        {
            if (target == null)
            {
                switch (direction)
                {
                    case Direction.East:
                    {
                        var obj = Map.EntityTree.Where(x => x.X == X + 1 && x.Y == Y).FirstOrDefault();
                        if (obj is Monster) target = (Monster) obj;
                        if (obj is User)
                        {
                            var user = (User) obj;
                            //need to do something for if pvpflagged.
                        }
                    }
                        break;
                    case Direction.West:
                    {
                        var obj = Map.EntityTree.Where(x => x.X == X - 1 && x.Y == Y).FirstOrDefault();
                        if (obj is Monster) target = (Monster) obj;
                    }
                        break;
                    case Direction.North:
                    {
                        var obj = Map.EntityTree.Where(x => x.X == X && x.Y == Y - 1).FirstOrDefault();
                        if (obj is Monster) target = (Monster) obj;
                    }
                        break;
                    case Direction.South:
                    {
                        var obj = Map.EntityTree.Where(x => x.X == X && x.Y == Y + 1).FirstOrDefault();
                        if (obj is Monster) target = (Monster) obj;
                    }
                        break;
                    default:
                        break;
                }
                //try to get the creature we're facing and set it as the target.
            }

            foreach (Castable c in SkillBook)
            {
                if (c.Isassail == "true")
                    //i do not like that this is a string. I'll probablt update it at some point to return a simple type of boolean.
                {
                    Attack(direction, c, target);
                }
            }
            //animation handled here as to not repeatedly send assails.
            //this.LastAttack = DateTime.Now;
            var assail = new ServerPacketStructures.PlayerAnimation() {Animation = 1, Speed = 20, UserId = this.Id};
            Enqueue(assail.Packet());
            SendAnimation(assail.Packet(), 01);
        }


        private string GroupProfileSegment()
        {
            StringBuilder sb = new StringBuilder();

            // Only build this string if the user's in a group. Otherwise an empty
            // string should be sent.
            if (Grouped)
            {
                sb.Append("Group members");
                sb.Append((char)0x0A);

                // The user's name should go first, and should not have an asterisk.
                // In practice this will mean that the user's name appears first and
                // is grayed out, while all other names are white.
                sb.Append("  " + Name);
                sb.Append((char)0x0A);

                foreach (var member in Group.Members)
                {
                    if (member.Name != Name)
                    {
                        sb.Append("  " + member.Name);
                        sb.Append((char)0x0A);
                    }
                }
                sb.Append(String.Format("Total {0}", Group.Members.Count));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Send a player's profile to themselves (e.g. click on self or hit Y for group info)
        /// </summary>
        public void SendProfile()
        {
            var profilePacket = new ServerPacket(0x39);
            profilePacket.WriteByte((byte) Nation.Flag); // citizenship
            profilePacket.WriteString8(Guild.Rank);
            profilePacket.WriteString8(Guild.Title);
            profilePacket.WriteString8(GroupText);
            profilePacket.WriteBoolean(Grouping);
            profilePacket.WriteByte(0); // ??
            profilePacket.WriteByte((byte) Class);
            //            profilePacket.WriteByte(1); // ??
            profilePacket.WriteByte(0);
            profilePacket.WriteByte(0); // ??
            profilePacket.WriteString8(IsMaster ? "Master" : Hybrasyl.Constants.REVERSE_CLASSES[(int) Class]);
            profilePacket.WriteString8(Guild.Name);
            profilePacket.WriteByte((byte) Legend.Count);
            foreach (var mark in Legend)
            {
                profilePacket.WriteByte((byte) mark.Icon);
                profilePacket.WriteByte((byte) mark.Color);
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
            Login.LastLogin = DateTime.Now;
            Save();
        }

        /// <summary>
        /// Update a player's last logoff time in the database and the live object.
        /// </summary>
        public void UpdateLogoffTime()
        {
            Login.LastLogoff = DateTime.Now;
            Save();
        }

        public void SendWorldMap(WorldMap map)
        {
            var x2E = new ServerPacket(0x2E);
            x2E.Write(map.GetBytes());
            x2E.DumpPacket();
            IsAtWorldMap = true;
            Enqueue(x2E);
        }

        public void SendMotion(uint id, byte motion, short speed)
        {
            Logger.InfoFormat("SendMotion id {0}, motion {1}, speed {2}", id,motion,speed);
            var x1A = new ServerPacket(0x1A);
            x1A.WriteUInt32(id);
            x1A.WriteByte(motion);
            x1A.WriteInt16(speed);
            x1A.WriteByte(0xFF);
            Enqueue(x1A);
        }

        public void SendEffect(uint id, ushort effect, short speed)
        {
            Logger.InfoFormat("SendEffect: id {0}, effect {1}, speed {2} ", id, effect, speed);
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
            Logger.InfoFormat("SendEffect: targetId {0}, targetEffect {1}, srcId {2}, srcEffect {3}, speed {4}",
                targetId, targetEffect, srcId, srcEffect, speed);
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
            Logger.InfoFormat("SendEffect: x {0}, y {1}, effect {2}, speed {3}", x,y,effect,speed);
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
            //CurrentMusicTrack = track;

            //var x19 = new ServerPacket(0x19);
            //x19.WriteByte(0xFF);
            //x19.WriteByte(track);
            //Enqueue(x19);
        }

        public void SendSound(byte sound)
        {
            Logger.InfoFormat("SendSound {0}", sound);
            var x19 = new ServerPacket(0x19);
            x19.WriteByte(sound);
            Enqueue(x19);
        }

        public void SendDoorUpdate(byte x, byte y, bool state, bool leftright)
        {
            // Send the user a door packet

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
            x2F.WriteByte(0x04); // type!
            x2F.WriteByte(0x01); // obj type
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x00); // ??
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16("What would you like to buy?");
            x2F.WriteUInt16((ushort)MerchantMenuItem.BuyItem);
            x2F.WriteUInt16((ushort)merchant.Inventory.Count);
            foreach (var item in merchant.Inventory.Values)
            {
                x2F.WriteUInt16((ushort)(0x8000 + item.Properties.Appearance.Sprite));
                x2F.WriteByte((byte)item.Properties.Appearance.Color);
                x2F.WriteUInt32((uint)item.Properties.Physical.Value);
                x2F.WriteString8(item.Name);
                x2F.WriteString8(string.Empty); // defunct item description
            }
            Enqueue(x2F);
        }

        public void ShowBuyMenuQuantity(Merchant merchant, string name)
        {
            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x03); // type!
            x2F.WriteByte(0x01); // obj type
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x00); // ??
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16(string.Format("How many {0} would you like to buy?", name));
            x2F.WriteString8(name);
            x2F.WriteUInt16((ushort)MerchantMenuItem.BuyItemQuantity);
            Enqueue(x2F);
        }

        public void ShowSellMenu(Merchant merchant)
        {

            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x05); // type!
            x2F.WriteByte(0x01); // obj type
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x00); // ??
            x2F.WriteString8(merchant.Name);
            x2F.WriteString16("What would you like to sell?");
            x2F.WriteUInt16((ushort)MerchantMenuItem.SellItem);

            int position = x2F.Position;
            x2F.WriteByte(0);
            int count = 0;

            for (byte i = 1; i <= Inventory.Size; ++i)
            {
                if (Inventory[i] == null || !merchant.Inventory.ContainsKey(Inventory[i].Name))
                    continue;

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
            x2F.WriteByte(0x03); // type!
            x2F.WriteByte(0x01); // obj type
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x00); // ??
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
            double offer = Math.Round(item.Value * 0.50) * quantity;

            var x2F = new ServerPacket(0x2F);
            x2F.WriteByte(0x01); // type!
            x2F.WriteByte(0x01); // obj type
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x00); // ??
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
            x2F.WriteByte(0x00); // type!
            x2F.WriteByte(0x01); // obj type
            x2F.WriteUInt32(merchant.Id);
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x01); // ??
            x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            x2F.WriteByte(0x00); // color
            x2F.WriteByte(0x00); // ??
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
            // Hilariously we need to check the length of this string (total length needs 
            // to be <67) otherwise we will cause a buffer overflow / crash on the client side
            // (For right now we assume the color code ({=c) isn't counted but that needs testing)
            // I MEAN IT TAKES 16 BIT RITE BUT HAY ARBITRARY LENGTH ON STRINGS WITH NO NULL TERMINATION IS LEET
            var transmit = String.Format("{{=c[{0}] {1}", sender, message);
            if (transmit.Length > 67)
            {
                // IT'S CHOPPIN TIME
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
            Save();
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
                x42.WriteByte(0); // show exchange window
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
                x42.WriteByte(1); // show quantity prompt
                x42.WriteByte(itemSlot); // Slot for which we need quantity info
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
                var x42 = new ServerPacket(0x42); // Update exchange packet
                x42.WriteByte(2); // Show item in exchange window
                x42.WriteByte((byte)(source ? 0 : 1)); // Update "my" side of the transaction
                x42.WriteByte(slot); // Which "exchange slot" to update
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
                var x42 = new ServerPacket(0x42); // Update exchange packet
                x42.WriteByte(3); // Update gold in exchange window
                x42.WriteByte((byte)(source ? 0 : 1)); // Update "my" side of the transaction
                x42.WriteUInt32(gold); // Which "exchange slot" to update
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
                var x42 = new ServerPacket(0x42); // Update exchange packet
                x42.WriteByte(4); // Exchange cancelled
                x42.WriteByte((byte)(source ? 0 : 1)); // Which "side" cancelled the transaction
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
                var x42 = new ServerPacket(0x42); // Update exchange packet
                x42.WriteByte(5); // Exchange confirmed
                x42.WriteByte((byte)(source ? 0 : 1)); // Which "side" confirmed the transaction
                x42.WriteString8("You exchanged.");
                Enqueue(x42);
            }
        }

        public void SendInventory()
        {
            for(byte i = 0; i<this.Inventory.Size; i++)
            {
                if(this.Inventory[i] != null)
                {
                    var x0F = new ServerPacket(0x0F);
                    x0F.WriteByte(i);
                    x0F.WriteUInt16((ushort)(Inventory[i].Sprite + 0x8000));
                    x0F.WriteByte(Inventory[i].Color);
                    x0F.WriteString8(this.Inventory[i].Name);
                    x0F.WriteInt32(this.Inventory[i].Count);
                    x0F.WriteBoolean(this.Inventory[i].Stackable);
                    x0F.WriteUInt32(this.Inventory[i].MaximumDurability);
                    x0F.WriteUInt32(this.Inventory[i].Durability);
                    Enqueue(x0F);
                }
            }
        }

        public void SendEquipment()
        {
            foreach (var item in Equipment)
            {
                SendEquipItem(item, item.EquipmentSlot);
            }
        }
        public void SendSkills()
        {
            for (byte i = 0; i < this.SkillBook.Size; i++)
            {
                if (this.SkillBook[i] != null)
                {
                    var x2C = new ServerPacket(0x2C);
                    x2C.WriteByte((byte)i);
                    x2C.WriteUInt16((ushort)(SkillBook[i].Icon));
                    x2C.WriteString8(SkillBook[i].Name);
                    x2C.WriteByte(0); //current level
                    x2C.WriteByte((byte)100); //this will need to be updated
                    Enqueue(x2C);
                }
            }
        }
        public void SendSpells()
        {
            for (byte i = 0; i < this.SpellBook.Size; i++)
            {
                if (this.SpellBook[i] != null)
                {
                    var x17 = new ServerPacket(0x17);
                    x17.WriteByte((byte)i);
                    x17.WriteUInt16((ushort)(SpellBook[i].Icon));
                    x17.WriteByte(0x00); //spell type? how are we determining this?
                    x17.WriteString8(SpellBook[i].Name);
                    x17.WriteString8(SpellBook[i].Name); //prompt? what is this?
                    x17.WriteByte((byte)SpellBook[i].Lines);
                    x17.WriteByte(0); //current level
                    x17.WriteByte((byte)100); //this will need to be updated
                    Enqueue(x17);
                }
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


        /*
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter =true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("SerializationVersion", "1");
            info.AddValue("Name", Name);
            info.AddValue("Sex", Sex);
            info.AddValue("HairStyle", HairStyle);
            info.AddValue("HairColor", HairColor);
            info.AddValue("Class", Class);
            info.AddValue("Level", Level);
            info.AddValue("LevelPoints", LevelPoints);
            info.AddValue("Experience", Experience);
            info.AddValue("Ability", Ability);
            info.AddValue("MapId", MapId);
            info.AddValue("MapX", MapX);
            info.AddValue("AbilityExp", AbilityExp);
            info.AddValue("BaseHp", BaseHp);
            info.AddValue("BaseMp", BaseMp);
            info.AddValue("Hp", Hp);
            info.AddValue("Mp", Mp);
            info.AddValue("BaseStr", BaseStr);
            info.AddValue("BaseInt", BaseStr);
            info.AddValue("BaseWis", BaseStr);
            info.AddValue("BaseDex", BaseStr);
            info.AddValue("BaseCon", BaseStr);
            info.AddValue("Gold", Gold);
            info.AddValue("IsMaster", IsMaster);
            info.AddValue("Dead", Dead);
            info.AddValue("Grouping", Grouping);
            info.AddValue("PortraitData", PortraitData);
            info.AddValue("ProfileText", ProfileText);
            info.AddValue("LoginTime", LoginTime);
            info.AddValue("LogoffTime", LogoffTime);
            info.AddValue("UserFlags", UserFlags);
            info.AddValue("PlayerStatus", Status);
                
            info.AddValue("LegendMarks", Legend);
            info.AddValue("Inventory", Inventory, typeof(Inventory));
            info.AddValue("Equipment", Equipment, typeof(Inventory));




        }*/
    }
}
