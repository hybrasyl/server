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


using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Castables;
using Hybrasyl.Nations;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Xml;
using Hybrasyl.Items;
using Class = Hybrasyl.Castables.Class;
using Motion = Hybrasyl.Castables.Motion;
using System.Globalization;
using Hybrasyl.Statuses;

namespace Hybrasyl.Objects
{

    [JsonObject]
    public class GuildMembership
    {
        public string Title { get; set; }
        public string Name { get; set; }
        public string Rank { get; set; }
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
        public string Hash { get; set; }
        public DateTime LastChanged { get; set; }
        public string LastChangedFrom { get; set; }
    }

    [JsonObject]
    public class LoginInfo
    {
        public DateTime LastLogin { get; set; }
        public DateTime LastLogoff { get; set; }
        public DateTime LastLoginFailure { get; set; }
        public string LastLoginFrom { get; set; }
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

        private static readonly ILog ActivityLogger = LogManager.GetLogger("UserActivityLogger");

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
        public Enums.Class Class { get; set; }
        [JsonProperty]
        public bool IsMaster { get; set; }
        public UserGroup Group { get; set; }

        public Mailbox Mailbox => World.GetMailbox(Name);
        public bool UnreadMail => Mailbox.HasUnreadMessages;

        #region Appearance settings 
        [JsonProperty]
        public RestPosition RestPosition { get; set; }
        [JsonProperty]
        public SkinColor SkinColor { get; set; }
        [JsonProperty]
        internal bool Transparent { get; set; }
        [JsonProperty]
        public byte FaceShape { get; set; }
        [JsonProperty]
        public LanternSize LanternSize { get; set; }
        [JsonProperty]
        public NameDisplayStyle NameStyle { get; set; }
        [JsonProperty]
        public bool DisplayAsMonster { get; set; }
        [JsonProperty]
        public ushort MonsterSprite { get; set; }
        [JsonProperty]
        public byte HairStyle { get; set; }
        [JsonProperty]
        public byte HairColor { get; set; }
        #endregion

        #region User metadata
        // Some structs helping us to define various metadata 
        [JsonProperty]
        public Location Location { get; set; }
        [JsonProperty]
        public LoginInfo Login { get; set; }
        [JsonProperty]
        public PasswordInfo Password { get; set; }
        [JsonProperty]
        public SkillBook SkillBook { get; private set; }
        [JsonProperty]
        public SpellBook SpellBook { get; private set; }

        [JsonProperty]
        public bool Grouping { get; set; }
        public UserStatus GroupStatus { get; set; }
        public byte[] PortraitData { get; set; }
        public string ProfileText { get; set; }

        public Castable PendingLearnableCastable { get; private set; }
        public ItemObject PendingSendableParcel { get; private set; }
        public string PendingParcelRecipient { get; private set; }
        public string PendingBuyableItem { get; private set; }
        public int PendingBuyableQuantity { get; private set; }
        public byte PendingSellableSlot { get; private set; }
        public int PendingSellableQuantity { get; private set; }
        public uint PendingMerchantOffer { get; private set; }


        [JsonProperty]
        public GuildMembership Guild { get; set; }

        public List<string> UseCastRestrictions => _currentStatuses.Select(e => e.Value.UseCastRestrictions).Where(e => e != string.Empty).ToList();
        public List<string> ReceiveCastRestrictions => _currentStatuses.Select(e => e.Value.ReceiveCastRestrictions).Where(e => e != string.Empty).ToList();

        private Nation _nation;

