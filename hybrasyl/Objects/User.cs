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
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */


using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using Hybrasyl.Utility;

namespace Hybrasyl.Objects
{
    

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
        public bool FirstLogin { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class User : Creature
    {
        private object _serializeLock = new object();

        public static string GetStorageKey(string name)
        {
            return string.Concat(typeof(User).Name, ':', name.ToLower());
        }

        public string StorageKey => string.Concat(GetType().Name, ':', Name.ToLower());

        [JsonProperty]
        public string Uuid { get; set; }

        [JsonProperty]
        public string AccountUuid { get; set; }

        private Client Client;

        [JsonProperty]
        public Xml.Gender Gender { get; set; }
        //private account Account { get; set; }

        [JsonProperty]
        public Xml.Class Class { get; set; }
        [JsonProperty]
        public bool IsMaster { get; set; }
        public UserGroup Group { get; set; }

        public int LevelCircle
        {
            get
            {
                if (Stats.Level < LevelCircles.CIRCLE_1)
                    return 0;
                else if (Stats.Level < LevelCircles.CIRCLE_2)
                    return 1;
                else if (Stats.Level < LevelCircles.CIRCLE_3)
                    return 2;
                else if (Stats.Level < LevelCircles.CIRCLE_4)
                    return 3;
                return 4;
            }
        }

        public Mailbox Mailbox => World.GetMailbox(Name);
        public Vault Vault => World.GetVault(AccountUuid == null ? Uuid : AccountUuid);
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

        #region User 
        // Some structs helping us to define various metadata 
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

        public Xml.Castable PendingLearnableCastable { get; private set; }
        public ItemObject PendingSendableParcel { get; private set; }
        public string PendingParcelRecipient { get; private set; }
        public string PendingBuyableItem { get; private set; }
        public int PendingBuyableQuantity { get; private set; }
        public byte PendingSellableSlot { get; private set; }
        public int PendingSellableQuantity { get; private set; }
        public uint PendingMerchantOffer { get; private set; }
        public byte PendingDepositSlot { get; private set; }
        public string PendingWithdrawItem { get; private set; }
        public byte PendingRepairSlot { get; private set; }
        public uint PendingRepairCost { get; private set; }

        [JsonProperty]
        public string GuildUuid { get; set; }

        public List<string> UseCastRestrictions => _currentStatuses.Select(e => e.Value.UseCastRestrictions).Where(e => e != string.Empty).ToList();
        public List<string> ReceiveCastRestrictions => _currentStatuses.Select(e => e.Value.ReceiveCastRestrictions).Where(e => e != string.Empty).ToList();

        private Xml.Nation _nation;

        public Xml.Nation Nation
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
        [JsonProperty] public string Title;


        public DialogState DialogState { get; set; }

        // Used by reactors and certain other objects to set an associate, so that functions called
        // from Lua later know who to "consult" for dialogs / etc.
        public VisibleObject LastAssociate { get; set; }

        [JsonProperty]
        private Dictionary<string, string> UserCookies { get; set; }
        // These are SESSION ONLY, they persist until logout / disconnect
        private Dictionary<string, string> UserSessionCookies { get; set; }

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
                var levelExp = (uint)Math.Pow(Stats.Level, 3) * 250;
                if (Stats.Level == Constants.MAX_LEVEL || Stats.Experience >= levelExp)
                    return 0;

                return (uint)(Math.Pow(Stats.Level, 3) * 250 - Stats.Experience);
            }
        }

        [JsonProperty]
        public uint LevelPoints = 0;

        public byte CurrentMusicTrack { get; set; }

        public void SetCitizenship()
        {
            if (Citizenship != null)
            {
                Xml.Nation theNation;
                Nation = World.WorldData.TryGetValue(Citizenship, out theNation) ? theNation : World.DefaultNation;
            }
        }

        public void ChrysalisMark()
        {
            // TODO: move to config
            if (!Legend.TryGetMark("CHR", out LegendMark mark))
            {
                // Create initial mark of Deoch
                Legend.AddMark(LegendIcon.Community, LegendColor.White, "Chaos Age Aisling", "CHR", true);
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

        private Queue<ServerPacket> LoginQueue { get; set; }

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
        public bool IsAtWorldMap { get { return Location.WorldMap; } set { Location.WorldMap = value; } }

        public void Enqueue(ServerPacket packet)
        {
            GameLog.DebugFormat("Sending {0:X2} to {1}", packet.Opcode, Name);
            Client.Enqueue(packet);
        }

        public override void AoiEntry(VisibleObject obj)
        {
            base.AoiEntry(obj);
            GameLog.DebugFormat("Showing {0} to {1}", Name, obj.Name);
            obj.ShowTo(this);
        }

        public override void AoiDeparture(VisibleObject obj)
        {
            base.AoiDeparture(obj);
            GameLog.Debug("Removing ItemObject with ID {Id}", obj.Id);
            var removePacket = new ServerPacket(0x0E);
            removePacket.WriteUInt32(obj.Id);
            Enqueue(removePacket);
        }

        public void AoiDeparture(VisibleObject obj, int transmitDelay)
        {
            base.AoiDeparture(obj);
            GameLog.Debug("Removing ItemObject with ID {Id}", obj.Id);
            var removePacket = new ServerPacket(0x0E);
            removePacket.TransmitDelay = transmitDelay;
            removePacket.WriteUInt32(obj.Id);
            Enqueue(removePacket);
        }

        /// <summary>
        /// Send a close dialog packet to the client. This will terminate any open dialog.
        /// </summary>
        public void SendCloseDialog()
        {
            var p = new ServerPacket(0x30);
            p.WriteByte(0x0A);
            p.WriteByte(0x00);
            Enqueue(p);
        }

        /// <summary>
        /// Close any active dialogs and clear all dialog state.
        /// </summary>
        public void ClearDialogState()
        {
            DialogState.EndDialog();
            SendCloseDialog();
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
            GameLog.DebugFormat($"{Name} - status update - sending Icon: {statuspacket.Icon}, Color: {statuspacket.BarColor}");
            GameLog.DebugFormat($"{Name} - status: {status.Name}, expired: {status.Expired}, remaining: {remaining}, duration: {status.Duration}");
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
                Stats.Hp = 1;
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
                var item = Inventory[i];
                RemoveItem(i);
                if (item.Perishable && (handler?.Perishable ?? true))
                {
                    // Item is broken
                    World.Remove(item);
                    continue;
                }
                if (item.Durability > 10)
                    item.Durability = Math.Ceiling(item.Durability * 0.90);
                else
                    item.Durability = 0;
                item.DeathPileOwner = Name;
                item.ItemDropTime = timeofdeath;
                item.ItemDropAllowedLooters = looters;
                item.ItemDropType = ItemDropType.UserDeathPile;
                Map.AddItem(X, Y, item);
            }

            // Now process equipment
            for (byte i = 0; i <= Equipment.Size; i++)
            {
                var item = Equipment[i];
                if (item == null)
                    continue;
                RemoveEquipment(i);
                if (item.Perishable && (handler?.Perishable ?? true))
                {
                    // Item is broken
                    World.Remove(item);
                    continue;
                }
                if (item.Durability > 10)
                    item.Durability = Math.Ceiling(item.Durability * 0.90);
                else
                    item.Durability = 0;
                item.DeathPileOwner = Name;
                item.ItemDropTime = timeofdeath;
                item.ItemDropAllowedLooters = looters;
                item.ItemDropType = ItemDropType.UserDeathPile;
                Map.AddItem(X, Y, item);
            }

            // Drop all gold
            if (Gold > 0)
            {
                var newGold = new Gold(Gold)
                {
                    ItemDropAllowedLooters = looters,
                    DeathPileOwner = Name,
                    ItemDropTime = timeofdeath
                };
                World.Insert(newGold);
                Map.AddGold(X, Y, newGold);
                Gold = 0;
            }

            // Experience penalty
            if (handler?.Penalty != null) {
                if (Stats.Experience > 1000)
                {
                    uint expPenalty;
                    if (handler.Penalty.Xp.Contains('.'))
                        expPenalty = (uint)Math.Ceiling(Stats.Experience * Convert.ToDouble(handler.Penalty.Xp));
                    else
                        expPenalty = Convert.ToUInt32(handler.Penalty.Xp);
                    Stats.Experience -= expPenalty;
                    SendSystemMessage($"You lose {expPenalty} experience!");
                }
                if (Stats.BaseHp >= 51 && Stats.Level == 99)
                {
                    uint hpPenalty;

                    if (handler.Penalty.Xp.Contains('.'))
                        hpPenalty = (uint)Math.Ceiling(Stats.Experience * Convert.ToDouble(handler.Penalty.Hp));
                    else
                        hpPenalty = Convert.ToUInt32(handler.Penalty.Hp);

                    Stats.BaseHp -= hpPenalty;
                    SendSystemMessage($"You lose {hpPenalty} HP!");
                }
            }
            Stats.Hp = 0;
            Stats.Mp = 0;
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
            if (handler?.Coma != null && Game.World.WorldData.TryGetValueByIndex(handler.Coma.Value, out Xml.Status status))
                RemoveStatus(status.Icon);
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

            Stats.Hp = 1;
            Stats.Mp = 1;

            UpdateAttributes(StatUpdateFlags.Full);

            if (handler.LegendMark != null)
            {
                LegendMark deathMark;

                if (Legend.TryGetMark(handler.LegendMark.Prefix, out deathMark) && handler.LegendMark.Increment)
                {
                    deathMark.AddQuantity(1);
                }
                else
                    Legend.AddMark(LegendIcon.Community, LegendColor.Brown, handler.LegendMark.Value, DateTime.Now, handler.LegendMark.Prefix, true,
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
            get { return (ushort)(Stats.BaseStr + Stats.Level / 4 + 48); }
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, Password.Hash);
        }

        public User() : base()
        {
            _initializeUser();
            LastAssociate = null;
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
            Location = new LocationInfo();
            Legend = new Legend();
            LastSaid = string.Empty;
            LastSpoke = 0;
            NumSaidRepeated = 0;
            PortraitData = new byte[0];
            ProfileText = string.Empty;
            DialogState = new DialogState(this);
            UserCookies = new Dictionary<string, string>();
            UserSessionCookies = new Dictionary<string, string>();
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
            if (Stats.Level == Constants.MAX_LEVEL || exp < ExpToLevel)
            {
                if (uint.MaxValue - Stats.Experience >= exp)
                    Stats.Experience += exp;
                else
                {
                    Stats.Experience = uint.MaxValue;
                    SendSystemMessage("You cannot gain any more experience.");
                }
            }
            else
            {
                // Apply one Level at a time

                var levelsGained = 0;
                Random random = new Random();

                while (exp > 0 && Stats.Level < 99)
                {
                    uint expChunk = Math.Min(exp, ExpToLevel);

                    exp -= expChunk;
                    Stats.Experience += expChunk;

                    if (ExpToLevel == 0)
                    {
                        levelsGained++;
                        Stats.Level++;
                        LevelPoints += 2;

                        // For level up we use Biomagus' formulas with a random 85% - 115% tweak
                        // HP: (CON/(Lv+1)*50*randomfactor)+25
                        // MP: (WIS/(Lv+1)*50*randomfactor)+25

                        var randomBonus = (random.NextDouble() * 0.30) + 0.85;
                        int bonusHpGain = (int) Math.Ceiling((double) (Stats.BaseCon / (float)Stats.Level) * 50 * randomBonus);
                        int bonusMpGain = (int) Math.Ceiling((double) (Stats.BaseWis / (float)Stats.Level) * 50 * randomBonus);

                        Stats.BaseHp += bonusHpGain + 25;
                        Stats.BaseMp += bonusMpGain + 25;
                        GameLog.UserActivityInfo("User {name}: level increased to {Level}, random factor {factor}, CON {Con}, WIS {Wis}: HP +{Hp}/+25, MP +{Mp}/+25" , Name, Stats.Level, randomBonus, Stats.BaseCon, Stats.BaseWis, 
                            bonusHpGain, bonusMpGain);

                    }
                }
                // If a user has just become level 99, add the remainder exp to their box
                if (Stats.Level == 99)
                    Stats.Experience += exp;

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

        /// <summary>
        /// Given a specified ItemObject, apply the given bonuses to the player.
        /// </summary>
        /// <param name="toApply">The ItemObject used to calculate bonuses.</param>
        public void ApplyBonuses(ItemObject toApply)
        {
            // Given an ItemObject, set our bonuses appropriately.
            // We might want to do this with reflection eventually?
            GameLog.DebugFormat("Bonuses are: {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}",
                toApply.BonusHp, toApply.BonusHp, toApply.BonusStr, toApply.BonusInt, toApply.BonusWis,
                toApply.BonusCon, toApply.BonusDex, toApply.BonusHit, toApply.BonusDmg, toApply.BonusAc,
                toApply.BonusMr, toApply.BonusRegen);

            Stats.BonusHp += toApply.BonusHp;
            Stats.BonusMp += toApply.BonusMp;
            Stats.BonusStr += toApply.BonusStr;
            Stats.BonusInt += toApply.BonusInt;
            Stats.BonusWis += toApply.BonusWis;
            Stats.BonusCon += toApply.BonusCon;
            Stats.BonusDex += toApply.BonusDex;
            Stats.BonusHit += toApply.BonusHit;
            Stats.BonusDmg += toApply.BonusDmg;
            Stats.BonusAc += toApply.BonusAc;
            Stats.BonusMr += toApply.BonusMr;
            Stats.BonusRegen += toApply.BonusRegen;

            switch (toApply.EquipmentSlot)
            {
                case (byte)ItemSlots.Necklace:
                    Stats.BaseOffensiveElement = toApply.Element;
                    break;
                case (byte)ItemSlots.Waist:
                    Stats.BaseDefensiveElement = toApply.Element;
                    break;
            }

            GameLog.DebugFormat(
                "Player {0}: stats now {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}",
                Stats.BonusHp, Stats.BonusHp, Stats.BonusStr, Stats.BonusInt, Stats.BonusWis,
                Stats.BonusCon, Stats.BonusDex, Stats.BonusHit, Stats.BonusDmg, Stats.BonusAc,
                Stats.BonusMr, Stats.BonusRegen, Stats.OffensiveElement, Stats.DefensiveElement);

        }

        /// <summary>
        /// Given a specified ItemObject, remove the given bonuses from the player.
        /// </summary>
        /// <param name="toRemove"></param>
        public void RemoveBonuses(ItemObject toRemove)
        {
            Stats.BonusHp -= toRemove.BonusHp;
            Stats.BonusMp -= toRemove.BonusMp;
            Stats.BonusStr -= toRemove.BonusStr;
            Stats.BonusInt -= toRemove.BonusInt;
            Stats.BonusWis -= toRemove.BonusWis;
            Stats.BonusCon -= toRemove.BonusCon;
            Stats.BonusDex -= toRemove.BonusDex;
            Stats.BonusHit -= toRemove.BonusHit;
            Stats.BonusDmg -= toRemove.BonusDmg;
            Stats.BonusAc -= toRemove.BonusAc;
            Stats.BonusMr -= toRemove.BonusMr;
            Stats.BonusRegen -= toRemove.BonusRegen;
            switch (toRemove.EquipmentSlot)
            {
                case (byte)ItemSlots.Necklace:
                    Stats.BaseOffensiveElement = Xml.Element.None;
                    break;
                case (byte)ItemSlots.Waist:
                    Stats.BaseDefensiveElement = Xml.Element.None;
                    break;
            }
        }

        public override void OnClick(User invoker)
        {

            var guildInfo = GetGuildInfo();

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
            // test
            profilePacket.WriteString8("");
            profilePacket.WriteByte((byte)(Grouping ? 1 : 0));
            profilePacket.WriteString8(guildInfo.GuildRank);
            profilePacket.WriteString8(Constants.REVERSE_CLASSES[(int)Class].Capitalize());
            profilePacket.WriteString8(guildInfo.GuildName);
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

        private (string GuildName, string GuildRank) GetGuildInfo()
        {
            var guild = World.WorldData.Get<Guild>($"{GuildUuid}");
            if (guild == null) return ("", "");

            return guild.GetUserDetails(GuildUuid);
        }

        private void SetValue(PropertyInfo info, object instance, object value)
        {
            try
            {
                GameLog.DebugFormat("Setting property value {0} to {1}", info.Name, value.ToString());
                info.SetValue(instance, Convert.ChangeType(value, info.PropertyType));
            }
            catch (Exception e)
            {
                GameLog.ErrorFormat("Exception trying to set {0} to {1}", info.Name, value.ToString());
                GameLog.ErrorFormat(e.ToString());
                throw;
            }

        }

        public void Save()
        {
            lock (_serializeLock)
            {
                var cache = World.DatastoreConnection.GetDatabase();
                if (Statuses.Count == 0)
                    Statuses = CurrentStatusInfo;
                cache.Set(GetStorageKey(Name), this);
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
            x22.TransmitDelay = 100;
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
            x04.WriteByte(0);
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

        public bool CanTalkTo(User target, out string msg)
        {
            // First, maake sure a) we can send a message and b) the target is not ignoring whispers.
            if (IsMuted)
            {
                msg = "A strange voice says, \"Not for you.\"";
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
        public void SendWhisper(string charname, string message)
        {
            if (!World.TryGetActiveUser(charname, out User target))
            {
                SendSystemMessage("That Aisling is not in Temuair.");
                return;
            }

            if (CanTalkTo(target, out string err))
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
            GameLog.DebugFormat("Sending add visible ItemObject packet");
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
                castable.UseCount += 1;
                if(castable.UseCount <= castable.Mastery.Uses)
                {
                    SendSkillUpdate(castable, slot);
                }
                castable.LastCast = DateTime.Now;
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
                Client.Enqueue(new ServerPacketStructures.Cooldown()
                {
                    Length = (uint)castable.Cooldown,
                    Pane = 0,
                    Slot = slot
                }.Packet());

                castable.UseCount += 1;
                if (castable.UseCount <= castable.Mastery.Uses)
                {
                    SendSpellUpdate(castable, slot);
                }
                
                SpellBook[slot].LastCast = DateTime.Now;
            }
            else
                SendSystemMessage("Failed.");
        }

        /// <summary>
        /// Process the casting cost for a castable. If all requirements were not met, return false.
        /// </summary>
        /// <param name="castable">The castable that is being cast.</param>
        /// <returns>True or false depending on success.</returns>
        public bool ProcessCastingCost(Xml.Castable castable)
        {
            if (castable.CastCosts.Count == 0) return true;

            var costs = castable.CastCosts.Where(e => e.Class.Contains(Class));

            if (costs.Count() == 0)
                costs = castable.CastCosts.Where(e => e.Class.Count == 0);

            if (costs.Count() == 0)
                return true;

            uint reduceHp = 0;
            uint reduceMp = 0;
            bool hasItemCost = true;
            var castcosts = costs.First();

            // HP cost can be either a percentage (0.25) or a fixed amount (50)
            if (castcosts.Stat?.Hp != null)
                if (castcosts.Stat.Hp.Contains('.'))
                    reduceHp = (uint) Math.Ceiling(Convert.ToDouble(castcosts.Stat.Hp) * Stats.MaximumHp);
                else 
                    reduceHp = Convert.ToUInt32(castcosts.Stat.Hp);
            if (castcosts.Stat?.Mp != null)
                if (castcosts.Stat.Mp.Contains('.'))
                    reduceMp = (uint)Math.Ceiling(Convert.ToDouble(castcosts.Stat.Mp) * Stats.MaximumMp);
                else
                    reduceMp = Convert.ToUInt32(castcosts.Stat.Mp);

            
            if (castcosts.Items != null)
            {
                foreach (var item in castcosts.Items)
                {
                    if (!Inventory.Contains(item.Value, item.Quantity)) hasItemCost = false;
                }
            }

            // Check that all requirements are met first. Note that a spell cannot be cast if its HP cost would result
            // in the caster's HP being reduced to zero.
            if (reduceHp >= Stats.Hp || reduceMp > Stats.Mp || castcosts.Gold > Gold || !hasItemCost) return false;

            if (castcosts.Gold > this.Gold) return false;

            if (reduceHp != 0) Stats.Hp -= reduceHp;
            if (reduceMp != 0) Stats.Mp -= reduceMp;
            if ((int)castcosts.Gold > 0 ) this.RemoveGold(new Gold(castcosts.Gold));
            castcosts.Items?.ForEach(item => RemoveItem(item.Value, item.Quantity));

            UpdateAttributes(StatUpdateFlags.Current);
            return true;
        }

        public void SendVisibleItem(ItemObject itemObject)
        {
            GameLog.DebugFormat("Sending add visible ItemObject packet");
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
            GameLog.DebugFormat("Sending add visible creature packet");
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

        public void SendUpdateToUser(Client client = null)
        {
            var offset = Equipment.Armor?.BodyStyle ?? 0;
            if (!Condition.Alive)
                offset += 0x20;

            GameLog.Debug($"Offset is: {offset.ToString("X")}");
            // Figure out what we're sending as the "helmet"
            var helmet = Equipment.Helmet?.DisplaySprite ?? HairStyle;
            helmet = Equipment.DisplayHelm?.DisplaySprite ?? helmet;

            (client ?? Client).Enqueue(new ServerPacketStructures.DisplayUser()
            {
                X = X,
                Y = Y,
                Direction = Direction,
                Id = Id,
                Gender = Gender,
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
            equipPacket.WriteUInt32(itemObject.DisplayDurability);
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

            GameLog.DebugFormat("Adding {0} qty {1} to slot {2}",
                itemObject.Name, itemObject.Count, slot);
            var x0F = new ServerPacket(0x0F);
            x0F.WriteByte((byte)slot);
            x0F.WriteUInt16((ushort)(itemObject.Sprite + 0x8000));
            x0F.WriteByte(0x00);
            x0F.WriteString8(itemObject.Name);
            x0F.WriteInt32(itemObject.Count);  //amount
            x0F.WriteBoolean(itemObject.Stackable);
            x0F.WriteUInt32(itemObject.MaximumDurability);  //maxdura
            x0F.WriteUInt32(itemObject.DisplayDurability);  //curdura
            x0F.WriteUInt32(0x00);  //?
            Enqueue(x0F);
        }

        public void SendSkillUpdate(Xml.Castable item, int slot)
        {
            if (item == null)
            {
                SendClearSkill(slot);
                return;
            }
            GameLog.DebugFormat("Adding skill {0} to slot {2}",
                item.Name, slot);

            var mastery = "";

            if(item.Mastery.Tiered)
            {
                mastery = $"[{item.MasteryLevel}]";
            }

            var x2C = new ServerPacket(0x2C);
            x2C.WriteByte((byte)slot);
            x2C.WriteUInt16((ushort)(item.Icon));
            x2C.WriteString8(Class == Xml.Class.Peasant ? item.Name : $"{item.Name} (Mastery{mastery}: {Math.Round((decimal)(item.UseCount > item.Mastery.Uses ? 100 : item.UseCount / item.Mastery.Uses), 2)}%)");
            Enqueue(x2C);
        }

        public void SendSpellUpdate(Xml.Castable item, int slot)
        {
            if (item == null)
            {
                SendClearSpell(slot);
                return;
            }
            GameLog.DebugFormat("Adding spell {0} to slot {2}",
                item.Name, slot);

            var mastery = "";

            if (item.Mastery.Tiered)
            {
                mastery = $"[{item.MasteryLevel}]";
            }

            var x17 = new ServerPacket(0x17);
            x17.WriteByte((byte)slot);
            x17.WriteUInt16((ushort)(item.Icon));
            var spellType = item.Intents[0].UseType;
            //var spellType = isClick ? 2 : 5;
            x17.WriteByte((byte)spellType); //spell type? how are we determining this?
            x17.WriteString8(Class == Xml.Class.Peasant ? item.Name : $"{item.Name} (Mastery{mastery}:{ Math.Round((decimal)(item.UseCount > item.Mastery.Uses ? 100 : item.UseCount / item.Mastery.Uses), 2)}%)");
            x17.WriteString8(item.Name); //prompt? what is this?
            x17.WriteByte((byte)item.Lines);
            Enqueue(x17);
        }

        public void SetCookie(string cookieName, string value)
        {
            UserCookies[cookieName] = value;
        }

        public void SetSessionCookie(string cookieName, string value)
        {
            UserSessionCookies[cookieName] = value;
        }

        public IReadOnlyDictionary<string, string> GetCookies()
        {
            return UserCookies;
        }
        public IReadOnlyDictionary<string, string> GetSessionCookies()
        {
            return UserSessionCookies;
        }

        public string GetCookie(string cookieName)
        {
            string value;
            if (UserCookies.TryGetValue(cookieName, out value))
            {
                return value;
            }
            return null;
        }

        public string GetSessionCookie(string cookieName)
        {
            string value;
            if (UserSessionCookies.TryGetValue(cookieName, out value))
            {
                return value;
            }
            return null;
        }

        public bool HasCookie(string cookieName) => UserCookies.Keys.Contains(cookieName);
        public bool HasSessionCookie(string cookieName) => UserSessionCookies.Keys.Contains(cookieName);

        public bool DeleteCookie(string cookieName) => UserCookies.Remove(cookieName);
        public bool DeleteSessionCookie(string cookieName) => UserSessionCookies.Remove(cookieName);


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
                x08.WriteByte(Stats.Level);
                x08.WriteByte(Stats.Ability);
                x08.WriteUInt32(Stats.MaximumHp);
                x08.WriteUInt32(Stats.MaximumMp);
                x08.WriteByte(Stats.Str);
                x08.WriteByte(Stats.Int);
                x08.WriteByte(Stats.Wis);
                x08.WriteByte(Stats.Con);
                x08.WriteByte(Stats.Dex);
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
                x08.WriteUInt32(Stats.Hp);
                x08.WriteUInt32(Stats.Mp);
            }
            if (flags.HasFlag(StatUpdateFlags.Experience))
            {
                x08.WriteUInt32(Stats.Experience);
                x08.WriteUInt32(ExpToLevel);
                x08.WriteUInt32(Stats.AbilityExp);
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
                x08.WriteByte((byte)Stats.BaseOffensiveElement);
                x08.WriteByte((byte)Stats.BaseDefensiveElement);
                x08.WriteSByte(Stats.Mr);
                x08.WriteByte(0);
                x08.WriteSByte(Stats.Ac);
                x08.WriteByte(Stats.Dmg);
                x08.WriteByte(Stats.Hit);
            }
            Enqueue(x08);
        }

        public int GetCastableMaxLevel(Xml.Castable castable) => IsMaster ? 100 : castable.GetMaxLevelByClass(Class);


        public User GetFacingUser()
        {
            List<VisibleObject> contents;

            switch (Direction)
            {
                case Xml.Direction.North:
                    contents = Map.GetTileContents(X, Y - 1);
                    break;
                case Xml.Direction.South:
                    contents = Map.GetTileContents(X, Y + 1);
                    break;
                case Xml.Direction.West:
                    contents = Map.GetTileContents(X - 1, Y);
                    break;
                case Xml.Direction.East:
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
                case Xml.Direction.North:
                    contents = Map.GetTileContents(X, Y - 1);
                    break;
                case Xml.Direction.South:
                    contents = Map.GetTileContents(X, Y + 1);
                    break;
                case Xml.Direction.West:
                    contents = Map.GetTileContents(X - 1, Y);
                    break;
                case Xml.Direction.East:
                    contents = Map.GetTileContents(X + 1, Y);
                    break;
                default:
                    contents = new List<VisibleObject>();
                    break;
            }

            return contents;
        }

        public override bool Walk(Xml.Direction direction)
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

                case Xml.Direction.North:
                    --newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                    departingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                    break;
                case Xml.Direction.South:
                    ++newY;
                    arrivingViewport = new Rectangle(oldX - halfViewport, oldY + halfViewport, Constants.VIEWPORT_SIZE, 1);
                    departingViewport = new Rectangle(oldX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, 1);
                    break;
                case Xml.Direction.West:
                    --newX;
                    arrivingViewport = new Rectangle(newX - halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    departingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    break;
                case Xml.Direction.East:
                    ++newX;
                    arrivingViewport = new Rectangle(oldX + halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    departingViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, 1, Constants.VIEWPORT_SIZE);
                    break;
            }
            var isWarp = Map.Warps.TryGetValue(new Tuple<byte, byte>((byte)newX, (byte)newY), out Warp targetWarp);
            var isReactor = Map.Reactors.TryGetValue(new Tuple<byte, byte>((byte)newX, (byte)newY), out Reactor newReactor);
            var wasReactor = Map.Reactors.TryGetValue(new Tuple<byte, byte>((byte)oldX, (byte)oldY), out Reactor oldReactor);

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
                    GameLog.DebugFormat("Collsion check: found obj {0}", obj.Name);
                    if (obj is Creature)
                    {
                        GameLog.DebugFormat("Walking prohibited: found {0}", obj.Name);
                        Refresh();
                        return false;
                    }
                }
                // Is this user entering a forbidden (by level or otherwise) warp?
                if (isWarp)
                {
                    if (targetWarp.MinimumLevel > Stats.Level)
                    {
                        Client.SendMessage("You're too afraid to even approach it!", 3);
                        Refresh();
                        return false;
                    }
                    else if (targetWarp.MaximumLevel < Stats.Level)
                    {
                        Client.SendMessage("Your honor forbids you from entering.", 3);
                        Refresh();
                        return false;
                    }
                }
                // Is the user trying to move into a reactor tile with blocking (meaning the reactor can't be "walked" on)?
                if (isReactor && newReactor.Blocking)
                {
                    Client.SendMessage("Your path is blocked!", 3);
                    Refresh();
                }
            }

            // Calculate the common viewport between the old and new position

            commonViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, Constants.VIEWPORT_SIZE, Constants.VIEWPORT_SIZE);
            commonViewport.Intersect(new Rectangle(newX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE, Constants.VIEWPORT_SIZE));
            GameLog.DebugFormat("Moving from {0},{1} to {2},{3}", oldX, oldY, newX, newY);
            GameLog.DebugFormat("Arriving viewport is a rectangle starting at {0}, {1}", arrivingViewport.X, arrivingViewport.Y);
            GameLog.DebugFormat("Departing viewport is a rectangle starting at {0}, {1}", departingViewport.X, departingViewport.Y);
            GameLog.DebugFormat("Common viewport is a rectangle starting at {0}, {1} of size {2}, {3}", commonViewport.X,
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
                    GameLog.DebugFormat("Sending walk packet for {0} to {1}", Name, user.Name);
                    var x0C = new ServerPacket(0x0C);
                    x0C.WriteUInt32(Id);
                    x0C.WriteUInt16((byte)oldX);
                    x0C.WriteUInt16((byte)oldY);
                    x0C.WriteByte((byte)direction);
                    x0C.WriteByte(0x00);
                    user.Enqueue(x0C);
                }
                // Reactors receive an OnMove event
                if (obj != this && obj is Reactor)
                {
                    var reactor = obj as Reactor;
                    reactor.OnMove(this);
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

            // Handle stepping onto a reactor, leaving a reactor, or both
            if (isReactor)
                newReactor.OnEntry(this);
            if (wasReactor)
                oldReactor.OnLeave(this);

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

            GameLog.DebugFormat("Attempting to add {0} gold", amount);

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
            GameLog.DebugFormat("Removing {0} gold", amount);

            if (Gold < amount)
            {
                GameLog.ErrorFormat("I don't have {0} gold. I only have {1}", amount, Gold);
                return false;
            }

            Gold -= amount;

            UpdateAttributes(StatUpdateFlags.Experience);
            return true;
        }

        public bool AddSkill(Xml.Castable castable)
        {
            if (SkillBook.IsFull(castable.Book))
            {
                SendSystemMessage("You cannot learn any more skills.");
                return false;
            }
            return AddSkill(castable, SkillBook.FindEmptySlot(castable.Book));
        }

        public bool AddSkill(Xml.Castable item, byte slot)
        {
            // Quantity check - if we already have an ItemObject with the same name, will
            // adding the MaximumStack)

            if (SkillBook.Contains(item.Id))
            {
                SendSystemMessage("You already know this skill.");
                return false;
            }

            GameLog.DebugFormat("Attempting to add skill to skillbook slot {0}", slot);


            if (!SkillBook.Insert(slot, item))
            {
                GameLog.DebugFormat("Slot was invalid or not null");
                return false;
            }

            SendSkillUpdate(item, slot);
            return true;
        }

        public bool AddSpell(Xml.Castable castable)
        {
            if (SpellBook.IsFull(castable.Book))
            {
                SendSystemMessage("You cannot learn any more spells.");
                return false;
            }
            return AddSpell(castable, SpellBook.FindEmptySlot(castable.Book));
        }

        public bool AddSpell(Xml.Castable item, byte slot)
        {
            // Quantity check - if we already have an ItemObject with the same name, will
            // adding the MaximumStack)

            if (SpellBook.Contains(item.Id))
            {
                SendSystemMessage("You already know this spell.");
                return false;
            }

            GameLog.DebugFormat("Attempting to add spell to spellbook slot {0}", slot);


            if (!SpellBook.Insert(slot, item))
            {
                GameLog.DebugFormat("Slot was invalid or not null");
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
                SendItemUpdate(inventoryItem, Inventory.SlotByName(inventoryItem.Name).First());
                World.Remove(itemObject);
                return true;
            }

            GameLog.DebugFormat("Attempting to add ItemObject to inventory slot {0}", slot);


            if (!Inventory.Insert(slot, itemObject))
            {
                GameLog.DebugFormat("Slot was invalid or not null");
                Map.Insert(itemObject, X, Y);
                return false;
            }

            SendItemUpdate(itemObject, slot);
            if (updateWeight) UpdateAttributes(StatUpdateFlags.Primary);
            return true;
        }

        public bool AddItem(string itemName, ushort quantity = 1, bool updateWeight = true)
        {
            var xmlItem = World.WorldData.GetByIndex<Xml.Item>(itemName);

            if(xmlItem.Stackable)
            {
                if(Inventory.Contains(itemName, 1))
                {
                    var slots = Inventory.SlotByName(itemName);

                    foreach(var i in slots)
                    {
                        if (quantity > 0)
                        {
                            var slot = Inventory[i];
                            if(slot.Count < slot.MaximumStack)
                            {
                                var diff = slot.MaximumStack - slot.Count;

                                if(diff >= quantity)
                                {
                                    slot.Count += quantity;
                                    quantity = 0;
                                }
                                else
                                {
                                    slot.Count += diff;
                                    quantity -= (ushort)diff;
                                }
                                SendItemUpdate(slot, i);
                                if(updateWeight) Inventory.RecalculateWeight();
                            }
                        }
                    }
                    if(quantity > 0)
                    {
                        do
                        {
                            var item = World.CreateItem(xmlItem.Id);
                            if(quantity > item.MaximumStack)
                            {
                                item.Count = item.MaximumStack;
                                quantity -= (ushort)item.MaximumStack;
                                AddItem(item, updateWeight);
                            }
                            else
                            {
                                item.Count = quantity;
                                quantity -= quantity;
                                AddItem(item, updateWeight);
                            }

                        } 
                        while (quantity > 0);
                    }
                }
                else
                {
                    do
                    {
                        var item = World.CreateItem(xmlItem.Id);
                        if (quantity > item.MaximumStack)
                        {
                            item.Count = item.MaximumStack;
                            quantity -= (byte)item.MaximumStack;
                            AddItem(item, updateWeight);
                        }
                        else
                        {
                            item.Count = quantity;
                            quantity -= quantity;
                            AddItem(item, updateWeight);
                        }
                    }
                    while (quantity > 0);
                }
                return true;
            }
            else if(Inventory.EmptySlots >= quantity)
            {
                do
                {
                    var item = World.CreateItem(xmlItem.Id);
                    AddItem(item, updateWeight);
                    quantity -= 1;
                }
                while (quantity > 0);
                return true;
            }
            else
            {
                return false;
            }
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

        public bool RemoveItem(string itemName, ushort quantity = 0x01, bool updateWeight = true)
        {
            var slotsToUpdate = new List<byte>();
            var slotsToClear = new List<byte>();
            if (Inventory.Contains(itemName, quantity))
            {
                var remaining = (int)quantity;
                var slots = Inventory.SlotByName(itemName);
                lock (Inventory.ContainerLock)
                {
                    foreach (var i in slots)
                    {
                        if (remaining > 0)
                        {
                            if (Inventory[i].Stackable)
                            {
                                if (Inventory[i].Count <= remaining)
                                {
                                    GameLog.Info($"RemoveItem {itemName}, quantity {quantity}: removing stack from slot {i} with {Inventory[i].Count}");
                                    remaining -= Inventory[i].Count;
                                    Inventory.Remove(i);
                                    slotsToClear.Add(i);
                                }
                                else if (Inventory[i].Count > remaining)
                                {
                                    GameLog.Info($"RemoveItem {itemName}, quantity {quantity}: removing quantity from stack, slot {i} with amount {Inventory[i].Count}");
                                    Inventory[i].Count -= remaining;
                                    remaining = 0;
                                    slotsToUpdate.Add(i);
                                }
                            }
                            else
                            {
                                GameLog.Info($"RemoveItem {itemName}, quantity {quantity}: removing nonstackable item from slot {i} with amount {Inventory[i].Count}");
                                Inventory.Remove(i);
                                remaining--;
                                slotsToClear.Add(i);
                            }
                        }
                        else
                        {
                            GameLog.Info($"RemoveItem {itemName}, quantity {quantity}: done, remaining {remaining}");
                            break;
                        }
                    }
                }

                foreach (var slot in slotsToClear)
                {
                    GameLog.Info("clearing slot {slot}");
                    SendClearItem(slot);
                }
                foreach (var slot in slotsToUpdate)
                    SendItemUpdate(Inventory[slot], slot);

                return true;
            }
            GameLog.Info($"RemoveItem {itemName}, quantity {quantity}: not found");
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
            GameLog.DebugFormat("Adding equipment to slot {0}", slot);

            if (!Equipment.Insert(slot, itemObject))
            {
                GameLog.DebugFormat("Slot wasn't null, aborting");
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

        public override void Damage(double damage, Xml.Element element = Xml.Element.None,
            Xml.DamageType damageType = Xml.DamageType.Direct, Xml.DamageFlags damageFlags = Xml.DamageFlags.None, Creature attacker = null, bool onDeath = true)
        {
            if (Condition.Comatose || !Condition.Alive) return;
            base.Damage(damage, element, damageType, damageFlags, attacker, false); // We handle ondeath for users here
            if (Stats.Hp == 0 && Group != null)
            {
                Stats.Hp = 1;
                var handler = Game.Config.Handlers?.Death?.Coma;
                if (handler?.Value != null && World.WorldData.TryGetValueByIndex(handler.Value, out Xml.Status status))
                {
                    ApplyStatus(new CreatureStatus(status, this, null, attacker));
                }
                else
                {
                    GameLog.Warning("No coma handler or status found - user {Name} died!");
                    OnDeath();
                }
            }
            else if (Stats.Hp == 0)
                OnDeath();
            else
            {
                foreach (var item in Equipment)
                {
                    if (item.EquipmentSlot != ServerItemSlots.Weapon)
                        item.Durability -= 1 / (item.MaximumDurability * (100 - Stats.Ac));
                }
            }
            UpdateAttributes(StatUpdateFlags.Current);
        }

        public override void Heal(double heal, Creature source = null)
        {
            base.Heal(heal, source);
            if (this is User) { UpdateAttributes(StatUpdateFlags.Current); }
        }



        public override bool UseCastable(Xml.Castable castObject, Creature target = null)
        {
            if (!ProcessCastingCost(castObject)) return false;
            if (base.UseCastable(castObject, target))
            {
                // This may need to occur elsewhere, depends on how it looks in game
                if (castObject.TryGetMotion(Class, out Xml.CastableMotion motion))
                    SendMotion(Id, motion.Id, motion.Speed);
                return true;
            }
            return false;
        }

        public void AssailAttack(Xml.Direction direction, Creature target = null)
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
            var motion = firstAssail?.Effects.Animations.OnCast.Player.FirstOrDefault(y => y.Class.Contains(Class));

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
            var guildInfo = GetGuildInfo();

            var profilePacket = new ServerPacket(0x39);
            profilePacket.WriteByte((byte)Nation.Flag); // citizenship
            profilePacket.WriteString8(guildInfo.GuildRank);

            profilePacket.WriteString8(Title);
            profilePacket.WriteString8(GroupText);
            profilePacket.WriteBoolean(Grouping);
            profilePacket.WriteByte(0); // ??
            profilePacket.WriteByte((byte)Class);
            //            profilePacket.WriteByte(1); // ??
            profilePacket.WriteByte(0);
            profilePacket.WriteByte(0); // ??
            profilePacket.WriteString8(IsMaster ? "Master" : Hybrasyl.Constants.REVERSE_CLASSES[(int)Class].Capitalize());
            profilePacket.WriteString8(guildInfo.GuildName);
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
            GameLog.DebugFormat("SendMotion id {0}, motion {1}, speed {2}", id, motion, speed);
            var x1A = new ServerPacket(0x1A);
            x1A.WriteUInt32(id);
            x1A.WriteByte(motion);
            x1A.WriteInt16(speed);
            x1A.WriteByte(0xFF);
            Enqueue(x1A);
        }

        public void SendEffect(uint id, ushort effect, short speed)
        {
            GameLog.DebugFormat("SendEffect: id {0}, effect {1}, speed {2} ", id, effect, speed);
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
            GameLog.DebugFormat("SendEffect: targetId {0}, targetEffect {1}, srcId {2}, srcEffect {3}, speed {4}",
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
            GameLog.DebugFormat("SendEffect: x {0}, y {1}, effect {2}, speed {3}", x, y, effect, speed);
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
            GameLog.DebugFormat("SendSound {0}", sound);
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

            foreach (var skill in merchant.Roles.Train.Where(x => x.Type == "Skill" && (x.Class.Contains(Class) || x.Class.Contains(Xml.Class.Peasant))).OrderBy(y => y.Name))
            {
                if (Game.World.WorldData.TryGetValueByIndex(skill.Name, out Xml.Castable result))
                {
                    if (SkillBook.Contains(result)) continue;
                    merchantSkills.Skills.Add(new MerchantSkill()
                    {
                        IconType = 3,
                        Icon = result.Icon,
                        Color = 1,
                        Name = result.Name
                    });
                }
            }
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
                PortraitType = Convert.ToByte(string.IsNullOrEmpty(Portrait)),
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

        public void ShowLearnSkill(Merchant merchant, Xml.Castable castable)
        {
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_choice");
            var skillDesc = castable.Descriptions.Single(x => x.Class.Contains(Class) || x.Class.Contains(Xml.Class.Peasant));
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
            var classReq = castable.Requirements.Single(x => x.Class.Contains(Class) || Class == Xml.Class.Peasant);
            Xml.LocalizedString learnString = null;
            MerchantOptions options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var prompt = string.Empty;
            if (classReq.Level.Min > Stats.Level)
            {
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_player_level");
                prompt = learnString.Value.Replace("$SKILLNAME", castable.Name).Replace("$LEVEL", classReq.Level.Min.ToString());
            }
            if (classReq.Physical != null)
            {
                if (Stats.Str < classReq.Physical.Str || Stats.Int < classReq.Physical.Int || Stats.Wis < classReq.Physical.Wis || Stats.Con < classReq.Physical.Con || Stats.Dex < classReq.Physical.Dex)
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
                    if (!SkillBook.Contains(Game.World.WorldData.GetByIndex<Xml.Castable>(preReq.Value)))
                    {
                        learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_skill_prereq_level");
                        prompt = learnString.Value.Replace("$SKILLNAME", preReq.Value).Replace("$PREREQ", preReq.Level.ToString());
                        break;
                    }
                    else if (SkillBook.Contains(Game.World.WorldData.GetByIndex<Xml.Castable>(preReq.Value)))
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
                if (classReq.Items != null) reqStr = classReq.Items.Aggregate(reqStr, (current, req) => current + (req.Value + "(" + req.Quantity + "), "));

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
            var classReq = castable.Requirements.Single(x => x.Class.Contains(Class) || Class == Xml.Class.Peasant);
            Xml.LocalizedString learnString;
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
            foreach (var spell in merchant.Roles.Train.Where(x => x.Type == "Spell" && (x.Class.Contains(Class) || x.Class.Contains(Xml.Class.Peasant))).OrderBy(y => y.Name))
            {
                // Verify the spell exists first
                if (Game.World.WorldData.TryGetValueByIndex(spell.Name, out Xml.Castable result))
                {
                    if (SpellBook.Contains(result)) continue;
                    merchantSpells.Spells.Add(new MerchantSpell()
                    {
                        IconType = 2,
                        Icon = result.Icon,
                        Color = 1,
                        Name = result.Name
                    });
                }
            }
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

        public void ShowLearnSpell(Merchant merchant, Xml.Castable castable)
        {
            var learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_choice");
            var skillDesc = castable.Descriptions.Single(x => x.Class.Contains(Class) || x.Class.Contains(Xml.Class.Peasant));
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
            var classReq = castable.Requirements.Single(x => x.Class.Contains(Class) || Class == Xml.Class.Peasant);
            Xml.LocalizedString learnString = null;
            MerchantOptions options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var prompt = string.Empty;
            if (classReq.Level.Min > Stats.Level)
            {
                learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_player_level");
                prompt = learnString.Value.Replace("$SPELLNAME", castable.Name).Replace("$LEVEL", classReq.Level.Min.ToString());
            }
            if (classReq.Physical != null)
            {
                if (Stats.Str < classReq.Physical.Str || Stats.Int < classReq.Physical.Int || Stats.Wis < classReq.Physical.Wis || Stats.Con < classReq.Physical.Con || Stats.Dex < classReq.Physical.Dex)
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
                    if (!SkillBook.Contains(Game.World.WorldData.GetByIndex<Xml.Castable>(preReq.Value)))
                    {
                        learnString = World.Strings.Merchant.FirstOrDefault(s => s.Key == "learn_spell_prereq_level");
                        prompt = learnString.Value.Replace("$SPELLNAME", preReq.Value).Replace("$PREREQ", preReq.Level.ToString());
                        break;
                    }
                    else if (SkillBook.Contains(Game.World.WorldData.GetByIndex<Xml.Castable>(preReq.Value)))
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
                if (classReq.Items != null) reqStr = classReq.Items.Aggregate(reqStr, (current, req) => current + (req.Value + "(" + req.Quantity + "), "));

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
            var classReq = castable.Requirements.Single(x => x.Class.Contains(Class) || Class == Xml.Class.Peasant);
            Xml.LocalizedString learnString;
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
                var worldItem = Game.World.WorldData.GetByIndex<Xml.Item>(item.Name);
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
            var item = Game.World.WorldData.GetByIndex<Xml.Item>(name);
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
            Xml.LocalizedString buyString;
            var prompt = string.Empty;
            var item = Game.World.WorldData.GetByIndex<Xml.Item>(PendingBuyableItem);
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
                    if (itemObj.Stackable) IncreaseItem(Inventory.SlotByName(itemObj.Name).First(), quantity);
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
                        IncreaseItem(Inventory.SlotByName(itemObj.Name).First(), quantity - 1);
                    }
                    else
                    {
                        AddItem(itemObj);
                    }
                }
                RemoveGold(reqGold);
                SendCloseDialog();
            }
            else
            {

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
            Xml.LocalizedString offerString = null;
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
            Xml.LocalizedString parcelString;
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

        public void ShowDepositGoldMenu(Merchant merchant)
        {
            string coins = "coin";
            if (Vault.CurrentGold > 1) coins = "coins";
            var pair = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_gold");
            var prompt = pair.Value.Replace("$COINS", Vault.CurrentGold.ToString()).Replace("$REF", coins);


            var input = new MerchantInput();
            input.Id = (ushort)MerchantMenuItem.DepositGoldQuantity;

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

        public void DepositGoldConfirm(Merchant merchant, uint amount)
        {
            string prompt;
            if(amount > Gold)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_gold_failure_deficit").Value;
                ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.DepositGoldMenu);
            }
            else
            {
                if (amount > Vault.RemainingGold)
                {
                    string coins = "coin";
                    if (amount > 1) coins = "coins";
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_gold_failure_surplus").Value.Replace("$COINS", Vault.RemainingGold.ToString()).Replace("$REF", coins);
                    ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.DepositGoldMenu);
                }
                else
                {
                    Vault.AddGold(amount);
                    Vault.Save();
                    RemoveGold(amount);
                    string coins = "coin";
                    if (amount > 1) coins = "coins";
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_gold_success").Value;
                    prompt = prompt.Replace("$COINS", amount.ToString());
                    prompt = prompt.Replace("$REF", coins);
                    merchant.Say(prompt);
                    SendCloseDialog();
                }
            }    
        }

        public void ShowWithdrawGoldMenu(Merchant merchant)
        {
            string coins = "coin";
            if (Vault.CurrentGold > 1) coins = "coins";
            var pair = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_gold");
            var prompt = pair.Value.Replace("$COINS", Vault.CurrentGold.ToString()).Replace("$REF", coins);


            var input = new MerchantInput();
            input.Id = (ushort)MerchantMenuItem.WithdrawGoldQuantity;


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

        public void WithdrawGoldConfirm(Merchant merchant, uint amount)
        {
            string prompt;
            if (amount > Vault.CurrentGold)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_gold_failure_deficit").Value;
                ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.WithdrawGoldMenu);
            }
            else
            {
                if (amount > (uint.MaxValue - Gold))
                {
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_gold_failure_surplus").Value;
                    ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.WithdrawGoldMenu);
                }
                else
                {
                    Vault.RemoveGold(amount);
                    Vault.Save();
                    AddGold(amount);
                    string coins = "coin";
                    if (amount > 1) coins = "coins";
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_gold_success").Value;
                    prompt = prompt.Replace("$COINS", amount.ToString());
                    prompt = prompt.Replace("$REF", coins);
                    merchant.Say(prompt);
                    SendCloseDialog();
                }
            }
        }

        public void ShowDepositItemMenu(Merchant merchant)
        {
            var pair = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_item");
            var prompt = pair.Value;


            var inventoryItems = new UserInventoryItems();
            inventoryItems.InventorySlots = new List<byte>();
            inventoryItems.Id = (ushort)MerchantMenuItem.DepositItemQuantity;

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

        public void ShowDepositItemQuantity(Merchant merchant, byte slot)
        {
            var item = Inventory[slot];
            PendingDepositSlot = slot;
            if (item.Stackable)
            {
                var prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_item_quantity").Value;
                var input = new MerchantInput();

                input.Id = (ushort)MerchantMenuItem.DepositItem;

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
            else
            {
                DepositItemConfirm(merchant, slot);
            }
        }

        public void DepositItemConfirm(Merchant merchant, byte slot, ushort quantity = 1)
        {
            var failure = false;

            var item = Inventory[slot];

            var fee = (uint)(Math.Round(item.Value * 0.10, 0) * quantity);
            
            var prompt = string.Empty;

            if (item.Durability != item.MaximumDurability)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_item_failure_durability").Value;
                failure = true;
            }


            if (!Inventory.ContainsName(item.Name))
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_item_failure_quantity").Value;
                failure = true;
            }

            if (item.Stackable && item.Count < quantity)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_item_failure_quantity").Value;
                failure = true;
            }

            if (fee > Gold)
            {
                string coins = "coin";
                if (fee > 1) coins = "coins";
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_item_failure_fee").Value.Replace("$COINS", fee.ToString()).Replace("$REF", coins);
                failure = true;
            }
                
            

            if (prompt == string.Empty) //this is so bad
            {
                //we can deposit!
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "deposit_item_success").Value.Replace("$ITEM", item.Name).Replace("$QUANTITY", quantity.ToString()).Replace("$COINS", fee.ToString());
                Vault.AddItem(item.Name, quantity);
                if(Inventory[slot].Stackable && Inventory[slot].Count > quantity)
                {
                    RemoveItem(item.Name, quantity);
                    
                }
                else
                {
                    RemoveItem(slot);
                }
                RemoveGold(fee);
                Vault.Save();
                failure = false;
            }

            if (failure)
            {
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
            else
            {
                merchant.Say(prompt);
                SendCloseDialog();
            }
        }

        public void ShowRepairItemMenu(Merchant merchant)
        {
            var inventoryItems = new UserInventoryItems();
            inventoryItems.InventorySlots = new List<byte>();
            inventoryItems.Id = (ushort)MerchantMenuItem.RepairItem;
            var itemsCount = 0;
            for (byte i = 0; i < Inventory.Size; i++)
            {
                if (Inventory[i] == null) continue;
                if (Inventory[i].Durability != Inventory[i].MaximumDurability)
                {
                    inventoryItems.InventorySlots.Add(i);
                    itemsCount++;
                }
            }

            var prompt = "";

            if (itemsCount > 0)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item").Value;
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
            else
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item_none").Value;
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
        }

        public void ShowRepairItem(Merchant merchant, byte slot)
        {
            var prompt = "";
            var item = Inventory[slot];

            PendingRepairSlot = slot;

            PendingRepairCost = (uint)Math.Ceiling(item.Value - (item.Durability / item.MaximumDurability * item.Value));

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();

            if(PendingRepairCost <=1)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item_nocost").Value;
            }
            else
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item_cost").Value.Replace("$COINS", PendingRepairCost.ToString());
                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.RepairItemAccept,
                    Text = "Yes"
                });
                options.Options.Add(new MerchantDialogOption()
                {
                    Id = (ushort)MerchantMenuItem.MainMenu,
                    Text = "No"
                });
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

        public void ShowRepairItemAccept(Merchant merchant)
        {
            if(Gold < PendingRepairCost)
            {
                var prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item_fail").Value;
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
            else
            {
                RemoveGold(PendingRepairCost);
                Inventory[PendingRepairSlot].Durability = Inventory[PendingRepairSlot].MaximumDurability;
                PendingRepairSlot = 0;
                PendingRepairCost = 0;
                DisplayPursuits(this);
            }
        }

        public void ShowRepairAllItems(Merchant merchant)
        {
            var prompt = "";
            var repairableCount = 0;

            for (byte i = 0; i < Inventory.Size; i++)
            {
                if (Inventory[i] == null) continue;
                if (Inventory[i].Durability != Inventory[i].MaximumDurability)
                {
                    var item = Inventory[i];
                    PendingRepairCost += (uint)Math.Ceiling(item.Value - (item.Durability / item.MaximumDurability * item.Value));
                    repairableCount++;
                }
            }

            for(byte i = 0; i < Equipment.Size; i++)
            {
                if (Equipment[i] == null) continue;
                if (Equipment[i].Durability != Equipment[i].MaximumDurability)
                {
                    var item = Equipment[i];
                    PendingRepairCost += (uint)Math.Ceiling(item.Value - (item.Durability / item.MaximumDurability * item.Value));
                    repairableCount++;
                }
            }

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();

            if (repairableCount > 0)
            {

                if (PendingRepairCost <= 1)
                {
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item_nocost").Value;
                }
                else
                {
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_all_items_cost").Value.Replace("$COINS", PendingRepairCost.ToString());
                    options.Options.Add(new MerchantDialogOption()
                    {
                        Id = (ushort)MerchantMenuItem.RepairAllItemsAccept,
                        Text = "Yes"
                    });
                    options.Options.Add(new MerchantDialogOption()
                    {
                        Id = (ushort)MerchantMenuItem.MainMenu,
                        Text = "No"
                    });
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
            else
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item_none").Value;
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

        public void ShowRepairAllItemsAccept(Merchant merchant)
        {
            var prompt = "";
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            if (Gold < PendingRepairCost)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_item_fail").Value;

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
            else
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "repair_all_items_success").Value;
                RemoveGold(PendingRepairCost);
                PendingRepairCost = 0;
                for (byte i = 0; i < Inventory.Size; i++)
                {
                    if (Inventory[i] == null) continue;
                    if (Inventory[i].Durability != Inventory[i].MaximumDurability)
                    {
                        Inventory[i].Durability = Inventory[i].MaximumDurability;
                    }
                }

                for (byte i = 0; i < Equipment.Size; i++)
                {
                    if (Equipment[i] == null) continue;
                    if (Equipment[i].Durability != Inventory[i].MaximumDurability)
                    {
                        Equipment[i].Durability = Equipment[i].MaximumDurability;
                    }
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
        }

        public void ShowWithdrawItemMenu(Merchant merchant)
        {

            var prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item").Value;

            var merchantItems = new MerchantShopItems();
            merchantItems.Items = new List<MerchantShopItem>();
            var itemsCount = 0;
            foreach (var item in Vault.Items)
            {
                var worldItem = Game.World.WorldData.GetByIndex<Xml.Item>(item.Key);
                merchantItems.Items.Add(new MerchantShopItem()
                {
                    Tile = (ushort)(0x8000 + worldItem.Properties.Appearance.Sprite),
                    Color = (byte)worldItem.Properties.Appearance.Color,
                    Description = worldItem.Properties.Vendor?.Description ?? "",
                    Name = worldItem.Name,
                    Price = item.Value

                }); ;
                itemsCount++;
            }
            merchantItems.Id = (ushort)MerchantMenuItem.WithdrawItemQuantity;


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

        public void ShowWithdrawItemQuantity(Merchant merchant, string item)
        {
            var worldItem = World.WorldData.GetByIndex<Xml.Item>(item);
            if (worldItem.Stackable)
            {
                PendingWithdrawItem = item;
                var prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item_quantity").Value;

                var input = new MerchantInput();
                input.Id = (ushort)MerchantMenuItem.WithdrawItem;

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
            else
            {
                WithdrawItemConfirm(merchant, item);
            }
        }

        public void WithdrawItemConfirm(Merchant merchant, string item, uint quantity = 1)
        {
            var failure = false;
            var worldItem = World.WorldData.GetByIndex<Xml.Item>(item);
            

            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();

            string prompt = string.Empty;

            if(quantity > Vault.Items[item])
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item_failure_quantity_bank").Value.Replace("$QUANTITY", quantity.ToString()).Replace("$ITEM", item);
                failure = true;
            }
            else if (worldItem.Stackable)
            {
                if (Inventory.Contains(item, 1))
                {
                    var maxQuantity = 0;
                    var existingStacks = Inventory.SlotByName(item);
                    foreach(var slot in existingStacks)
                    {
                        maxQuantity += Inventory[slot].MaximumStack - Inventory[slot].Count;
                    }
                    maxQuantity += (Inventory.EmptySlots - 2 ) * worldItem.MaximumStack; //account for slot 0 and gold slot

                    if (quantity > maxQuantity)
                    {
                        prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item_failure_quantity_inventory_diff").Value.Replace("$ITEM", item).Replace("$QUANTITY", maxQuantity.ToString());
                    }
                }
                else
                {
                    if(Inventory.EmptySlots == 0)
                    {
                        prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item_failure_slot").Value;
                    }
                }
            }
            else
            {
                if (Inventory.EmptySlots == 0)
                {
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item_failure_slot").Value;
                }
                else if (CurrentWeight + worldItem.Properties.Physical.Weight > MaximumWeight)
                {
                    prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item_failure_weight").Value;
                }
            }

            if(prompt == string.Empty)
            {
                prompt = World.Strings.Merchant.FirstOrDefault(s => s.Key == "withdraw_item_success").Value.Replace("$ITEM", item).Replace("$QUANTITY", quantity.ToString());
                if (worldItem.Stackable)
                {
                    Vault.RemoveItem(item, (ushort)quantity);
                    AddItem(item, (ushort)quantity);
                }
                else
                {
                    var itemObj = World.CreateItem(worldItem.Id);
                    Vault.RemoveItem(item);
                    AddItem(itemObj);
                }
                Vault.Save();
                merchant.Say(prompt);
                SendCloseDialog();
            }
            else
            {

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

        public void Logoff(bool disconnect = false)
        {
            UpdateLogoffTime();
            Save();
            if (!disconnect)
            {
                var redirect = new Redirect(Client, Game.World, Game.Login, "socket", Client.EncryptionSeed, Client.EncryptionKey);
                Client.Redirect(redirect, true);
            }
            else
                Client.Socket.Disconnect(true);
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
                    x0F.WriteString8(Inventory[i].Name);
                    x0F.WriteInt32(Inventory[i].Count);
                    x0F.WriteBoolean(Inventory[i].Stackable);
                    x0F.WriteUInt32(Inventory[i].MaximumDurability);
                    x0F.WriteUInt32(Inventory[i].DisplayDurability);
                    Enqueue(x0F);
                }
            }
        }

        public void SendEquipment()
        {
            for (byte i=0; i < Equipment.Size; i++)
            {
                if (Equipment[i] != null)
                    SendEquipItem(Equipment[i], i);
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

        public void ReapplyStatuses()
        {
            foreach (var status in Statuses)
                ApplyStatus(new CreatureStatus(status, this));
            UpdateAttributes(StatUpdateFlags.Full);
            Statuses.Clear();
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