        public Nation Nation
        {
            get
            {
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

        [JsonProperty] public Legend Legend;


        public DialogState DialogState { get; set; }

        [JsonProperty]
        private Dictionary<string, string> UserFlags { get; set; }
        private Dictionary<string, string> UserSessionFlags { get; set; }

        public Exchange ActiveExchange { get; set; }

        public bool IsAvailableForExchange => Condition.NoFlags;
        #endregion

        /// <summary>
        /// Reindexes any temporary data structures that may need to be recreated after a user is deserialized from JSON data.
        /// </summary>
        public void Reindex()
        {
            Legend.RegenerateIndex();
        }

        public uint ExpToLevel
        {
            get
            {
                var levelExp = (uint)Math.Pow(Level, 3) * 250;
                if (Level == Constants.MAX_LEVEL || Experience >= levelExp)
                    return 0;

                return (uint)(Math.Pow(Level, 3) * 250 - Experience);
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
                Nation = World.WorldData.TryGetValue(Citizenship, out theNation) ? theNation : World.DefaultNation;
            }
        }

        public bool IsPrivileged
        {
            get
            {
                if (Game.Config.Access?.Privileged != null)
                {
                    return IsExempt || Flags.ContainsKey("gamemaster") ||
                        Game.Config.Access.Privileged.IndexOf(Name, 0, StringComparison.CurrentCultureIgnoreCase) != 1;
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

        public string SinceLastLoginstring => SinceLastLogin < 86400 ?
            $"{Math.Floor(SinceLastLogin / 3600)} hours, {Math.Floor(SinceLastLogin % 3600 / 60)} minutes" :
            $"{Math.Floor(SinceLastLogin / 86400)} days, {Math.Floor(SinceLastLogin % 86400 / 3600)} hours, {Math.Floor(SinceLastLogin % 86400 % 3600 / 60)} minutes";

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
            Logger.DebugFormat("Sending {0:X2} to {1}", packet.Opcode, Name);
            Client.Enqueue(packet);
        }

        public override void AoiEntry(VisibleObject obj)
        {
            base.AoiEntry(obj);
            Logger.DebugFormat("Showing {0} to {1}", Name, obj.Name);
            obj.ShowTo(this);
        }

        public override void AoiDeparture(VisibleObject obj)
        {
            base.AoiDeparture(obj);
            Logger.DebugFormat("Removing ItemObject with ID {0}", obj.Id);
            var removePacket = new ServerPacket(0x0E);
            removePacket.WriteUInt32(obj.Id);
            Enqueue(removePacket);
        }

        public void AoiDeparture(VisibleObject obj, int transmitDelay)
        {
            Logger.DebugFormat("Removing ItemObject with ID {0}", obj.Id);
            var removePacket = new ServerPacket(0x0E);
            removePacket.TransmitDelay = transmitDelay;
            removePacket.WriteUInt32(obj.Id);
            Enqueue(removePacket);
        }


        /// <summary>T
        /// Send a status bar update to the client based on the state of a given status.
        /// </summary>
        /// <param name="status">The status to update on the client side.</param>
        /// <param name="remove">Force removal of the status</param>

        public virtual void SendStatusUpdate(ICreatureStatus status, bool remove = false)
        {
            var statuspacket = new ServerPacketStructures.StatusBar { Icon = status.Icon };
            var elapsed = DateTime.Now - status.Start;
            var remaining = status.Duration - elapsed.TotalSeconds;
            StatusBarColor color;
            if (remaining >= 80)
                color = StatusBarColor.White;
            else if (remaining <= 80 && remaining >= 60)
                color = StatusBarColor.Red;
            else if (remaining <= 60 && remaining >= 40)
                color = StatusBarColor.Orange;
            else if (remaining <= 40 && remaining >= 20)
                color = StatusBarColor.Green;
            else
                color = StatusBarColor.Blue;

            if (remove || status.Expired)
                color = StatusBarColor.Off;

            statuspacket.BarColor = color;
            Logger.DebugFormat($"{Name} - status update - sending Icon: {statuspacket.Icon}, Color: {statuspacket.BarColor}");
            Logger.DebugFormat($"{Name} - status: {status.Name}, expired: {status.Expired}, remaining: {remaining}, duration: {status.Duration}");
            Enqueue(statuspacket.Packet());
        }

        /// <summary>
        /// Sadly, all things in this world must come to an end.
        /// </summary>
        public override void OnDeath()
        {
            var handler = Game.Config.Handlers?.Death;
            if (!(handler?.Active ?? true))
            {
                SendSystemMessage("Death disabled by server configuration");
                Hp = 1;
                UpdateAttributes(StatUpdateFlags.Full);
                return;
            }

            var timeofdeath = DateTime.Now;
            var looters = Group?.Members.Select(user => user.Name).ToList() ?? new List<string>();

            // Remove all statuses
            RemoveAllStatuses();

            // We are now quite dead, not mostly dead

            Condition.Comatose = false;

            // First: break everything that is breakable in the inventory
            for (byte i = 0; i <= Inventory.Size; ++i)
            {
                if (Inventory[i] == null) continue;
                var theItem = Inventory[i];
                RemoveItem(i);
                if (theItem.Perishable && (handler?.Perishable ?? true)) continue;
                theItem.DeathPileOwner = Name;
                theItem.DeathPileTime = timeofdeath;
                theItem.DeathPileAllowedLooters = looters;
                Map.AddItem(X, Y, theItem);
            }

            // Now process equipment
            foreach (var item in Equipment)
            {
                RemoveEquipment(item.EquipmentSlot);
                if (item.Perishable && (handler?.Perishable ?? true)) continue;
                if (item.Durability > 10)
                    item.Durability = (uint)Math.Ceiling(item.Durability * 0.90);
                else
                    item.Durability = 0;
                item.DeathPileOwner = Name;
                item.DeathPileTime = timeofdeath;
                item.DeathPileAllowedLooters = looters;

                Map.AddItem(X, Y, item);
            }

            // Drop all gold
            if (Gold > 0)
            {
                var newGold = new Gold(Gold)
                {
                    DeathPileAllowedLooters = looters,
                    DeathPileOwner = Name,
                    DeathPileTime = timeofdeath
                };
                World.Insert(newGold);
                Map.AddGold(X, Y, newGold);
                Gold = 0;
            }

            // Experience penalty
            if (handler?.Penalty != null) {
                if (Experience > 1000)
                {
                    uint expPenalty;
                    if (handler.Penalty.Xp.Contains('.'))
                        expPenalty = (uint)Math.Ceiling(Experience * Convert.ToDouble(handler.Penalty.Xp));
                    else
                        expPenalty = Convert.ToUInt32(handler.Penalty.Xp);
                    Experience -= expPenalty;
                    SendSystemMessage($"You lose {expPenalty} experience!");
                }
                if (BaseHp >= 51 && Level == 99)
                {
                    uint hpPenalty;

                    if (handler.Penalty.Xp.Contains('.'))
                        hpPenalty = (uint)Math.Ceiling(Experience * Convert.ToDouble(handler.Penalty.Hp));
                    else
                        hpPenalty = Convert.ToUInt32(handler.Penalty.Hp);

                    BaseHp -= hpPenalty;
                    SendSystemMessage($"You lose {hpPenalty} HP!");
                }
            }
            Hp = 0;
            Mp = 0;
            Condition.Alive = false;
            UpdateAttributes(StatUpdateFlags.Full);
            Effect(76, 120);
            SendSystemMessage("Your items are ripped from your body.");

            if (Game.Config.Handlers?.Death?.Map != null) {
                Teleport(Game.Config.Handlers.Death.Map.Value,
                    Game.Config.Handlers.Death.Map.X,
                    Game.Config.Handlers.Death.Map.Y);
            }

            if (Game.Config.Handlers?.Death?.GroupNotify ?? true)
                Group?.SendMessage($"{Name} has died!");
            

        }


        /// <summary>
        /// End a user's coma status (skulling).
        /// </summary>
        public void EndComa()
        {
            if (!Condition.Comatose) return;
            Condition.Comatose = false;
            var handler = Game.Config.Handlers?.Death;
            if (handler?.Coma != null && Game.World.WorldData.TryGetValueByIndex(handler.Coma.Value, out Status status))
                RemoveStatus(status.Icon, false);
        }

        /// <summary>
        /// Resurrect a player.
        /// </summary>
        public void Resurrect()
        {
            var handler = Game.Config.Handlers?.Death;
            // Teleport user to national spawn point
            Condition.Alive = true;
            if (Nation.SpawnPoints.Count != 0)
            {
                var spawnpoint = Nation.RandomSpawnPoint;
                Teleport(spawnpoint.MapName, spawnpoint.X, spawnpoint.Y);
            }
            else
            {
                // Handle any weird cases where a map someone exited on was deleted, etc
                // This "default" of Mileth should be set somewhere else
                Teleport((ushort)500, (byte)50, (byte)50);
            }

            Hp = 1;
            Mp = 1;

            UpdateAttributes(StatUpdateFlags.Full);

            if (handler.LegendMark != null)
            {
                LegendMark deathMark;

                if (Legend.TryGetMark(handler.LegendMark.Prefix, out deathMark) && handler.LegendMark.Increment)
                {
                    deathMark.AddQuantity(1);
                }
                else
                    Legend.AddMark(LegendIcon.Community, LegendColor.Orange, handler.LegendMark.Value, DateTime.Now, handler.LegendMark.Prefix, true,
                        1);

            }
        }

        public string GroupText
        {
            get
            {
                // This also eventually needs to consider marriages
                return Grouping ? "Grouped!" : "Adventuring Alone";
            }
        }

        /**
         * Returns the current weight as perceived by the client. The actual inventory or equipment
         * weight may be less than zero, but this method will never return a negative value (negative
         * values will appear as zero as the client expects).
         */

        public ushort VisibleWeight
        {
            get { return (ushort)Math.Max(0, CurrentWeight); }
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
            get { return (ushort)(BaseStr + Level / 4 + 48); }
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, Password.Hash);
        }

        public User() : base()
        {
            _initializeUser();
        }

        private void _initializeUser(string playername = "")
        {
            Inventory = new Inventory(59);
            Equipment = new Inventory(18);
            SkillBook = new SkillBook();
            SpellBook = new SpellBook();
            IsAtWorldMap = false;
            Login = new LoginInfo();
            Password = new PasswordInfo();
            Location = new Location();
            Legend = new Legend();
            Guild = new GuildMembership();
            LastSaid = string.Empty;
            LastSpoke = 0;
            NumSaidRepeated = 0;
            PortraitData = new byte[0];
            ProfileText = string.Empty;
            DialogState = new DialogState(this);
            UserFlags = new Dictionary<string, string>();
            UserSessionFlags = new Dictionary<string, string>();
            Group = null;
            Flags = new Dictionary<string, bool>();
            _currentStatuses = new ConcurrentDictionary<ushort, ICreatureStatus>();

            #region Appearance defaults
            RestPosition = RestPosition.Standing;
            SkinColor = SkinColor.Basic;
            Transparent = false;
            FaceShape = 0;
            NameStyle = NameDisplayStyle.GreyHover;
            LanternSize = LanternSize.None;
            DisplayAsMonster = false;
            MonsterSprite = ushort.MinValue;
            #endregion
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
                Group.ShareExperience(this, exp);
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
            Client.SendMessage($"{exp} experience!", MessageTypes.SYSTEM);
            if (Level == Constants.MAX_LEVEL || exp < ExpToLevel)
            {
                if (uint.MaxValue - Experience >= exp)
                    Experience += exp;
                else
                {
                    Experience = uint.MaxValue;
                    SendSystemMessage("You cannot gain any more experience.");
                }
            }
            else
            {
                // Apply one Level at a time

                var levelsGained = 0;
                Random random = new Random();

                while (exp > 0 && Level < 99)
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
                // If a user has just become level 99, add the remainder exp to their box
                if (Level == 99)
                    Experience += exp;

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
            Location = new Location { MapId = targetMap, WorldMap = false, X = targetX, Y = targetY };
            _initializeUser(playername);
        }

        /// <summary>
        /// Given a specified ItemObject, apply the given bonuses to the player.
        /// </summary>
        /// <param name="toApply">The ItemObject used to calculate bonuses.</param>
        public void ApplyBonuses(ItemObject toApply)
        {
            // Given an ItemObject, set our bonuses appropriately.
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
                case (byte)ItemSlots.Necklace:
                    BaseOffensiveElement = toApply.Element;
                    break;
                case (byte)ItemSlots.Waist:
                    BaseDefensiveElement = toApply.Element;
                    break;
            }

            Logger.DebugFormat(
                "Player {0}: stats now {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}",
                BonusHp, BonusHp, BonusStr, BonusInt, BonusWis,
                BonusCon, BonusDex, BonusHit, BonusDmg, BonusAc,
                BonusMr, BonusRegen, OffensiveElement, DefensiveElement);

        }

        /// <summary>
        /// Given a specified ItemObject, remove the given bonuses from the player.
        /// </summary>
        /// <param name="toRemove"></param>
        public void RemoveBonuses(ItemObject toRemove)
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
                    BaseOffensiveElement = Enums.Element.None;
                    break;
                case (byte)ItemSlots.Waist:
                    BaseDefensiveElement = Enums.Element.None;
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
            foreach (var mark in Legend.Where(mark => mark.Public))
            {
                profilePacket.WriteByte((byte)mark.Icon);
                profilePacket.WriteByte((byte)mark.Color);
                profilePacket.WriteString8(mark.Prefix);
                profilePacket.WriteString8(mark.ToString());
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

        public void DisplayIncomingWhisper(string charname, string message)
        {
            Client.SendMessage(string.Format("{0}\" {1}", charname, message), 0x0);
        }

        public void DisplayOutgoingWhisper(string charname, string message)
        {
            Client.SendMessage(string.Format("{0}> {1}", charname, message), 0x0);
        }

        public void SendWhisper(string charname, string message)
        {
            var target = World.FindUser(charname);
            string err = string.Empty;

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
                string err = string.Empty;
                foreach (var member in Group.Members)
                {
                    if (CanTalkTo(member, out err))
                    {
                        member.Client.SendMessage(string.Format("[!{0}] {1}", Name, message), MessageTypes.GROUP);
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

            msg = string.Empty;
            return true;
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (obj is User)
            {
                var user = obj as User;
                SendUpdateToUser(user.Client);
            }
            else if (obj is ItemObject)
            {
                var item = obj as ItemObject;
                SendVisibleItem(item);

            }
        }

        public void SendVisibleGold(Gold gold)
        {
            Logger.DebugFormat("Sending add visible ItemObject packet");
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
            if (castable.OnCooldown)
            {
                SendSystemMessage("You must wait longer to use that.");
                return;
            }
            if (UseCastable(castable))
            {
                Client.Enqueue(new ServerPacketStructures.Cooldown()
                {
                    Length = (uint)castable.Cooldown,
                    Pane = 1,
                    Slot = slot
                }.Packet());
            }
            else
                SendSystemMessage("Failed.");
        }

        internal void UseSpell(byte slot, uint target = 0)
        {
            var castable = SpellBook[slot];
            Creature targetCreature = Map.EntityTree.OfType<Creature>().SingleOrDefault(x => x.Id == target) ?? null;

            if (castable.OnCooldown)
            {
                SendSystemMessage("You must wait longer to use that.");
                return;
            }

            if (UseCastRestrictions.Contains(castable.Categories.Category.Value))
            {
                SendSystemMessage("You cannot cast that now.");
                return;
            }
            if (UseCastable(castable, targetCreature))
            {
                SpellBook[slot].LastCast = DateTime.Now;
                Client.Enqueue(new ServerPacketStructures.Cooldown()
                {
                    Length = (uint)castable.Cooldown,
                    Pane = 0,
                    Slot = slot
                }.Packet());
            }
            else
                SendSystemMessage("Failed.");
        }

        /// <summary>
        /// Process the casting cost for a castable. If all requirements were not met, return false.
        /// </summary>
        /// <param name="castable">The castable that is being cast.</param>
        /// <returns>True or false depending on success.</returns>
        public bool ProcessCastingCost(Castable castable)
        {
            if (castable.CastCosts?.CastCost == null) return true;

            var costs = castable.CastCosts.CastCost;
            uint reduceHp = 0;
            uint reduceMp = 0;
            int removeNumItems = 0;

            // HP cost can be either a percentage (0.25) or a fixed amount (50)
            if (costs.Stat?.Hp != null)
                if (costs.Stat.Hp.Contains('.'))
                    reduceHp = (uint) Math.Ceiling(Convert.ToDouble(costs.Stat.Hp) * this.MaximumHp);
                else 
                    reduceHp = Convert.ToUInt32(costs.Stat.Hp);
            if (costs.Stat?.Mp != null)
                if (costs.Stat.Mp.Contains('.'))
                    reduceMp = (uint)Math.Ceiling(Convert.ToDouble(costs.Stat.Mp) * this.MaximumMp);
                else
                    reduceMp = Convert.ToUInt32(costs.Stat.Mp);

            
            if (costs.Items != null)
            {
                foreach (var item in costs.Items)
                {
                    if (Inventory.Contains(item.Value, item.Quantity)) removeNumItems++;
                }
            }

            // Check that all requirements are met first. Note that a spell cannot be cast if its HP cost would result
            // in the caster's HP being reduced to zero.
            if (reduceHp > Hp || reduceMp >= Mp || costs.Gold > Gold || (removeNumItems != 0 && costs.Items.Count == removeNumItems)) return false;

            if (costs.Gold > this.Gold) return false;

            if (reduceHp != 0) this.Hp -= reduceHp;
            if (reduceMp != 0) this.Mp -= reduceMp;
            if ((int)costs.Gold > 0 ) this.RemoveGold(new Gold(costs.Gold));
            if (removeNumItems > 0)
            {
                foreach(var item in costs.Items)
                {
                    RemoveItem(item.Value, item.Quantity);
                }                
            }
            UpdateAttributes(StatUpdateFlags.Current);
            return true;
        }

        public void SendVisibleItem(ItemObject itemObject)
        {
            Logger.DebugFormat("Sending add visible ItemObject packet");
            var x07 = new ServerPacket(0x07);
            x07.WriteUInt16(1); // Anything but 0x0001 does nothing or makes client crash
            x07.WriteUInt16(itemObject.X);
            x07.WriteUInt16(itemObject.Y);
            x07.WriteUInt32(itemObject.Id);
            x07.WriteUInt16((ushort)(itemObject.Sprite + 0x8000));
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
            x07.WriteByte(0); // Unknown what this is
            x07.WriteByte(0);
            x07.WriteByte(0);
            x07.WriteByte(0);
            x07.WriteByte((byte)creature.Direction);
            x07.WriteByte(0);
            x07.WriteByte(0);
            x07.WriteString8(creature.Name);
            x07.DumpPacket();
            Enqueue(x07);


        }

        public void SendUpdateToUser(Client client)
        {
            var offset = Equipment.Armor?.BodyStyle ?? 0;
            if (!Condition.Alive)
                offset += 0x20;

            Logger.Debug($"Offset is: {offset.ToString("X")}");
            // Figure out what we're sending as the "helmet"
            var helmet = Equipment.Helmet?.DisplaySprite ?? HairStyle;
            helmet = Equipment.DisplayHelm?.DisplaySprite ?? helmet;

            client.Enqueue(new ServerPacketStructures.DisplayUser()
            {
                X = X,
                Y = Y,
                Direction = Direction,
                Id = Id,
                Sex = Sex,
                Helmet = helmet,
                Weapon = Equipment.Weapon?.DisplaySprite ?? 0,
                Armor = (Equipment.Armor?.DisplaySprite ?? 0),
                BodySpriteOffset = offset,
                Boots = (byte)(Equipment.Boots?.DisplaySprite ?? 0),
                BootsColor = (byte)(Equipment.Boots?.Color ?? 0),
                DisplayAsMonster = DisplayAsMonster,
                FaceShape = FaceShape,
                FirstAcc = Equipment.FirstAcc?.DisplaySprite ?? 0,
                SecondAcc = Equipment.SecondAcc?.DisplaySprite ?? 0,
                ThirdAcc = Equipment.ThirdAcc?.DisplaySprite ?? 0,
                FirstAccColor = Equipment.FirstAcc?.Color ?? 0,
                SecondAccColor = Equipment.SecondAcc?.Color ?? 0,
                ThirdAccColor = Equipment.ThirdAcc?.Color ?? 0,
                LanternSize = LanternSize,
                RestPosition = RestPosition,
                Overcoat = Equipment.Overcoat?.DisplaySprite ?? 0,
                OvercoatColor = Equipment.Overcoat?.Color ?? 0,
                SkinColor = SkinColor,
                Invisible = Transparent,
                NameStyle = NameStyle,
                Name = Name,
                GroupName = string.Empty, // TODO: Group name
                MonsterSprite = MonsterSprite,
                HairColor = HairColor
            }.Packet());
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
        /// Sends an equip ItemObject packet to the client, triggering an update of the detail window ('a').
        /// </summary>
        /// <param name="itemObject">The ItemObject which will be equipped.</param>
        /// <param name="slot">The slot in which we are equipping.</param>
        public void SendEquipItem(ItemObject itemObject, int slot)
        {
            // Update the client.
            // ServerPacket type: 0x37
            // byte: index
            // Uint16: sprite offset (79 FF is actually a red scroll, 80 00 onwards are real items)
            // Byte: ??
            // Byte: ItemObject Name length
            // string: ItemObject Name
            // Uint32: Max Durability
            // Uint32: Min Durability

            if (itemObject == null)
            {
                SendRefreshEquipmentSlot(slot);
                return;
            }

            var equipPacket = new ServerPacket(0x37);
            equipPacket.WriteByte((byte)slot);
            equipPacket.WriteUInt16((ushort)(itemObject.Sprite + 0x8000));
            equipPacket.WriteByte(0x00);
            equipPacket.WriteStringWithLength(itemObject.Name);
            equipPacket.WriteByte(0x00);
            equipPacket.WriteUInt32(itemObject.MaximumDurability);
            equipPacket.WriteUInt32(itemObject.Durability);
            equipPacket.DumpPacket();
            Enqueue(equipPacket);
        }

        /// <summary>
        /// Sends a clear ItemObject packet to the connected client for the specified slot. 
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
        public void SendClearSpell(int slot)
        {
            var x2D = new ServerPacket(0x18);
            x2D.WriteByte((byte)slot);
            Enqueue(x2D);
        }

        /// <summary>
        /// Send an ItemObject update packet (essentially placing the ItemObject in a given slot, as far as the client is concerned.
        /// </summary>
        /// <param name="itemObject">The ItemObject we are sending to the user.</param>
        /// <param name="slot">The client's ItemObject slot.</param>
        public void SendItemUpdate(ItemObject itemObject, int slot)
        {
            if (itemObject == null)
            {
                SendClearItem(slot);
                return;
            }

            Logger.DebugFormat("Adding {0} qty {1} to slot {2}",
                itemObject.Name, itemObject.Count, slot);
            var x0F = new ServerPacket(0x0F);
            x0F.WriteByte((byte)slot);
            x0F.WriteUInt16((ushort)(itemObject.Sprite + 0x8000));
            x0F.WriteByte(0x00);
            x0F.WriteString8(itemObject.Name);
            x0F.WriteInt32(itemObject.Count);  //amount
            x0F.WriteBoolean(itemObject.Stackable);
            x0F.WriteUInt32(itemObject.MaximumDurability);  //maxdura
            x0F.WriteUInt32(itemObject.Durability);  //curdura
            x0F.WriteUInt32(0x00);  //?
            Enqueue(x0F);
        }

        public void SendSkillUpdate(Castable item, int slot)
        {
            if (item == null)
            {
                SendClearSkill(slot);
                return;
            }
            Logger.DebugFormat("Adding skill {0} to slot {2}",
                item.Name, slot);
            var x2C = new ServerPacket(0x2C);
            x2C.WriteByte((byte)slot);
            x2C.WriteUInt16((ushort)(item.Icon));
            x2C.WriteString8(Class == Enums.Class.Peasant ? item.Name : $"{item.Name} (Lev:{item.CastableLevel}/{GetCastableMaxLevel(item)})");
            Enqueue(x2C);
        }

        public void SendSpellUpdate(Castable item, int slot)
        {
            if (item == null)
            {
                SendClearSpell(slot);
                return;
            }
            Logger.DebugFormat("Adding spell {0} to slot {2}",
                item.Name, slot);
            var x17 = new ServerPacket(0x17);
            x17.WriteByte((byte)slot);
            x17.WriteUInt16((ushort)(item.Icon));
            var spellType = item.Intents[0].UseType;
            //var spellType = isClick ? 2 : 5;
            x17.WriteByte((byte)spellType); //spell type? how are we determining this?
            x17.WriteString8(Class == Enums.Class.Peasant ? item.Name : $"{item.Name} (Lev:{item.CastableLevel}/{GetCastableMaxLevel(item)})");
            x17.WriteString8(item.Name); //prompt? what is this?
            x17.WriteByte((byte)item.Lines);
            Enqueue(x17);
        }

        public void SetFlag(string flag, string value)
        {
            UserFlags[flag] = value;
        }

        public void SetSessionFlag(string flag, string value)
        {
            UserSessionFlags[flag] = value;
        }

        public string GetFlag(string flag)
        {
            string value;
            if (UserFlags.TryGetValue(flag, out value))
            {
                return value;
            }
            return string.Empty;
        }


        public string GetSessionFlag(string flag)
        {
            string value;
            if (UserSessionFlags.TryGetValue(flag, out value))
            {
                return value;
            }
            return string.Empty;
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
                x08.WriteByte((byte)(Condition.Blinded ? 0x08 : 0x00));
                x08.WriteByte(0); // Unknown
                x08.WriteByte(0); // Unknown
                x08.WriteByte(0); // Unknown
                x08.WriteByte((byte)(Mailbox.HasUnreadMessages ? 0x10 : 0x00));
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

        public int GetCastableMaxLevel(Castable castable) => IsMaster ? 100 : castable.GetMaxLevelByClass((Castables.Class)Class);


        public User GetFacingUser()
        {
            List<VisibleObject> contents;

            switch (Direction)
            {
                case Direction.North:
                    contents = Map.GetTileContents(X, Y - 1);
                    break;
                case Direction.South:
                    contents = Map.GetTileContents(X, Y + 1);
                    break;
                case Direction.West:
                    contents = Map.GetTileContents(X - 1, Y);
                    break;
                case Direction.East:
                    contents = Map.GetTileContents(X + 1, Y);
                    break;
                default:
                    contents = new List<VisibleObject>();
                    break;
            }

            return (User)contents.FirstOrDefault(y => y is User);
        }

        /// <summary>
        /// Returns all the objects that are directly facing the user.
        /// </summary>
        /// <returns>A list of visible objects.</returns>
        public List<VisibleObject> GetFacingObjects()
        {
            List<VisibleObject> contents;

            switch (Direction)
            {
                case Direction.North:
                    contents = Map.GetTileContents(X, Y - 1);
                    break;
                case Direction.South:
                    contents = Map.GetTileContents(X, Y + 1);
                    break;
                case Direction.West:
                    contents = Map.GetTileContents(X - 1, Y);
                    break;
                case Direction.East:
                    contents = Map.GetTileContents(X + 1, Y);
                    break;
                default:
                    contents = new List<VisibleObject>();
                    break;
            }

            return contents;
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

        public void RecalculateBonuses()
        {
            foreach (var item in Equipment)
                ApplyBonuses(item);
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
            // Quantity check - if we already have an ItemObject with the same name, will
            // adding the MaximumStack)

            if (SkillBook.Contains(item.Id))
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
            return AddSpell(castable, SpellBook.FindEmptySlot());
        }

        public bool AddSpell(Castable item, byte slot)
        {
            // Quantity check - if we already have an ItemObject with the same name, will
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

        public bool AddItem(ItemObject itemObject, bool updateWeight = true)
        {
            if (Inventory.IsFull)
            {
                SendSystemMessage("You cannot carry any more items.");
                Map.Insert(itemObject, X, Y);
                return false;
            }
            return AddItem(itemObject, Inventory.FindEmptySlot(), updateWeight);
        }

        public bool AddItem(ItemObject itemObject, byte slot, bool updateWeight = true)
        {
            // Weight check

            if (itemObject.Weight + CurrentWeight > MaximumWeight)
            {
                SendSystemMessage("It's too heavy.");
                Map.Insert(itemObject, X, Y);
                return false;
            }

            // Quantity check - if we already have an ItemObject with the same name, will
            // adding the MaximumStack)

            var inventoryItem = Inventory.Find(itemObject.Name);

            if (inventoryItem != null && itemObject.Stackable)
            {
                if (itemObject.Count + inventoryItem.Count > inventoryItem.MaximumStack)
                {
                    itemObject.Count = (inventoryItem.Count + itemObject.Count) - inventoryItem.MaximumStack;
                    inventoryItem.Count = inventoryItem.MaximumStack;
                    SendSystemMessage(string.Format("You can't carry any more {0}", itemObject.Name));
                    Map.Insert(itemObject, X, Y);
                    return false;
                }

                // Merge stack and destroy "added" ItemObject
                inventoryItem.Count += itemObject.Count;
                itemObject.Count = 0;
                SendItemUpdate(inventoryItem, Inventory.SlotOf(inventoryItem.Name).First());
                World.Remove(itemObject);
                return true;
            }

            Logger.DebugFormat("Attempting to add ItemObject to inventory slot {0}", slot);


            if (!Inventory.Insert(slot, itemObject))
            {
                Logger.DebugFormat("Slot was invalid or not null");
                Map.Insert(itemObject, X, Y);
                return false;
            }

            SendItemUpdate(itemObject, slot);
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

        public bool RemoveItem(string itemName, byte quantity = 0x01, bool updateWeight = true)
        {
            if (Inventory.Contains(itemName, quantity))
            {
                var remaining = (int)quantity;
                var slots = Inventory.SlotOf(itemName);
                foreach (var i in slots)
                {
                    if (remaining > 0)
                    {
                        if (Inventory[i].Stackable)
                        {
                            if (Inventory[i].Count <= remaining)
                            {
                                remaining -= Inventory[i].Count;
                                Inventory[i].Remove();
                                SendClearItem(i);
                            }
                            if (Inventory[i].Count > remaining)
                            {
                                Inventory[i].Count -= remaining;
                                remaining = 0;
                                SendItemUpdate(Inventory[i], i);
                            }
                        }
                        else
                        {
                            Inventory.Remove(i);
                            remaining--;
                            SendItemUpdate(Inventory[i], i);
                        }
                    }
                    else
                    {
                        break;
                    }
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

        public bool AddEquipment(ItemObject itemObject, byte slot, bool sendUpdate = true)
        {
            Logger.DebugFormat("Adding equipment to slot {0}", slot);

            if (!Equipment.Insert(slot, itemObject))
            {
                Logger.DebugFormat("Slot wasn't null, aborting");
                return false;
            }

            SendEquipItem(itemObject, slot);
            Client.SendMessage(string.Format("Equipped {0}", itemObject.Name), 3);
            ApplyBonuses(itemObject);
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

        public void SwapCastable(byte oldSlot, byte newSlot, Book book)
        {
            if (book == SkillBook)
            {
                SkillBook.Swap(oldSlot, newSlot);
                SendSkillUpdate(SkillBook[oldSlot], oldSlot);
                SendSkillUpdate(SkillBook[newSlot], newSlot);
            }
            else
            {
                SpellBook.Swap(oldSlot, newSlot);
                SendSpellUpdate(SpellBook[oldSlot], oldSlot);
                SendSpellUpdate(SpellBook[newSlot], newSlot);
            }
        }

        public override void RegenerateMp(double mp, Creature regenerator = null)
        {
            base.RegenerateMp(mp, regenerator);
            UpdateAttributes(StatUpdateFlags.Current);
        }

        public override void Damage(double damage, Enums.Element element = Enums.Element.None,
            Enums.DamageType damageType = Enums.DamageType.Direct, Castables.DamageFlags damageFlags = Castables.DamageFlags.None, Creature attacker = null, bool onDeath=true)
        {
            if (Condition.Comatose || !Condition.Alive) return;
            base.Damage(damage, element, damageType, damageFlags, attacker);
            if (Hp == 0)
            {
                if (Group != null) {
                    Hp = 1;
                    var handler = Game.Config.Handlers?.Death?.Coma;
                    if (handler != null && World.WorldData.TryGetValueByIndex(handler.Value, out Status status))
                        ApplyStatus(new CreatureStatus(status, this));
                    else
                    {
                        Logger.Warn("No coma handler or status found - user {Name} died!");
                        OnDeath();
                    }
                }
                else
                    OnDeath();
            }
            UpdateAttributes(StatUpdateFlags.Current);
        }

        public override void Heal(double heal, Creature source = null)
        {
            base.Heal(heal, source);
            if (this is User) { UpdateAttributes(StatUpdateFlags.Current); }
        }



        public override bool UseCastable(Castable castObject, Creature target = null)
        {
            if (!ProcessCastingCost(castObject)) return false;
            if (base.UseCastable(castObject, target))
            {
                // This may need to occur elsewhere, depends on how it looks in game
                if (castObject.TryGetMotion((Class)Class, out Motion motion))
                    SendMotion(Id, motion.Id, motion.Speed);
                return true;
            }
            return false;
        }

        public void AssailAttack(Direction direction, Creature target = null)
        {
            if (target == null)
                target = GetDirectionalTarget(direction);

            foreach (var c in SkillBook.Where(c => c.IsAssail))
            {
                if (target != null && target.GetType() != typeof(Merchant))
                {
                    UseCastable(c, target);
                }
            }
            //animation handled here as to not repeatedly send assails.
            var firstAssail = SkillBook.FirstOrDefault(x => x.IsAssail);
            var motion = firstAssail?.Effects.Animations.OnCast.Player.FirstOrDefault(y => y.Class.Contains((Class)Class));

            var motionId = motion != null ? (byte)motion.Id : (byte)1;
            var assail = new ServerPacketStructures.PlayerAnimation() { Animation = motionId, Speed = 20, UserId = this.Id };
            var soundId = firstAssail != null ? firstAssail.Effects.Sound.Id : (byte)1;
            Enqueue(assail.Packet());
            PlaySound(soundId);
            SendAnimation(assail.Packet());
            PlaySound(soundId);
        }


        private string GroupProfileSegment()
        {
            var sb = new StringBuilder();

            // Only build this string if the user's in a group. Otherwise an empty
            // string should be sent.
            if (!Grouped) return sb.ToString();
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
            sb.Append($"Total {Group.Members.Count}");

            return sb.ToString();
        }

        /// <summary>
        /// Send a player's profile to themselves (e.g. click on self or hit Y for group info)
        /// </summary>
        public void SendProfile()
        {
            var profilePacket = new ServerPacket(0x39);
            profilePacket.WriteByte((byte)Nation.Flag); // citizenship
            profilePacket.WriteString8(Guild.Rank);
            profilePacket.WriteString8(Guild.Title);
            profilePacket.WriteString8(GroupText);
            profilePacket.WriteBoolean(Grouping);
            profilePacket.WriteByte(0); // ??
            profilePacket.WriteByte((byte)Class);
            //            profilePacket.WriteByte(1); // ??
            profilePacket.WriteByte(0);
            profilePacket.WriteByte(0); // ??
            profilePacket.WriteString8(IsMaster ? "Master" : Hybrasyl.Constants.REVERSE_CLASSES[(int)Class]);
            profilePacket.WriteString8(Guild.Name);
            profilePacket.WriteByte((byte)Legend.Count);
            foreach (var mark in Legend)
            {
                profilePacket.WriteByte((byte)mark.Icon);
                profilePacket.WriteByte((byte)mark.Color);
                profilePacket.WriteString8(mark.Prefix);
                profilePacket.WriteString8(mark.ToString());
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
            Logger.DebugFormat("SendMotion id {0}, motion {1}, speed {2}", id, motion, speed);
            var x1A = new ServerPacket(0x1A);
            x1A.WriteUInt32(id);
            x1A.WriteByte(motion);
            x1A.WriteInt16(speed);
            x1A.WriteByte(0xFF);
            Enqueue(x1A);
        }

        public void SendEffect(uint id, ushort effect, short speed)
        {
            Logger.DebugFormat("SendEffect: id {0}, effect {1}, speed {2} ", id, effect, speed);
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
            Logger.DebugFormat("SendEffect: targetId {0}, targetEffect {1}, srcId {2}, srcEffect {3}, speed {4}",
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
            Logger.DebugFormat("SendEffect: x {0}, y {1}, effect {2}, speed {3}", x, y, effect, speed);
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
            Logger.DebugFormat("SendSound {0}", sound);
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

        public void ShowLearnSkillMenu(Merchant merchant)
        {
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill");
            var prompt = string.Empty;
            if (learnString != null) prompt = learnString.Value ?? string.Empty;

            var merchantSkills = new MerchantSkills();
            merchantSkills.Skills = new List<MerchantSkill>();
            var skillsCount = 0;
            foreach (var skill in merchant.Roles.Train.Where(x => x.Type == "Skill").OrderBy(y => y.Name))
            {
                if (SkillBook.Contains(Game.World.WorldData.GetByIndex<Castable>(skill.Name))) continue;
                skillsCount++;
                var worldSkill = Game.World.WorldData.GetByIndex<Castable>(skill.Name);
                merchantSkills.Skills.Add(new MerchantSkill()
                {
                    IconType = 3,
                    Icon = worldSkill.Icon,
                    Color = 1,
                    Name = worldSkill.Name
                });
            }
            merchantSkills.SkillsCount = (ushort)skillsCount;
            merchantSkills.Id = (ushort)MerchantMenuItem.LearnSkill;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.MerchantSkills,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Skills = merchantSkills
            };

            Enqueue(packet.Packet());
        }

        public void ShowForgetSkillMenu(Merchant merchant)
        {
            var forgetString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "forget_skill");
            var prompt = string.Empty;
            if (forgetString != null) prompt = forgetString.Value ?? string.Empty;

            var userSkills = new UserSkillBook();
            userSkills.Id = (ushort)MerchantMenuItem.ForgetSkillAccept;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.UserSkillBook,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                UserSkills = userSkills
            };

            Enqueue(packet.Packet());
        }

        public void ShowForgetSkillAccept(Merchant merchant, byte slot)
        {
            var forgetString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "forget_castable_success");
            var prompt = forgetString.Value;

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();

            options.OptionsCount = (byte)options.Options.Count;
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };
            Enqueue(packet.Packet());

            SkillBook.Remove(slot);
            SendClearSkill(slot);
        }

        public void ShowForgetSpellMenu(Merchant merchant)
        {
            var forgetString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "forget_spell");
            var prompt = string.Empty;
            if (forgetString != null) prompt = forgetString.Value ?? string.Empty;

            var userSpells = new UserSpellBook();
            userSpells.Id = (ushort)MerchantMenuItem.ForgetSpellAccept;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.UserSpellBook,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                UserSpells = userSpells
            };

            Enqueue(packet.Packet());
        }

        public void ShowForgetSpellAccept(Merchant merchant, byte slot)
        {
            var forgetString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "forget_castable_success");
            var prompt = forgetString.Value;

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();

            options.OptionsCount = (byte)options.Options.Count;
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };
            Enqueue(packet.Packet());

            SpellBook.Remove(slot);
            SendClearSpell(slot);
        }

        public void ShowLearnSkill(Merchant merchant, Castable castable)
        {
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_choice");
            var skillDesc = castable.Descriptions.Single(x => x.Class.Contains((Class)Class) || x.Class.Contains(Castables.Class.Peasant));
            var prompt = learnString.Value.Replace("$SKILLNAME", castable.Name).Replace("$SKILLDESC", skillDesc.Value);

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();

            options.Options.Add(new MerchantDialogOption()
            {
                Id = (ushort)MerchantMenuItem.LearnSkillAgree,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption()
            {
                Id = (ushort)MerchantMenuItem.LearnSkillDisagree,
                Text = "No"
            });

            options.OptionsCount = (byte)options.Options.Count;
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            PendingLearnableCastable = castable;

            Enqueue(packet.Packet());
        }

        public void ShowLearnSkillAgree(Merchant merchant)
        {
            var castable = PendingLearnableCastable;
            //now check requirements.
            var classReq = castable.Requirements.Single(x => x.Class.Contains((Class)Class) || Class == Enums.Class.Peasant);
            String learnString = null;
            MerchantOptions options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var prompt = string.Empty;
            if (classReq.Level.Min > Level)
            {
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_player_level");
                prompt = learnString.Value.Replace("$SKILLNAME", castable.Name).Replace("$LEVEL", classReq.Level.Min.ToString());
            }
            if (classReq.Physical != null)
            {
                if (Str < classReq.Physical.Str || Int < classReq.Physical.Int || Wis < classReq.Physical.Wis || Con < classReq.Physical.Con || Dex < classReq.Physical.Dex)
                {
                    learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_prereq_stats");
                    var statStr = $"\n[STR {classReq.Physical.Str} INT {classReq.Physical.Int} WIS {classReq.Physical.Wis} CON {classReq.Physical.Con} DEX {classReq.Physical.Dex}]";
                    prompt = learnString.Value.Replace("$SKILLNAME", castable.Name).Replace("$STATS", statStr);
                }
            }
            if (classReq.Prerequisites != null)
            {
                foreach (var preReq in classReq.Prerequisites)
                {
                    if (!SkillBook.Contains(Game.World.WorldData.GetByIndex<Castable>(preReq.Value)))
                    {
                        learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_prereq_level");
                        prompt = learnString.Value.Replace("$SKILLNAME", preReq.Value).Replace("$PREREQ", preReq.Level.ToString());
                        break;
                    }
                    else if (SkillBook.Contains(Game.World.WorldData.GetByIndex<Castable>(preReq.Value)))
                    {
                        var preReqSkill = SkillBook.Single(x => x.Name == preReq.Value);
                        if (preReqSkill.CastableLevel < preReq.Level)
                        {
                            learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_prereq_level");
                            prompt = learnString.Value.Replace("$SKILLNAME", preReq.Value).Replace("$PREREQ", preReq.Level.ToString());
                            break;
                        }
                    }
                }
            }
            if (prompt == string.Empty) //this is so bad
            {
                var reqStr = string.Empty;
                //now we can learning!
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_reqs");
                reqStr = classReq.Items.Aggregate(reqStr, (current, req) => current + (req.Value + "(" + req.Quantity + "), "));

                if (classReq.Gold != 0)
                {
                    reqStr += classReq.Gold + " coins";
                }
                else
                {
                    reqStr = reqStr.Remove(reqStr.Length - 1);
                }
                prompt = learnString.Value.Replace("$SKILLNAME", castable.Name).Replace("$REQS", reqStr);

                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.LearnSkillAccept,
                    Text = "Yes"
                });
                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.LearnSkillDisagree,
                    Text = "No"
                });

            }

            options.OptionsCount = (byte)options.Options.Count;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());

        }

        public void ShowLearnSkillAccept(Merchant merchant)
        {
            var castable = PendingLearnableCastable;
            var classReq = castable.Requirements.Single(x => x.Class.Contains((Class)Class) || Class == Enums.Class.Peasant);
            String learnString;
            var prompt = string.Empty;
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            //verify user has required items.
            if (!(Gold > classReq.Gold))
            {
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_prereq_gold");
                prompt = learnString.Value;
            }
            if (prompt == string.Empty)
            {
                if (classReq.Items.Any(itemReq => !Inventory.Contains(itemReq.Value, itemReq.Quantity)))
                {
                    learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_prereq_item");
                    prompt = learnString.Value;
                }
            }
            if (prompt == string.Empty)
            {
                RemoveGold(classReq.Gold);
                foreach (var req in classReq.Items)
                {
                    RemoveItem(req.Value, req.Quantity);
                }
                SkillBook.Add(castable);
                SendInventory();
                SendSkills();
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_success");
                prompt = learnString.Value;
            }
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());
        }

        public void ShowLearnSkillDisagree(Merchant merchant)
        {
            PendingLearnableCastable = null;
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "forget_castable_success");
            var prompt = learnString.Value;
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());
        }

        public void ShowLearnSpellMenu(Merchant merchant)
        {
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell");
            var prompt = string.Empty;
            if (learnString != null) prompt = learnString.Value ?? string.Empty;

            var merchantSpells = new MerchantSpells();
            merchantSpells.Spells = new List<MerchantSpell>();
            var spellsCount = 0;
            foreach (var spell in merchant.Roles.Train.Where(x => x.Type == "Spell").OrderBy(y => y.Name))
            {
                if (SpellBook.Contains(Game.World.WorldData.GetByIndex<Castable>(spell.Name))) continue;
                spellsCount++;
                var worldSpell = Game.World.WorldData.GetByIndex<Castable>(spell.Name);
                merchantSpells.Spells.Add(new MerchantSpell()
                {
                    IconType = 2,
                    Icon = worldSpell.Icon,
                    Color = 1,
                    Name = worldSpell.Name
                });
            }
            merchantSpells.SpellsCount = (ushort)spellsCount;
            merchantSpells.Id = (ushort)MerchantMenuItem.LearnSpell;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.MerchantSpells,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Spells = merchantSpells
            };

            Enqueue(packet.Packet());
        }

        public void ShowLearnSpell(Merchant merchant, Castable castable)
        {
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_choice");
            var skillDesc = castable.Descriptions.Single(x => x.Class.Contains((Class)Class) || x.Class.Contains(Castables.Class.Peasant));
            var prompt = learnString.Value.Replace("$SPELLNAME", castable.Name).Replace("$SPELLDESC", skillDesc.Value);

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();

            options.Options.Add(new MerchantDialogOption()
            {
                Id = (ushort)MerchantMenuItem.LearnSpellAgree,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption()
            {
                Id = (ushort)MerchantMenuItem.LearnSpellDisagree,
                Text = "No"
            });

            options.OptionsCount = (byte)options.Options.Count;
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            PendingLearnableCastable = castable;

            Enqueue(packet.Packet());
        }

        public void ShowLearnSpellAgree(Merchant merchant)
        {
            var castable = PendingLearnableCastable;
            //now check requirements.
            var classReq = castable.Requirements.Single(x => x.Class.Contains((Class)Class) || Class == Enums.Class.Peasant);
            String learnString = null;
            MerchantOptions options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var prompt = string.Empty;
            if (classReq.Level.Min > Level)
            {
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_player_level");
                prompt = learnString.Value.Replace("$SPELLNAME", castable.Name).Replace("$LEVEL", classReq.Level.Min.ToString());
            }
            if (classReq.Physical != null)
            {
                if (Str < classReq.Physical.Str || Int < classReq.Physical.Int || Wis < classReq.Physical.Wis || Con < classReq.Physical.Con || Dex < classReq.Physical.Dex)
                {
                    learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_prereq_stats");
                    var statStr = $"\n[STR {classReq.Physical.Str} INT {classReq.Physical.Int} WIS {classReq.Physical.Wis} CON {classReq.Physical.Con} DEX {classReq.Physical.Dex}]";
                    prompt = learnString.Value.Replace("$SPELLNAME", castable.Name).Replace("$STATS", statStr);
                }
            }
            if (classReq.Prerequisites != null)
            {
                foreach (var preReq in classReq.Prerequisites)
                {
                    if (!SkillBook.Contains(Game.World.WorldData.GetByIndex<Castable>(preReq.Value)))
                    {
                        learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_prereq_level");
                        prompt = learnString.Value.Replace("$SPELLNAME", preReq.Value).Replace("$PREREQ", preReq.Level.ToString());
                        break;
                    }
                    else if (SkillBook.Contains(Game.World.WorldData.GetByIndex<Castable>(preReq.Value)))
                    {
                        var preReqSkill = SkillBook.Single(x => x.Name == preReq.Value);
                        if (preReqSkill.CastableLevel < preReq.Level)
                        {
                            learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_prereq_level");
                            prompt = learnString.Value.Replace("$SPELLNAME", preReq.Value).Replace("$PREREQ", preReq.Level.ToString());
                            break;
                        }
                    }
                }
            }
            if (prompt == string.Empty) //this is so bad
            {
                var reqStr = string.Empty;
                //now we can learning!
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_reqs");
                reqStr = classReq.Items.Aggregate(reqStr, (current, req) => current + (req.Value + "(" + req.Quantity + "), "));

                if (classReq.Gold != 0)
                {
                    reqStr += classReq.Gold + " coins";
                }
                else
                {
                    reqStr = reqStr.Remove(reqStr.Length - 1);
                }
                prompt = learnString.Value.Replace("$SPELLNAME", castable.Name).Replace("$REQS", reqStr);

                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.LearnSkillAccept,
                    Text = "Yes"
                });
                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.LearnSkillDisagree,
                    Text = "No"
                });

            }

            options.OptionsCount = (byte)options.Options.Count;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());

        }

        public void ShowLearnSpellAccept(Merchant merchant)
        {
            var castable = PendingLearnableCastable;
            var classReq = castable.Requirements.Single(x => x.Class.Contains((Class)Class) || Class == Enums.Class.Peasant);
            String learnString;
            var prompt = string.Empty;
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            //verify user has required items.
            if (!(Gold > classReq.Gold))
            {
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_prereq_gold");
                prompt = learnString.Value;
            }
            if (prompt == string.Empty)
            {
                if (classReq.Items.Any(itemReq => !Inventory.Contains(itemReq.Value, itemReq.Quantity)))
                {
                    learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_prereq_item");
                    prompt = learnString.Value;
                }
            }
            if (prompt == string.Empty)
            {
                RemoveGold(classReq.Gold);
                foreach (var req in classReq.Items)
                {
                    RemoveItem(req.Value, req.Quantity);
                }
                SkillBook.Add(castable);
                SendInventory();
                SendSkills();
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_success");
                prompt = learnString.Value;
            }
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());
        }

        public void ShowLearnSpellDisagree(Merchant merchant)
        {
            PendingLearnableCastable = null;
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "forget_castable_success");
            var prompt = learnString.Value;
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());
        }

        public void ShowBuyMenu(Merchant merchant)
        {
            var buyString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "buy");
            var prompt = string.Empty;
            if (buyString != null) prompt = buyString.Value ?? string.Empty;

            var merchantItems = new MerchantShopItems();
            merchantItems.Items = new List<MerchantShopItem>();
            var itemsCount = 0;
            foreach (var item in merchant.Roles.Vend.Items)
            {
                var worldItem = Game.World.WorldData.GetByIndex<Item>(item.Name);
                merchantItems.Items.Add(new MerchantShopItem()
                {
                    Tile = (ushort)(0x8000 + worldItem.Properties.Appearance.Sprite),
                    Color = (byte)worldItem.Properties.Appearance.Color,
                    Description = worldItem.Properties.Vendor?.Description ?? "",
                    Name = worldItem.Name,
                    Price = worldItem.Properties.Physical.Value

                });
                itemsCount++;
            }
            merchantItems.ItemsCount = (ushort)itemsCount;
            merchantItems.Id = (ushort)MerchantMenuItem.BuyItemQuantity;


            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.MerchantShopItems,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                ShopItems = merchantItems
            };
            Enqueue(packet.Packet());
        }

        public void ShowBuyMenuQuantity(Merchant merchant, string name)
        {
            var item = Game.World.WorldData.GetByIndex<Item>(name);
            PendingBuyableItem = name;
            if (item.Stackable)
            {
                var buyString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "buy_quantity");
                var prompt = string.Empty;
                if (buyString != null) prompt = buyString.Value ?? string.Empty;

                var input = new MerchantInput();

                input.Id = (ushort)MerchantMenuItem.BuyItemAccept;


                var packet = new ServerPacketStructures.MerchantResponse()
                {
                    MerchantDialogType = MerchantDialogType.Input,
                    MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                    ObjectId = merchant.Id,
                    Tile1 = (ushort)(0x4000 + merchant.Sprite),
                    Color1 = 0,
                    Tile2 = (ushort)(0x4000 + merchant.Sprite),
                    Color2 = 0,
                    PortraitType = 0,
                    Name = merchant.Name,
                    Text = prompt,
                    Input = input
                };
                Enqueue(packet.Packet());
            }
            else //buy item
            {
                ShowBuyItem(merchant);
            }
            //var x2F = new ServerPacket(0x2F);
            //x2F.WriteByte(0x03); // type!
            //x2F.WriteByte(0x01); // obj type
            //x2F.WriteUInt32(merchant.Id);
            //x2F.WriteByte(0x01); // ??
            //x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            //x2F.WriteByte(0x00); // color
            //x2F.WriteByte(0x01); // ??
            //x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            //x2F.WriteByte(0x00); // color
            //x2F.WriteByte(0x00); // ??
            //x2F.WriteString8(merchant.Name);
            //x2F.WriteString16(string.Format("How many {0} would you like to buy?", name));
            //x2F.WriteString8(name);
            //x2F.WriteUInt16((ushort)MerchantMenuItem.BuyItemQuantity);
            //Enqueue(x2F);
        }

        public void ShowBuyItem(Merchant merchant, int quantity = 1)
        {
            String buyString;
            var prompt = string.Empty;
            var item = Game.World.WorldData.GetByIndex<Item>(PendingBuyableItem);
            var itemObj = Game.World.CreateItem(item.Id);
            var reqGold = (uint)(itemObj.Value * quantity);
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            if (quantity > 10) //TODO: merchants need to hold their current inventory count.
            {
                buyString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "buy_failure_quantity");
                prompt = buyString.Value;
            }
            if (Gold < reqGold)
            {
                buyString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "buy_failure_gold");
                prompt = buyString.Value;
            }

            if (prompt == string.Empty) //this is so bad
            {
                //check if user has item
                var hasItem = Inventory.Contains(itemObj.Name);
                if (hasItem)
                {
                    if (itemObj.Stackable) IncreaseItem(Inventory.SlotOf(itemObj.Name).First(), quantity);
                    else
                    {
                        AddItem(itemObj);
                    }
                }
                else
                {
                    if (itemObj.Stackable)
                    {
                        AddItem(itemObj);
                        IncreaseItem(Inventory.SlotOf(itemObj.Name).First(), quantity - 1);
                    }
                    else
                    {
                        AddItem(itemObj);
                    }
                }
                RemoveGold(reqGold);
            }
            else
            {
                options.OptionsCount = (byte)options.Options.Count;

                var packet = new ServerPacketStructures.MerchantResponse()
                {
                    MerchantDialogType = MerchantDialogType.Options,
                    MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                    ObjectId = merchant.Id,
                    Tile1 = (ushort)(0x4000 + merchant.Sprite),
                    Color1 = 0,
                    Tile2 = (ushort)(0x4000 + merchant.Sprite),
                    Color2 = 0,
                    PortraitType = 0,
                    Name = merchant.Name,
                    Text = prompt,
                    Options = options
                };

                Enqueue(packet.Packet());
            }
        }

        public void ShowSellMenu(Merchant merchant)
        {
            var sellString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "sell");
            var prompt = string.Empty;
            if (sellString != null) prompt = sellString.Value ?? string.Empty;

            var inventoryItems = new UserInventoryItems();
            inventoryItems.InventorySlots = new List<byte>();
            inventoryItems.Id = (ushort)MerchantMenuItem.SellItemQuantity;

            var itemsCount = 0;
            for (byte i = 0; i < Inventory.Size; i++)
            {
                if (Inventory[i] == null) continue;
                if (Inventory[i].Exchangeable && Inventory[i].Durability == Inventory[i].MaximumDurability)
                {
                    inventoryItems.InventorySlots.Add(i);
                    itemsCount++;
                }
            }
            inventoryItems.InventorySlotsCount = (byte)itemsCount;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.UserInventoryItems,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                UserInventoryItems = inventoryItems
            };
            Enqueue(packet.Packet());
        }
        public void ShowSellQuantity(Merchant merchant, byte slot)
        {
            var item = Inventory[slot];
            PendingSellableSlot = slot;
            if (item.Stackable)
            {
                var sellString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "sell_quantity");
                var prompt = string.Empty;
                if (sellString != null) prompt = sellString.Value ?? string.Empty;

                var input = new MerchantInput();

                input.Id = (ushort)MerchantMenuItem.SellItem;

                var packet = new ServerPacketStructures.MerchantResponse()
                {
                    MerchantDialogType = MerchantDialogType.InputWithArgument,
                    MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                    ObjectId = merchant.Id,
                    Tile1 = (ushort)(0x4000 + merchant.Sprite),
                    Color1 = 0,
                    Tile2 = (ushort)(0x4000 + merchant.Sprite),
                    Color2 = 0,
                    PortraitType = 0,
                    Name = merchant.Name,
                    Text = prompt,
                    Input = input

                };
                Enqueue(packet.Packet());
            }
            else
            {
                ShowSellConfirm(merchant, slot);
            }

            //var x2F = new ServerPacket(0x2F);
            //x2F.WriteByte(0x03); // type!
            //x2F.WriteByte(0x01); // obj type
            //x2F.WriteUInt32(merchant.Id);
            //x2F.WriteByte(0x01); // ??
            //x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            //x2F.WriteByte(0x00); // color
            //x2F.WriteByte(0x01); // ??
            //x2F.WriteUInt16((ushort)(0x4000 + merchant.Sprite));
            //x2F.WriteByte(0x00); // color
            //x2F.WriteByte(0x00); // ??
            //x2F.WriteString8(merchant.Name);
            //x2F.WriteString16("How many are you selling?");
            //x2F.WriteByte(1);
            //x2F.WriteByte(slot);
            //x2F.WriteUInt16((ushort)MerchantMenuItem.SellItemQuantity);
            //Enqueue(x2F);
        }
        public void ShowSellConfirm(Merchant merchant, byte slot, int quantity = 1)
        {
            PendingSellableQuantity = quantity;
            var item = Inventory[slot];
            var offer = (uint)(Math.Round(item.Value * 0.10, 0) * quantity);
            PendingMerchantOffer = offer;
            String offerString = null;
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var prompt = string.Empty;

            if (item.Durability != item.MaximumDurability)
            {
                offerString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "sell_failure");
                prompt = offerString.Value;
            }

            if (prompt == string.Empty)
            {
                if (!Inventory.Contains(item.Name))
                {
                    offerString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "sell_failure");
                    prompt = offerString.Value;
                }
            }

            if (prompt == string.Empty)
            {
                if (!Inventory.Contains(item.Name, quantity))
                {
                    offerString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "sell_failure_quantity");
                    prompt = offerString.Value;
                }
            }

            if (prompt == string.Empty) //this is so bad
            {
                var quant = quantity > 1 ? "those" : "that";
                //now we can learning!
                offerString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "sell_offer");
                prompt = offerString.Value.Replace("$GOLD", offer.ToString()).Replace("$QUANTITY", quant);

                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.SellItemAccept,
                    Text = "Yes"
                });
                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.MainMenu,
                    Text = "No"
                });

            }

            options.OptionsCount = (byte)options.Options.Count;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());

        }

        public void SellItemAccept(Merchant merchant)
        {
            if (Inventory[PendingSellableSlot].Count > PendingSellableQuantity)
            {
                DecreaseItem(PendingSellableSlot, PendingSellableQuantity);
                AddGold(PendingMerchantOffer);
            }
            else
            {
                RemoveItem(PendingSellableSlot);
                AddGold(PendingMerchantOffer);
            }
            PendingSellableSlot = 0;
            PendingMerchantOffer = 0;

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            options.OptionsCount = 0;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = "Come back if you have more wares to sell.",
                Options = options
            };

            Enqueue(packet.Packet());
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

        public void ShowMerchantSendParcel(Merchant merchant)
        {
            var parcelString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "send_parcel");
            var prompt = string.Empty;
            if (parcelString != null) prompt = parcelString.Value ?? string.Empty;

            var userItems = new UserInventoryItems { InventorySlots = new List<byte>() };
            var itemsCount = 0;
            for (byte i = 0; i < Inventory.Size; i++)
            {
                if (Inventory[i] == null) continue;
                if (Inventory[i].Exchangeable && Inventory[i].Durability == Inventory[i].MaximumDurability)
                {
                    userItems.InventorySlots.Add(i);
                    itemsCount++;
                }
            }
            userItems.InventorySlotsCount = (byte)itemsCount;
            userItems.Id = (ushort)MerchantMenuItem.SendParcelRecipient;


            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.UserInventoryItems,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                UserInventoryItems = userItems
            };
            Enqueue(packet.Packet());
        }

        public void ShowMerchantSendParcelRecipient(Merchant merchant, ItemObject item)
        {
            var sendString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "send_parcel_recipient");
            var prompt = sendString.Value;

            var input = new MerchantInput();
            input.Id = (ushort)MerchantMenuItem.SendParcelAccept;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Input,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Input = input
            };

            PendingSendableParcel = item;

            Enqueue(packet.Packet());
        }

        public void ShowMerchantSendParcelAccept(Merchant merchant, string recipient)
        {
            var itemObj = PendingSendableParcel;
            PendingParcelRecipient = recipient;
            String parcelString;
            var prompt = string.Empty;
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            //verify user has required items.
            var parcelFee = (uint)Math.Round(itemObj.Value * .10, 0);
            if (!(Gold > parcelFee))
            {
                parcelString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "send_parcel_fail");
                prompt = parcelString.Value.Replace("$FEE", parcelFee.ToString());
            }
            if (prompt == string.Empty)
            {
                RemoveGold(parcelFee);
                RemoveItem(itemObj.Name);
                SendInventory();
                parcelString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "send_parcel_success");
                prompt = parcelString.Value.Replace("$FEE", parcelFee.ToString());

                //TODO: Send parcel to recipient
                PendingSendableParcel = null;
            }
            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());
        }

        public void ShowMerchantReceiveParcelAccept(Merchant merchant)
        {
            var sendString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "receive_parcel");
            var prompt = sendString.Value;

            var input = new MerchantInput();
            input.Id = (ushort)MerchantMenuItem.SendParcelAccept;

            var packet = new ServerPacketStructures.MerchantResponse()
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort)(0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort)(0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Input = input
            };

            //TODO: Get Parcel from pending mail.

            Enqueue(packet.Packet());
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
            var transmit = string.Format("{{=c[{0}] {1}", sender, message);
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
            GlobalConnectionManifest.DeregisterClient(Client);
            Client.Redirect(new Redirect(Client, world, Game.Login, name, Client.EncryptionSeed, Client.EncryptionKey), true);
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
            var redirect = new Redirect(Client, Game.World, Game.Login, "socket", Client.EncryptionSeed, Client.EncryptionKey);
            Client.Redirect(redirect, true);
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
            if (!Condition.InExchange || !requestor.Condition.InExchange) return;
            Enqueue(new ServerPacketStructures.Exchange
            {
                Action = ExchangeActions.Initiate,
                RequestorId = requestor.Id,
                RequestorName = requestor.Name
            }.Packet());
        }

        /// <summary>
        /// Send a quantity prompt request to the client (when dealing with stacked items)
        /// </summary>
        /// <param name="itemSlot">The ItemObject slot containing a stacked ItemObject that will be split (client side)</param>
        public void SendExchangeQuantityPrompt(byte itemSlot)
        {
            if (!Condition.InExchange) return;
            Enqueue(
                new ServerPacketStructures.Exchange
                {
                    Action = ExchangeActions.QuantityPrompt,
                    ItemSlot = itemSlot
                }.Packet());
        }
        /// <summary>
        /// Send an exchange update packet for an ItemObject to an active exchange participant.
        /// </summary>
        /// <param name="toAdd">ItemObject to add to the exchange window</param>
        /// <param name="slot">Byte indicating the exchange window slot to be updated</param>
        /// <param name="source">Boolean indicating which "side" of the transaction will be updated (source / "left side" == true)</param>
        public void SendExchangeUpdate(ItemObject toAdd, byte slot, bool source = true)
        {
            if (!Condition.InExchange) return;
            var update = new ServerPacketStructures.Exchange
            {
                Action = ExchangeActions.ItemUpdate,
                Side = source,
                ItemSlot = slot,
                ItemSprite = toAdd.Sprite,
                ItemColor = toAdd.Color,
                ItemName = toAdd.Stackable && toAdd.Count > 1 ? $"{toAdd.Name} ({toAdd.Count}" : toAdd.Name
            };
            Enqueue(update.Packet());
        }

        /// <summary>
        /// Send an exchange update packet for gold to an active exchange participant.
        /// </summary>
        /// <param name="gold">The amount of gold to be added to the window.</param>
        /// <param name="source">Boolean indicating which "side" of the transaction will be updated (source / "left side" == true)</param>
        public void SendExchangeUpdate(uint gold, bool source = true)
        {
            if (!Condition.InExchange) return;
            Enqueue(new ServerPacketStructures.Exchange
            {
                Action = ExchangeActions.GoldUpdate,
                Side = source,
                Gold = gold
            }.Packet());
        }

        /// <summary>
        /// Send a cancellation notice for an exchange.
        /// </summary>
        /// <param name="source">The "side" responsible for cancellation (source / "left side" == true)</param>
        public void SendExchangeCancellation(bool source = true)
        {
            if (!Condition.InExchange) return;
            Enqueue(new ServerPacketStructures.Exchange
            {
                Action = ExchangeActions.Cancel,
                Side = source
            }.Packet());
        }

        /// <summary>
        /// Send a confirmation notice for an exchange.
        /// </summary>
        /// <param name="source">The "side" responsible for confirmation (source / "left side" == true)</param>

        public void SendExchangeConfirmation(bool source = true)
        {
            if (!Condition.InExchange) return;
            Enqueue(new ServerPacketStructures.Exchange
            {
                Action = ExchangeActions.Confirm,
                Side = source
            }.Packet());
        }

        public void SendInventory()
        {
            for (byte i = 0; i < this.Inventory.Size; i++)
            {
                if (this.Inventory[i] != null)
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
                    SendSkillUpdate(SkillBook[i], i);
                }
            }
        }
        public void SendSpells()
        {
            for (byte i = 0; i < this.SpellBook.Size; i++)
            {
                if (this.SpellBook[i] != null)
                {
                    SendSpellUpdate(SpellBook[i], i);
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

    }
}