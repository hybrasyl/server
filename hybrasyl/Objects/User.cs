/*
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


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Messaging;
using Hybrasyl.Utility;
using Hybrasyl.Xml;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

[JsonObject]
public class KillRecord
{
    public string Name { get; set; }
    public DateTime Timestamp { get; set; }
}

[JsonObject(MemberSerialization.OptIn)]
public class User : Creature
{
    private object _serializeLock = new();

    private Client Client;

    [JsonProperty] public uint LevelPoints;

    public User()
    {
        _initializeUser();
        LastAssociate = null;
    }

    public User(Guid serverGuid, long connectionId, string playername = "")
    {
        ServerGuid = serverGuid;
        Client client;
        if (GlobalConnectionManifest.ConnectedClients.TryGetValue(connectionId, out client)) Client = client;
        _initializeUser(playername);
    }

    public User(Guid serverGuid, Client client, string playername = "")
    {
        ServerGuid = serverGuid;
        Client = client;
        _initializeUser(playername);
    }

    public string StorageKey => string.Concat(GetType().Name, ':', Name.ToLower());

    public GuidReference GuidReference => Game.World.WorldData.GetGuidReference(this);

    [JsonProperty] public Guid AccountGuid { get; set; } = Guid.Empty;
    public bool Connected => Client?.Connected ?? false;
    public long ConnectionId => Client?.ConnectionId ?? PreviousConnectionId;
    public long PreviousConnectionId { get; set; }

    [JsonProperty] public Gender Gender { get; set; }
    //private account Account { get; set; }

    [JsonProperty] public Class Class { get; set; }

    [JsonProperty] public Class PreviousClass { get; set; }

    [JsonProperty] public bool IsMaster { get; set; }

    public String AdHocScript { get; set; } = null;
    public UserGroup Group { get; set; }
    public GroupRecruit GroupRecruit { get; set; }

    public int LevelCircle
    {
        get
        {
            return Stats.Level switch
            {
                < LevelCircles.CIRCLE_1 => 0,
                < LevelCircles.CIRCLE_2 => 1,
                < LevelCircles.CIRCLE_3 => 2,
                < LevelCircles.CIRCLE_4 => 3,
                _ => 4
            };
        }
    }

    public Mailbox Mailbox => Game.World.WorldData.GetOrCreateByGuid<Mailbox>(Guid, Name);
    public SentMail SentMailbox => Game.World.WorldData.GetOrCreateByGuid<SentMail>(Guid, Name);
    public Vault Vault => Game.World.WorldData.GetOrCreateByGuid<Vault>(AccountGuid == Guid.Empty ? Guid : AccountGuid);
    public ParcelStore ParcelStore => Game.World.WorldData.GetOrCreateByGuid<ParcelStore>(Guid, Name);

    public MailFlags MailStatus
    {
        get
        {
            var ret = MailFlags.None;
            if (UnreadMail)
                ret |= MailFlags.Mail;
            if (HasParcels)
                ret |= MailFlags.Parcel;
            return ret;
        }
    }

    public bool UnreadMail => Mailbox.HasUnreadMessages;
    public bool HasParcels => ParcelStore.Items.Count > 0;

    public uint ExpToLevel
    {
        get
        {
            var levelExp = (uint) Math.Pow(Stats.Level, 3) * 250;
            if (Stats.Level == Constants.MAX_LEVEL || Stats.Experience >= levelExp)
                return 0;

            return (uint) (Math.Pow(Stats.Level, 3) * 250 - Stats.Experience);
        }
    }

    public byte CurrentMusicTrack { get; set; }

    public double SinceLastLogin
    {
        get
        {
            var span = AuthInfo.LastLogin - AuthInfo.LastLogoff;
            return span.TotalSeconds < 0 ? 0 : span.TotalSeconds;
        }
    }

    public string SinceLastLoginstring => SinceLastLogin < 86400
        ? $"{Math.Floor(SinceLastLogin / 3600)} hours, {Math.Floor(SinceLastLogin % 3600 / 60)} minutes"
        : $"{Math.Floor(SinceLastLogin / 86400)} days, {Math.Floor(SinceLastLogin % 86400 / 3600)} hours, {Math.Floor(SinceLastLogin % 86400 % 3600 / 60)} minutes";

    // Throttling checks for messaging

    public long LastSpoke { get; set; }
    public string LastSaid { get; set; }
    public int NumSaidRepeated { get; set; }

    // Throttling checks for messaging
    public DateTime LastBoardMessageSent { get; set; }
    public string LastBoardMessageTarget { get; set; }
    public DateTime LastMailboxMessageSent { get; set; }
    public string LastMailboxRecipient { get; set; }
    public Dictionary<string, bool> Flags { get; private set; }

    public bool CollisionsDisabled => Flags.ContainsKey("disablecollisions") ? Flags["disablecollisions"] : false;

    private Queue<ServerPacket> LoginQueue { get; set; }

    public DateTime LastAttack { get; set; }

    public bool Grouped => Group != null;

    [JsonProperty] public Dictionary<byte, bool> ClientSettings { get; set; }


    [JsonProperty] public bool IsMuted { get; set; }

    [JsonProperty] public bool IsIgnoringWhispers { get; set; }

    [JsonProperty]
    public bool IsAtWorldMap
    {
        get => Location.WorldMap;
        set => Location.WorldMap = value;
    }

    public string GroupText =>
        // This also eventually needs to consider marriages
        Grouping ? "Grouped!" : "Adventuring Alone";

    /**
         * Returns the current weight as perceived by the client. The actual inventory or equipment
         * weight may be less than zero, but this method will never return a negative value (negative
         * values will appear as zero as the client expects).
         */

    public ushort VisibleWeight => (ushort) Math.Max(0, CurrentWeight);

    /**
         * Returns the true weight of the user's inventory + equipment, which could be negative.
         * Note that you should use VisibleWeight when communicating with the client since negative
         * weights should be invisible to users.
         */
    public int CurrentWeight => Inventory.Weight + Equipment.Weight;

    public ushort MaximumWeight => (ushort) (Stats.BaseStr + Stats.Level / 4 + 48);

    public string LastSystemMessage { get; private set; } = string.Empty;

    public static string GetStorageKey(string name) => string.Concat(typeof(User).Name, ':', name.ToLower());

    /// <summary>
    ///     Reindexes any temporary data structures that may need to be recreated after a user is deserialized from JSON data.
    /// </summary>
    public void Reindex()
    {
        Legend.RegenerateIndex();
    }

    public void SetCitizenship()
    {
        if (Citizenship != null)
        {
            Nation theNation;
            Nation = World.WorldData.TryGetValue(Citizenship, out theNation) ? theNation : World.DefaultNation;
        }
    }

    public override void Say(string message, string from = "")
    {
        if (Map.AllowSpeaking)
        {
            if (World.WorldData.TryGetSocialEvent(this, out var e) &&
                (e.Speakers.Contains(Name) || e.Type != SocialEventType.Class))
            {
                base.Say(message, from);
                return;
            }

            if (!Condition.IsSayProhibited || AuthInfo.IsExempt)
            {
                base.Say(message, from);
                return;
            }
        }

        SendSystemMessage("You try to speak, but nothing happens.");
    }

    public override void Shout(string message, string from = "")
    {
        if (Map.AllowSpeaking)
        {
            if (World.WorldData.TryGetSocialEvent(this, out var e) &&
                (e.Speakers.Contains(Name) || e.Type != SocialEventType.Class))
            {
                base.Shout(message, from);
                return;
            }

            if (!Condition.IsShoutProhibited || AuthInfo.IsExempt)
            {
                base.Shout(message, from);
                return;
            }

            SendSystemMessage("You try to shout, but nothing happens.");
        }
    }

    public bool ChangeCitizenship(string nationName)
    {
        if (World.WorldData.TryGetValue(nationName, out Nation theNation))
        {
            Nation = theNation;
            return true;
        }

        return false;
    }

    public void ChrysalisMark()
    {
        // TODO: move to config
        if (!Legend.TryGetMark("CHR", out var mark))
            // Create initial mark of Deoch
            Legend.AddMark(LegendIcon.Community, LegendColor.White, "Chaos Age Aisling", "CHR");
    }

    public bool GetClientSetting(string key) => ClientSettings[Game.Config.GetSettingNumber(key)];

    public bool ToggleClientSetting(string key)
    {
        var num = Game.Config.GetSettingNumber(key);
        ClientSettings[num] = !ClientSettings[num];
        return ClientSettings[num];
    }

    public bool ToggleClientSetting(byte number)
    {
        ClientSettings[number] = !ClientSettings[number];
        return ClientSettings[number];
    }

    public void Enqueue(ServerPacket packet)
    {
        GameLog.DebugFormat("Sending 0x{0:X2} to {1}", packet.Opcode, Name);
        try
        {
            Client?.Enqueue(packet);
        }
        catch (ObjectDisposedException)
        {
            GameLog.Warning("User {user}: socket enqueue failed due to disconnect, removing", Name);
            // Forcibly destroy client and remove user from world
            PreviousConnectionId = Client.ConnectionId;
            Client = null;
            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.CleanupUser, CleanupType.ByName,
                Name));
        }
    }

    public override void AoiEntry(VisibleObject obj)
    {
        GameLog.DebugFormat("Showing {0} to {1}", Name, obj.Name);
        if (obj is Creature c)
        {

            if (!Condition.SeeInvisible && c.Condition.IsInvisible && obj != this) return;
            base.AoiEntry(obj);
            obj.ShowTo(this);
        }
        else
        {
            base.AoiEntry(obj);
            obj.ShowTo(this);
        }
    }

    public override void AoiDeparture(VisibleObject obj)
    {
        if (obj is Creature c)
            if (!Condition.SeeInvisible && c.Condition.IsInvisible && obj != this)
                return;
        base.AoiDeparture(obj);
        GameLog.Debug("Removing ItemObject with ID {Id}", obj.Id);
        var removePacket = new ServerPacket(0x0E);
        removePacket.WriteUInt32(obj.Id);
        Enqueue(removePacket);
    }

    public void AoiDeparture(VisibleObject obj, int transmitDelay = 0)
    {
        base.AoiDeparture(obj);
        GameLog.Debug("Removing ItemObject with ID {Id}", obj.Id);
        var removePacket = new ServerPacket(0x0E);
        removePacket.TransmitDelay = transmitDelay;
        removePacket.WriteUInt32(obj.Id);
        Enqueue(removePacket);
    }

    /// <summary>
    ///     Send a close dialog packet to the client. This will terminate any open dialog.
    /// </summary>
    public void SendCloseDialog()
    {
        var p = new ServerPacket(0x30);
        p.WriteByte(0x0A);
        p.WriteByte(0x00);
        Enqueue(p);
    }

    /// <summary>
    ///     Close any active dialogs and clear all dialog state.
    /// </summary>
    public void ClearDialogState()
    {
        DialogState.EndDialog();
        SendCloseDialog();
    }

    /// <summary>
    ///     T
    ///     Send a status bar update to the client based on the state of a given status.
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
        GameLog.DebugFormat(
            $"{Name} - status update - sending Icon: {statuspacket.Icon}, Color: {statuspacket.BarColor}");
        GameLog.DebugFormat(
            $"{Name} - status: {status.Name}, expired: {status.Expired}, remaining: {remaining}, duration: {status.Duration}");
        Enqueue(statuspacket.Packet());
    }

    public override void OnHear(SpokenEvent e)
    {
        if (e.Speaker != this)
            MessagesReceived.Add(e);
        var x0D = new ServerPacket(0x0D);
        x0D.WriteBoolean(e.Shout);
        x0D.WriteUInt32(e.Speaker.Id);
        if (e.Shout)
            x0D.WriteString8(
                !string.IsNullOrEmpty(e.From) ? $"{e.From}! {e.Message}" : $"{e.Speaker.Name}! {e.Message}");
        else
            x0D.WriteString8(
                !string.IsNullOrEmpty(e.From) ? $"{e.From}: {e.Message}" : $"{e.Speaker.Name}: {e.Message}");
        Enqueue(x0D);
    }

    /// <summary>
    ///     Sadly, all things in this world must come to an end.
    /// </summary>
    public override void OnDeath()
    {
        // we cannot die twice
        if (!Condition.Alive) return;
        var handler = Game.Config.Handlers?.Death;
        if (!(handler?.Active ?? true))
        {
            SendSystemMessage("Death disabled by server configuration");
            Stats.Hp = 1;
            UpdateAttributes(StatUpdateFlags.Full);
            return;
        }

        var timeofdeath = DateTime.Now;
        var looters = Group?.Members.Select(selector: user => user.Name).ToList() ?? new List<string>();

        // Remove all statuses
        RemoveAllStatuses();

        // We are now quite dead, not mostly dead
        Condition.Comatose = false;
        Condition.Alive = false;

        // First: break everything that is breakable in the inventory
        for (byte i = 1; i <= Inventory.Size; ++i)
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

            if (!item.Undamageable)
            {
                if (item.Durability > 10)
                    item.Durability = Math.Ceiling(item.Durability * 0.90);
                else
                    item.Durability = 0;
            }

            item.DeathPileOwner = Name;
            item.ItemDropTime = timeofdeath;
            item.ItemDropAllowedLooters = looters;
            item.ItemDropType = ItemDropType.UserDeathPile;
            Map.AddItem(X, Y, item);
        }

        // Now process equipment
        for (byte i = 1; i <= Equipment.Size; i++)
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

            if (!item.Undamageable)
            {
                if (item.Durability > 10)
                    item.Durability = Math.Ceiling(item.Durability * 0.90);
                else
                    item.Durability = 0;
            }

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
            Stats.Gold = 0;
        }

        // Experience penalty
        if (handler?.Penalty != null)
        {
            if (Stats.Experience > 1000)
            {
                uint expPenalty;
                if (handler.Penalty.Xp.Contains('.'))
                    expPenalty = (uint) Math.Ceiling(Stats.Experience * Convert.ToDouble(handler.Penalty.Xp));
                else
                    expPenalty = Convert.ToUInt32(handler.Penalty.Xp);
                Stats.Experience -= expPenalty;
                SendSystemMessage($"You lose {expPenalty} experience!");
            }

            if (Stats.BaseHp >= 51 && Stats.Level == 99)
            {
                uint hpPenalty;

                if (handler.Penalty.Hp.Contains('.'))
                    hpPenalty = (uint) Math.Ceiling(Stats.BaseHp * Convert.ToDouble(handler.Penalty.Hp));
                else
                    hpPenalty = Convert.ToUInt32(handler.Penalty.Hp);
                Stats.BaseHp -= hpPenalty;
                SendSystemMessage($"You lose {hpPenalty} HP!");
            }
        }

        Stats.Hp = 0;
        Stats.Mp = 0;
        UpdateAttributes(StatUpdateFlags.Full);
        Effect(76, 120);

        // Save location for recall / etc
        Location.DeathMap = Map;
        Location.DeathMapX = X;
        Location.DeathMapY = Y;

        SendSystemMessage("Your items are ripped from your body.");

        if (Game.Config.Handlers?.Death?.Map != null)
            Teleport(Game.Config.Handlers.Death.Map.Value,
                Game.Config.Handlers.Death.Map.X,
                Game.Config.Handlers.Death.Map.Y);

        if (Game.Config.Handlers?.Death?.GroupNotify ?? true)
            Group?.SendMessage($"{Name} has died!");
    }


    /// <summary>
    ///     End a user's coma status (skulling).
    /// </summary>
    public void EndComa()
    {
        if (!Condition.Comatose) return;
        Condition.Comatose = false;
        var handler = Game.Config.Handlers?.Death;
        if (handler?.Coma != null && Game.World.WorldData.TryGetValue(handler.Coma.Value, out Status status))
            RemoveStatus(status.Icon);
    }

    /// <summary>
    ///     Resurrect a player, optionally, instantly returning them to their point of death.
    /// </summary>
    /// <param name="recall">If true, resurrect at exact point of death.</param>
    public void Resurrect(bool recall = false)
    {
        var handler = Game.Config.Handlers?.Death;
        Condition.Alive = true;

        // Teleport user to national spawn point, or if recalled, to death location

        if (!recall)
        {
            if (Nation.SpawnPoints.Count != 0)
            {
                var spawnpoint = Nation.RandomSpawnPoint;
                Teleport(spawnpoint.MapName, spawnpoint.X, spawnpoint.Y);
            }
            else
            {
                // Handle any weird cases where a map someone exited on was deleted, etc
                // This "default" of Mileth should be set somewhere else
                Teleport(500, 50, 50);
            }
        }
        else
        {
            Teleport(Location.DeathMapId, Location.DeathMapX, Location.DeathMapY);
        }

        Stats.Hp = 1;
        Stats.Mp = 1;

        UpdateAttributes(StatUpdateFlags.Full);

        if (handler.LegendMark != null)
        {
            LegendMark deathMark;

            if (Legend.TryGetMark(handler.LegendMark.Prefix, out deathMark) && handler.LegendMark.Increment)
                deathMark.AddQuantity(1);
            else
                Legend.AddMark(LegendIcon.Community, LegendColor.Brown, handler.LegendMark.Value, DateTime.Now,
                    handler.LegendMark.Prefix, true,
                    1);
        }
    }

    private void _initializeUser(string playername = "")
    {
        Inventory = new Inventory(59);
        Equipment = new Equipment(18);
        SkillBook = new SkillBook();
        SpellBook = new SpellBook();
        IsAtWorldMap = false;
        Location = new LocationInfo();
        Legend = new Legend();
        LastSaid = string.Empty;
        LastSpoke = 0;
        NumSaidRepeated = 0;
        PortraitData = new byte[0];
        ProfileText = string.Empty;
        DialogState = new DialogState(this);
        ClientSettings = new Dictionary<byte, bool>();
        Group = null;
        Flags = new Dictionary<string, bool>();
        _currentStatuses = new ConcurrentDictionary<ushort, ICreatureStatus>();
        RecentKills = new List<KillRecord>();
        MessagesReceived = new List<SpokenEvent>();

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

    public void TrackKill(string name, DateTime timestamp)
    {
        // FIXME: better implementation; stack cannot be used without deserialization workarounds
        if (RecentKills.Count > 25)
            RecentKills = RecentKills.Skip(1).ToList();
        RecentKills.Add(new KillRecord { Name = name, Timestamp = timestamp });
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

        if (!Grouped) Group = new UserGroup(this);

        return Group.Add(invitee);
    }

    /**
         * Distributes experience to a group if the user is in one, or to the
         * user directly if the user is ungrouped.
         */
    public void ShareExperience(uint exp, byte mobLevel)
    {
        if (Group != null)
        {
            Group.ShareExperience(this, exp, mobLevel);
        }
        else
        {
            var difference = Stats.Level - mobLevel;
            switch (difference)
            {
                case > 5:
                    exp = 1;
                    break;
                case 5:
                    exp = (uint) Math.Ceiling(exp * 0.40);
                    break;
                case 4:
                    exp = (uint) Math.Ceiling(exp * 0.80);
                    break;
                case -6:
                    exp = (uint) Math.Ceiling(exp * 1.15);
                    break;
                case -5:
                    exp = (uint) Math.Ceiling(exp * 1.10);
                    break;
                case -4:
                    exp = (uint) Math.Ceiling(exp * 1.05);
                    break;
                case < -7:
                    exp = (uint) Math.Ceiling(exp * 1.20);
                    break;
            }

            GiveExperience(exp, true);
        }
    }


    /// <summary>
    ///     Calculate the amount of gold to be given to a user, taking bonuses into account
    /// </summary>
    /// <param name="exp">The amount of gold to be given.</param>
    public uint CalculateGold(uint gold)
    {
        switch (Stats.ExtraGold)
        {
            case < 0:
                gold -= (uint) (gold * (Stats.ExtraXp / 100) * -1);
                break;
            case > 0:
                gold += (uint) (gold * (Stats.ExtraXp / 100));
                break;
        }

        return gold;
    }

    /// <summary>
    ///     Give a user experience, potentially applying any local bonuses.
    /// </summary>
    /// <param name="exp">The amount of experience to be given.</param>
    /// <param name="ApplyBonus">Whether or not to apply XP bonuses from items / etc (ExtraXp stat)</param>
    public void GiveExperience(uint exp, bool applyBonus = false)
    {

        var bonus = 0;

        if (applyBonus)
            bonus = Convert.ToInt32(exp * Stats.ExtraXp / 100);

        if (bonus + exp < 0)
            Client?.SendMessage("You cannot currently gain experience.", MessageTypes.SYSTEM);

        exp = Convert.ToUInt32(bonus + exp);

        if (Stats.Level == Constants.MAX_LEVEL || exp < ExpToLevel)
        {
            if (uint.MaxValue - Stats.Experience >= exp)
            {
                Stats.Experience += exp;
                Client?.SendMessage($"{exp} experience!", MessageTypes.SYSTEM);
                if (bonus < 0)
                    Client?.SendMessage($"{bonus} penalty experience...", MessageTypes.SYSTEM);
                if (bonus > 0)
                    Client?.SendMessage($"{bonus} bonus experience!", MessageTypes.SYSTEM);


            }
            else
            {
                Stats.Experience = uint.MaxValue;
                SendSystemMessage("You cannot gain any more experience.");
            }
        }
        else
        {
            // Apply one level at a time

            var levelsGained = 0;

            while (exp > 0 && Stats.Level < 99)
            {
                var expChunk = Math.Min(exp, ExpToLevel);

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
                    var randomBonus = Random.Shared.NextDouble() * 0.30 + 0.85;
                    var bonusHpGain =
                        (int) Math.Ceiling((double) (Stats.BaseCon / (float) Stats.Level) * 50 * randomBonus);
                    var bonusMpGain =
                        (int) Math.Ceiling((double) (Stats.BaseWis / (float) Stats.Level) * 50 * randomBonus);

                    Stats.BaseHp += bonusHpGain + 25;
                    Stats.BaseMp += bonusMpGain + 25;
                    GameLog.UserActivityInfo(
                        "User {name}: level increased to {Level}, random factor {factor}, CON {Con}, WIS {Wis}: HP +{Hp}/+25, MP +{Mp}/+25",
                        Name, Stats.Level, randomBonus, Stats.BaseCon, Stats.BaseWis,
                        bonusHpGain, bonusMpGain);
                }
            }

            // If a user has just become level 99, add the remainder exp to their box
            if (Stats.Level == 99)
                Stats.Experience += exp;

            if (levelsGained > 0)
            {
                Client?.SendMessage("A rush of insight fills you!", MessageTypes.SYSTEM);
                Client?.SendMessage("A rush of insight fills you!", MessageTypes.SYSTEM);
                Effect(50, 100);
                UpdateAttributes(StatUpdateFlags.Full);
            }
        }

        UpdateAttributes(StatUpdateFlags.Experience);
    }

    public void TakeExperience(uint exp) { }

    public bool AssociateConnection(Guid serverGuid, long connectionId)
    {
        ServerGuid = serverGuid;
        Client client;
        if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(connectionId, out client)) return false;
        Client = client;
        return true;
    }

    /// <summary>
    ///     Given a specified ItemObject, apply the given bonuses to the player.
    /// </summary>
    /// <param name="toApply">The ItemObject used to calculate bonuses.</param>
    public void ApplyBonuses(ItemObject toApply)
    {
        // Evaluate formulas if needed
        toApply.EvalFormula(this);
        Stats.Apply(toApply.Stats);

        switch (toApply.EquipmentSlot)
        {
            case (byte) ItemSlots.Necklace:
                Stats.BaseOffensiveElement = toApply.Element;
                break;
            case (byte) ItemSlots.Waist:
                Stats.BaseDefensiveElement = toApply.Element;
                break;
        }
    }

    /// <summary>
    ///     Given a specified ItemObject, remove the given bonuses from the player.
    /// </summary>
    /// <param name="toRemove"></param>
    public void RemoveBonuses(ItemObject toRemove)
    {
        Stats.Remove(toRemove.Stats);
        switch (toRemove.EquipmentSlot)
        {
            case (byte) ItemSlots.Necklace:
                Stats.BaseOffensiveElement = ElementType.None;
                break;
            case (byte) ItemSlots.Waist:
                Stats.BaseDefensiveElement = ElementType.None;
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

        profilePacket.WriteByte((byte) GroupStatus);
        profilePacket.WriteString8(Name);
        profilePacket.WriteByte(Nation.Flag); // This should pull from town / nation
        // test
        profilePacket.WriteString8("");
        profilePacket.WriteByte((byte) (Grouping ? 1 : 0));
        profilePacket.WriteString8(guildInfo.GuildRank);
        profilePacket.WriteString8(Constants.REVERSE_CLASSES[(int) Class].Capitalize());
        profilePacket.WriteString8(guildInfo.GuildName);
        profilePacket.WriteByte((byte) Legend.Count);
        foreach (var mark in Legend.Where(predicate: mark => mark.Public))
        {
            profilePacket.WriteByte((byte) mark.Icon);
            profilePacket.WriteByte((byte) mark.Color);
            profilePacket.WriteString8(mark.Prefix);
            profilePacket.WriteString8(mark.ToString());
        }

        profilePacket.WriteUInt16((ushort) (PortraitData.Length + ProfileText.Length + 4));
        profilePacket.WriteUInt16((ushort) PortraitData.Length);
        profilePacket.Write(PortraitData);
        profilePacket.WriteString16(ProfileText);

        invoker.Enqueue(profilePacket);
    }

    private (string GuildName, string GuildRank) GetGuildInfo()
    {
        var guild = World.WorldData.Get<Guild>(GuildGuid);
        if (guild == null) return ("", "");

        return guild.GetUserDetails(GuildGuid);
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
            Game.ReportException(e);
            GameLog.ErrorFormat("Exception trying to set {0} to {1}", info.Name, value.ToString());
            GameLog.ErrorFormat(e.ToString());
            throw;
        }
    }

    public void Save(bool serializeStatus = false)
    {
        lock (_serializeLock)
        {
            var cache = World.DatastoreConnection.GetDatabase();
            if (serializeStatus)
            {
                if (ActiveStatusCount > 0)
                    Statuses = CurrentStatusInfo.ToList();
                else
                    Statuses.Clear();
            }

            AuthInfo.Save();
            Mailbox.Save();
            SentMailbox.Save();
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
        x15.WriteByte((byte) (Map.Checksum % 256));
        x15.WriteByte((byte) (Map.Checksum / 256));
        x15.WriteString8(Map.Name);
        Enqueue(x15);

        if (Map.Music != 0xFF) SendMusic(Map.Music);
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

        var doors = GetDoorsCoordsInView(GetViewport());

        if (doors.Count <= 0) return;
        foreach (var door in doors)
            SendDoorUpdate(door.Item1, door.Item2, Map.Doors[door].Closed, Map.Doors[door].IsLeftRight);
    }

    public List<(byte X, byte Y)> GetDoorsCoordsInView(Rectangle viewPort)
    {
        var ret = new List<(byte X, byte Y)>();

        for (var x = viewPort.X; x < viewPort.X + viewPort.Width; x++)
        for (var y = viewPort.Y; y < viewPort.Y + viewPort.Height; y++)
        {
            var coords = ((byte) x, (byte) y);
            ;
            if (Map.Doors.ContainsKey(coords)) ret.Add(coords);
        }

        return ret;
    }

    public void SendRefresh()
    {
        var x22 = new ServerPacket(0x22);
        x22.WriteByte(0x00);
        x22.TransmitDelay = 100;
        Enqueue(x22);
    }

    public void DisplayIncomingWhisper(string charname, string message)
    {
        Client.SendMessage($"{charname}\" {message}", 0x0);
    }

    public void DisplayOutgoingWhisper(string charname, string message)
    {
        Client.SendMessage($"{charname}> {message}", 0x0);
    }

    public bool CanTalkTo(User target, out string msg)
    {
        msg = string.Empty;
        // First, maake sure a) we can send a message and b) the target is not ignoring whispers.
        if (IsMuted)
            msg = "A strange voice says, \"Not for you.\"";

        if (Condition.IsWhisperProhibited)
            msg = "You concentrate, but nothing happens.";

        if (target.IsIgnoringWhispers)
            msg = "Sadly, that Aisling cannot hear whispers.";

        return string.IsNullOrEmpty(msg);
    }

    public void SendWhisper(string charname, string message)
    {
        if (!World.TryGetActiveUser(charname, out var target))
        {
            SendSystemMessage("That Aisling is not in Temuair.");
            return;
        }

        if (target.Condition.Flags.HasFlag(PlayerFlags.InBoard))
            SendSystemMessage($"{target.Name} is reading a board.");

        if (CanTalkTo(target, out var err))
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
            var err = string.Empty;
            foreach (var member in Group.Members)
                if (CanTalkTo(member, out err))
                    member.Client.SendMessage($"[!{Name}] {message}", MessageTypes.GROUP);
                else
                    Client.SendMessage(err, 0x0);
        }
    }

    public override void ShowTo(IVisible obj)
    {
        switch (obj)
        {
            case User user:
                SendUpdateToUser(user.Client);
                break;
            case ItemObject itemObject:
            {
                SendVisibleItem(itemObject);
                break;
            }
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
        x07.WriteUInt16((ushort) (gold.Sprite + 0x8000));
        x07.WriteInt32(0);
        x07.DumpPacket();
        Enqueue(x07);
    }

    internal void UseSkill(byte slot)
    {
        if (!Map.AllowCasting)
            if (!AuthInfo.IsPrivileged)
            {
                SendSystemMessage("You can't use that here.");
                return;
            }

        var bookSlot = SkillBook[slot];
        if (bookSlot.OnCooldown)
        {
            SendSystemMessage("You must wait longer to use that.");
            return;
        }

        if (!Condition.CastingAllowed)
        {
            SendSystemMessage("You cannot do this now.");
            return;
        }

        if (bookSlot.Castable.TryGetMotion(Class, out var motion))
        {
            Motion(motion.Id, motion.Speed);
            if (bookSlot.Castable?.Effects?.Sound != null)
                PlaySound(bookSlot.Castable.Effects.Sound.Id);
        }

        if (UseCastable(bookSlot.Castable))
        {
            if (bookSlot.UseCount != uint.MaxValue)
                bookSlot.UseCount += 1;
            if (bookSlot.UseCount <= bookSlot.Castable.Mastery.Uses)
                SendSkillUpdate(bookSlot, slot);

            bookSlot.Castable.LastCast = DateTime.Now;
            Client.Enqueue(new ServerPacketStructures.Cooldown
            {
                Length = (uint) bookSlot.Castable.Cooldown,
                Pane = 1,
                Slot = slot
            }.Packet());
        }
    }

    internal void UseSpell(byte slot, uint target = 0)
    {
        if (!Map.AllowCasting)
            if (!AuthInfo.IsPrivileged)
            {
                SendSystemMessage("You can't cast that here.");
                return;
            }

        if (Condition.Muted)
        {
            SendSystemMessage("You try to speak, but cannot.");
            return;
        }

        var bookSlot = SpellBook[slot];
        var targetCreature = Map.EntityTree.OfType<Creature>().SingleOrDefault(predicate: x => x.Id == target) ?? null;

        if (bookSlot.OnCooldown)
        {
            SendSystemMessage("You must wait longer to use that.");
            return;
        }

        if (bookSlot.Castable.Intents[0].UseType == SpellUseType.Target)
        {
            if (targetCreature == null || targetCreature.Map != Map)
                return;

            if (Distance(targetCreature) > Constants.HALF_VIEWPORT_SIZE)
            {
                SendSystemMessage("Your target is too far away.");
                return;
            }

            if (!targetCreature.Condition.Alive)
            {
                SendSystemMessage("Your target is dead.");
                return;
            }
        }

        var intersect = UseCastRestrictions.Intersect(bookSlot.Castable.Categories.Select(selector: x => x.Value),
            StringComparer.InvariantCultureIgnoreCase);

        if (intersect.Any() || !Condition.CastingAllowed)
        {
            SendSystemMessage("You cannot cast that now.");
            return;
        }

        if (bookSlot.Castable.TryGetMotion(Class, out var motion))
            Motion(motion.Id, motion.Speed);

        if (!UseCastable(bookSlot.Castable, targetCreature)) return;
        bookSlot.UseCount += 1;
        if (bookSlot.UseCount <= bookSlot.Castable.Mastery.Uses)
            SendSpellUpdate(bookSlot, slot);
        if (bookSlot.Castable.Cooldown > 0)
            Client.Enqueue(new ServerPacketStructures.Cooldown
            {
                Length = (uint) bookSlot.Castable.Cooldown,
                Pane = 0,
                Slot = slot
            }.Packet());
        bookSlot.LastCast = DateTime.Now;
    }

    /// <summary>
    ///     Process the casting cost for a castable. If all requirements were not met, return false.
    /// </summary>
    /// <param name="castable">The castable that is being cast.</param>
    /// <returns>True or false depending on success.</returns>
    public bool ProcessCastingCost(Castable castable, Creature target, out string message)
    {
        var cost = NumberCruncher.CalculateCastCost(castable, target, this);
        var hasItemCost = true;
        message = string.Empty;

        if (cost.IsNoCost) return true;

        if (cost.Items != null)
            foreach (var itemReq in cost.Items)
                if (!Inventory.ContainsName(itemReq.Item, itemReq.Quantity))
                    hasItemCost = false;

        // Check that all requirements are met first. Note that a spell cannot be cast if its HP cost would result
        // in the caster's HP being reduced to zero.

        if (cost.Hp >= Stats.Hp)
            message = "You lack the required vitality.";

        if (cost.Mp > Stats.Mp)
            message = "Your mana is too low.";

        if (!hasItemCost)
            message = "You lack the required items.";

        if (cost.Gold > Gold)
            message = "You lack the required gold.";

        if (message != string.Empty)
            return false;

        if (cost.Hp != 0) Stats.Hp -= cost.Hp;
        if (cost.Mp != 0) Stats.Mp -= cost.Mp;
        if ((int) cost.Gold > 0) RemoveGold(new Gold(cost.Gold));
        cost.Items?.ForEach(action: itemReq => RemoveItem(itemReq.Item, itemReq.Quantity));

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
        x07.WriteUInt16((ushort) (itemObject.Sprite + 0x8000));
        x07.WriteByte(itemObject.Color);
        x07.WriteByte(0);
        x07.WriteByte(0);
        x07.WriteByte(0);
        //x07.WriteInt32(0); // Unknown what this is
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
        x07.WriteUInt16((ushort) (creature.Sprite + 0x4000));
        x07.WriteByte(0); // Unknown what this is
        x07.WriteByte(0);
        x07.WriteByte(0);
        x07.WriteByte(0);
        x07.WriteByte((byte) creature.Direction);
        x07.WriteByte(0);
        if (creature is Merchant)
        {
            x07.WriteByte(0x02);
            x07.WriteString8(creature.Name);
        }
        else
        {
            x07.WriteByte(0);
        }

        //x07.DumpPacket();
        Enqueue(x07);
    }

    public void SetHairstyle(ushort hairStyle)
    {
        HairStyle = hairStyle;
        SendUpdateToUser();

        foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
        {
            obj.AoiEntry(this);
            AoiEntry(obj);
        }
    }

    public void SetHairColor(ItemColor itemColor)
    {
        HairColor = (byte) itemColor;
        SendUpdateToUser();

        foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
        {
            obj.AoiEntry(this);
            AoiEntry(obj);
        }
    }

    public void SendUpdateToUser(Client client = null)
    {
        var offset = Equipment.Armor?.BodyStyle ?? 0;
        if (!Condition.Alive)
            offset += 0x20;
        else if (Condition.IsInvisible)
            offset += 0x40;
        

        GameLog.Debug($"Offset is: {offset.ToString("X")}");
        // Figure out what we're sending as the "helmet"
        var helmet = Equipment.Helmet?.DisplaySprite ?? HairStyle;
        helmet = Equipment.DisplayHelm?.DisplaySprite ?? helmet;
        var helmcolor = Equipment.DisplayHelm?.Color ?? 0;
        var color = helmcolor == 0 ? HairColor : helmcolor;
        // Why is this so difficult?
        var bootSprite = Equipment.Armor?.HideBoots ?? false ? 0 : Equipment.Boots?.DisplaySprite ?? 0;
        (client ?? Client)?.Enqueue(new ServerPacketStructures.DisplayUser
        {
            X = X,
            Y = Y,
            Direction = Direction,
            Id = Id,
            Gender = Gender,
            Helmet = helmet,
            Weapon = Equipment.Weapon?.DisplaySprite ?? 0,
            Armor = Equipment.Armor?.DisplaySprite ?? 0,
            BodySpriteOffset = offset,
            Boots = (byte) bootSprite,
            BootsColor = Equipment.Boots?.Color ?? 0,
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
            Shield = (byte) (Equipment.Shield?.DisplaySprite ?? 0),
            Invisible = Transparent,
            NameStyle = NameStyle,
            Name = Name,
            GroupName = GroupRecruit?.Name ?? string.Empty,
            MonsterSprite = MonsterSprite,
            HairColor = color
        }.Packet());
    }

    public void RequestPortrait()
    {
        var x49 = new ServerPacket(0x49);
        x49.WriteByte(0x00);
        x49.WriteByte(0x00);
        Enqueue(x49);
    }

    public override void SendId()
    {
        var x05 = new ServerPacket(0x05);
        x05.WriteUInt32(Id);
        x05.WriteByte((byte) Direction);
        x05.WriteByte(0x00); // unknown. clanid?
        x05.WriteByte((byte) Class);
        x05.WriteByte(0x00); // unknown
        x05.WriteByte((byte) Gender);
        x05.WriteByte(0x00);
        Enqueue(x05);
    }

    /// <summary>
    ///     Sends an equip ItemObject packet to the client, triggering an update of the detail window ('a').
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
        equipPacket.WriteByte((byte) slot);
        equipPacket.WriteUInt16((ushort) (itemObject.Sprite + 0x8000));
        equipPacket.WriteByte(itemObject.Color);
        equipPacket.WriteStringWithLength(itemObject.Name);
        equipPacket.WriteByte(0x00);
        equipPacket.WriteUInt32(itemObject.MaximumDurability);
        equipPacket.WriteUInt32(itemObject.DisplayDurability);
        equipPacket.DumpPacket();
        Enqueue(equipPacket);
        SendSystemMessage(itemObject.EquipmentSlot == (byte) EquipmentSlot.Weapon
            ? $"Equipped {itemObject.SlotName}: {itemObject.Name}"
            : $"Equipped {itemObject.SlotName}: {itemObject.Name} (AC {Stats.Ac} MR {Stats.Mr} Regen {Stats.Regen})");
    }

    /// <summary>
    ///     Sends a clear ItemObject packet to the connected client for the specified slot.
    ///     Because the slots on the client side start with one, decrement the slot before sending.
    /// </summary>
    /// <param name="slot">The client side slot to clear.</param>
    public void SendClearItem(int slot)
    {
        var x10 = new ServerPacket(0x10);
        x10.WriteByte((byte) slot);
        x10.WriteUInt16(0x0000);
        x10.WriteByte(0x00);
        Enqueue(x10);
    }

    public void SendClearSkill(int slot)
    {
        var x2D = new ServerPacket(0x2D);
        x2D.WriteByte((byte) slot);
        Enqueue(x2D);
    }

    public void SendClearSpell(int slot)
    {
        var x2D = new ServerPacket(0x18);
        x2D.WriteByte((byte) slot);
        Enqueue(x2D);
    }

    /// <summary>
    ///     Send an ItemObject update packet (essentially placing the ItemObject in a given slot, as far as the client is
    ///     concerned.
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
        x0F.WriteByte((byte) slot);
        x0F.WriteUInt16((ushort) (itemObject.Sprite + 0x8000));
        x0F.WriteByte(itemObject.Color);
        x0F.WriteString8(itemObject.Name);
        x0F.WriteInt32(itemObject.Count);  //amount
        x0F.WriteBoolean(itemObject.Stackable);
        x0F.WriteUInt32(itemObject.MaximumDurability);  //maxdura
        x0F.WriteUInt32(itemObject.DisplayDurability);  //curdura
        x0F.WriteUInt32(0x00);  //?
        Enqueue(x0F);
    }

    public void SendSkillUpdate(BookSlot item, int slot)
    {
        if (item == null)
        {
            SendClearSkill(slot);
            return;
        }

        GameLog.DebugFormat("Adding skill {0} to slot {2}",
            item.Castable.Name, slot);

        //if(item.Castable.Mastery.Tiered)
        //{
        //    mastery = $"[{item.MasteryLevel}]";
        //}

        var x2C = new ServerPacket(0x2C);
        x2C.WriteByte((byte) slot);
        x2C.WriteUInt16(item.Castable.Icon);
        if (item.Castable.Mastery.Uses != 1)
        {
            double percent;
            if (item.UseCount > item.Castable.Mastery.Uses) percent = 100;
            else percent = Math.Floor(item.UseCount / (double) item.Castable.Mastery.Uses * 100);

            x2C.WriteString8($"{item.Castable.Name} (Lev:{percent}/100)");
        }
        else
        {
            x2C.WriteString8(item.Castable.Name);
        }

        Enqueue(x2C);
    }

    public void SendSpellUpdate(BookSlot item, int slot)
    {
        if (item == null)
        {
            SendClearSpell(slot);
            return;
        }

        GameLog.DebugFormat("Adding spell {0} to slot {2}",
            item.Castable.Name, slot);


        var name = "";
        if (item.Castable.Mastery.Uses != 1)
        {
            double percent;
            if (item.UseCount > item.Castable.Mastery.Uses) percent = 100;
            else percent = Math.Floor(item.UseCount / (double) item.Castable.Mastery.Uses * 100);

            name = $"{item.Castable.Name} (Lev:{percent}/100)";
        }
        else
        {
            name = item.Castable.Name;
        }

        var spellUpdate = new ServerPacketStructures.AddSpell
        {
            Slot = (byte) slot,
            Icon = item.Castable.Icon,
            UseType = (byte) item.Castable.Intents[0].UseType,
            Name = name,
            Prompt = "\0",
            Lines = (byte) CalculateLines(item.Castable)
        };
        Enqueue(spellUpdate.Packet());
    }

    private int CalculateLines(Castable castable)
    {
        try
        {
            // TODO: potentially add additional equipment types. for now only weapons
            if (Equipment.Weapon?.CastModifiers != null)
            {
                object modifier = null;
                foreach (var castmodifier in Equipment.Weapon.CastModifiers)
                    // Matches most to least specific, first match wins
                    if (!string.IsNullOrEmpty(castmodifier.Castable) &&
                        castmodifier.Castable.ToLower() == castable.Name.ToLower())
                    {
                        modifier = castmodifier.Item;
                        break;
                    }
                    else if (!string.IsNullOrEmpty(castmodifier.Group) &&
                             castable.Categories.Select(selector: x => x.Value.ToLower())
                                 .Contains(castmodifier.Group.ToLower()))
                    {
                        modifier = castmodifier.Item;
                        break;
                    }
                    else if (castmodifier.All)
                    {
                        modifier = castmodifier.Item;
                        break;
                    }

                // Evaluate modifier match.
                // Exact match first, then between min / max, which is same as "all" if no min/max defined (default -1 / 255)
                if (modifier is CastModifierAdd add)
                {
                    if (castable.Lines == add.Match ||
                        (add.Match == -1 && castable.Lines >= add.Min && castable.Lines <= add.Max))
                        return Math.Min(255, castable.Lines + add.Amount);
                }
                else if (modifier is CastModifierSubtract sub)
                {
                    if (castable.Lines == sub.Match ||
                        (sub.Match == -1 && castable.Lines >= sub.Min && castable.Lines <= sub.Max))
                        return Math.Max(0, castable.Lines - sub.Amount);
                }
                else if (modifier is CastModifierReplace repl)
                {
                    if (castable.Lines == repl.Match ||
                        (repl.Match == -1 && castable.Lines >= repl.Min && castable.Lines <= repl.Max))
                        return repl.Amount;
                }
            }

            return castable.Lines;
        }
        catch (Exception e)
        {
            GameLog.Error("Lines calculation error: {e}, returning default of 3", e);
            return 3;
        }
    }

    public override void UpdateAttributes(StatUpdateFlags flags)
    {
        if (Client is null) return;
        var x08 = new ServerPacket(0x08);
        if (UnreadMail || HasParcels) flags |= StatUpdateFlags.UnreadMail;

        if (CollisionsDisabled)
            flags |= StatUpdateFlags.GameMasterA;

        x08.WriteByte((byte) flags);
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
                x08.WriteByte((byte) LevelPoints);
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
            x08.WriteByte(0); // Unknown
            x08.WriteByte((byte) (Condition.Blinded ? 0x08 : 0x00));
            x08.WriteByte(0); // Unknown
            x08.WriteByte(0); // Unknown
            x08.WriteByte(0); // Unknown
            x08.WriteByte((byte) MailStatus);
            x08.WriteByte((byte) Stats.BaseOffensiveElement);
            x08.WriteByte((byte) Stats.BaseDefensiveElement);
            x08.WriteByte(Stats.MrRating);
            x08.WriteByte(0); // "fast move"
            x08.WriteSByte(Stats.Ac);
            x08.WriteByte(Stats.DmgRating);
            x08.WriteByte(Stats.HitRating);
        }

        Enqueue(x08);
    }

    public int GetCastableMaxLevel(Castable castable) => IsMaster ? 100 : castable.GetMaxLevelByClass(Class);


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

        return (User) contents.FirstOrDefault(predicate: y => y is User);
    }

    /// <summary>
    ///     Returns all the objects that are directly facing the user.
    /// </summary>
    /// <returns>A list of visible objects.</returns>
    public List<VisibleObject> GetFacingObjects(int distance = 1)
    {
        var contents = new List<VisibleObject>();

        switch (Direction)
        {
            case Direction.North:
            {
                for (var i = 1; i <= distance; i++) contents.AddRange(Map.GetTileContents(X, Y - i));
            }
                break;
            case Direction.South:
            {
                for (var i = 1; i <= distance; i++) contents.AddRange(Map.GetTileContents(X, Y + i));
            }
                break;
            case Direction.West:
            {
                for (var i = 1; i <= distance; i++) contents.AddRange(Map.GetTileContents(X - i, Y));
            }
                break;
            case Direction.East:
            {
                for (var i = 1; i <= distance; i++) contents.AddRange(Map.GetTileContents(X + i, Y));
            }
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
        var arrivingViewport = Rectangle.Empty;
        var departingViewport = Rectangle.Empty;
        var commonViewport = Rectangle.Empty;
        var halfViewport = Constants.VIEWPORT_SIZE / 2;

        if (Condition.Disoriented)
        {
            direction = (Direction) Random.Shared.Next(4);
            SendSystemMessage("You stumble around, unable to gather your bearings.");
        }

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
                arrivingViewport = new Rectangle(oldX - halfViewport + 2, newY - halfViewport + 4,
                    Constants.VIEWPORT_SIZE, 1);
                departingViewport = new Rectangle(oldX - halfViewport + 2, oldY + halfViewport - 2,
                    Constants.VIEWPORT_SIZE, 1);
                break;
            case Direction.South:
                ++newY;
                arrivingViewport = new Rectangle(oldX - halfViewport - 2, oldY + halfViewport - 4,
                    Constants.VIEWPORT_SIZE, 1);
                departingViewport = new Rectangle(oldX - halfViewport + 2, newY - halfViewport + 2,
                    Constants.VIEWPORT_SIZE, 1);
                break;
            case Direction.West:
                --newX;
                arrivingViewport = new Rectangle(newX - halfViewport + 4, oldY - halfViewport + 2, 1,
                    Constants.VIEWPORT_SIZE);
                departingViewport = new Rectangle(oldX + halfViewport - 2, oldY - halfViewport - 2, 1,
                    Constants.VIEWPORT_SIZE);
                break;
            case Direction.East:
                ++newX;
                arrivingViewport = new Rectangle(oldX + halfViewport - 4, oldY - halfViewport + 2, 1,
                    Constants.VIEWPORT_SIZE);
                departingViewport = new Rectangle(oldX - halfViewport + 2, oldY - halfViewport + 2, 1,
                    Constants.VIEWPORT_SIZE);
                break;
        }

        var isWarp = Map.Warps.TryGetValue(new Tuple<byte, byte>((byte) newX, (byte) newY), out var targetWarp);
        var isReactors = Map.Reactors.TryGetValue(((byte) newX, (byte) newY), out var newReactors);
        var wasReactors = Map.Reactors.TryGetValue(((byte) oldX, (byte) oldY), out var oldReactors);

        // Now that we know where we are going, perform some sanity checks.
        // Is the player trying to walk into a wall, or off the map?

        if (newX > Map.X || newY > Map.Y || newX < 0 || newY < 0)
        {
            Refresh();
            return false;
        }
        // Allow a user to walk into walls, if and only if collisions are disabled (implies privileged user)

        if (Map.IsWall[newX, newY] && !CollisionsDisabled)
        {
            Refresh();
            return false;
        }

        // Is the player trying to walk into an occupied tile?
        foreach (var obj in Map.GetTileContents((byte) newX, (byte) newY))
        {
            GameLog.DebugFormat("Collision check: found obj {0}", obj.Name);
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

            if (targetWarp.MaximumLevel < Stats.Level)
            {
                Client.SendMessage("Your honor forbids you from entering.", 3);
                Refresh();
                return false;
            }
        }

        // Is the user trying to move into a reactor tile with blocking (meaning the reactor can't be "walked" on)?
        if (isReactors && newReactors.Values.Any(predicate: x => x.Blocking))
        {
            Client.SendMessage("Your path is blocked!", 3);
            Refresh();
        }

        // Calculate the common viewport between the old and new position

        commonViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport, Constants.VIEWPORT_SIZE,
            Constants.VIEWPORT_SIZE);
        commonViewport.Intersect(new Rectangle(newX - halfViewport, newY - halfViewport, Constants.VIEWPORT_SIZE,
            Constants.VIEWPORT_SIZE));
        GameLog.DebugFormat("Moving from {0},{1} to {2},{3}", oldX, oldY, newX, newY);
        GameLog.DebugFormat("Arriving viewport is a rectangle starting at {0}, {1}", arrivingViewport.X,
            arrivingViewport.Y);
        GameLog.DebugFormat("Departing viewport is a rectangle starting at {0}, {1}", departingViewport.X,
            departingViewport.Y);
        GameLog.DebugFormat("Common viewport is a rectangle starting at {0}, {1} of size {2}, {3}", commonViewport.X,
            commonViewport.Y, commonViewport.Width, commonViewport.Height);

        X = (byte) newX;
        Y = (byte) newY;
        Direction = direction;

        // Transmit update to the moving client, as we are actually walking now
        var x0B = new ServerPacket(0x0B);
        x0B.WriteByte((byte) direction);
        x0B.WriteUInt16((byte) oldX);
        x0B.WriteUInt16((byte) oldY);
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
                x0C.WriteUInt16((byte) oldX);
                x0C.WriteUInt16((byte) oldY);
                x0C.WriteByte((byte) direction);
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

        if (isWarp) return targetWarp.Use(this);

        // Handle stepping onto a reactor, leaving a reactor, or both
        if (isReactors)
            foreach (var reactor in newReactors.Values)
                reactor.OnEntry(this);

        if (wasReactors)
            foreach (var reactor in oldReactors.Values)
                reactor.OnLeave(this);

        HasMoved = true;
        Map.EntityTree.Move(this);
        return true;
    }

    public bool AddGold(Gold gold) => AddGold(gold.Amount);

    public bool AddGold(uint amount)
    {
        if (Gold + amount > Constants.MAXIMUM_GOLD)
        {
            Client.SendMessage("You cannot carry any more gold.", 3);
            return false;
        }

        GameLog.DebugFormat("Attempting to add {0} gold", amount);

        Stats.Gold += amount;

        UpdateAttributes(StatUpdateFlags.Experience);
        return true;
    }

    public bool RemoveGold(Gold gold) => RemoveGold(gold.Amount);

    public void RecalculateBonuses()
    {
        foreach (var item in Equipment)
            ApplyBonuses(item);
        foreach (var item in Inventory)
            item.EvalFormula(this);
    }

    public bool RemoveGold(uint amount)
    {
        GameLog.DebugFormat("Removing {0} gold", amount);

        if (Gold < amount)
        {
            GameLog.ErrorFormat("I don't have {0} gold. I only have {1}", amount, Gold);
            return false;
        }

        Stats.Gold -= amount;

        UpdateAttributes(StatUpdateFlags.Experience);
        return true;
    }

    public bool AddSkill(Castable castable)
    {
        if (SkillBook.IsFull(castable.Book))
        {
            SendSystemMessage("You cannot learn any more skills.");
            return false;
        }

        return AddSkill(castable, SkillBook.FindEmptySlot(castable.Book));
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

        GameLog.DebugFormat("Attempting to add skill to skillbook slot {0}", slot);


        if (!SkillBook.Insert(slot, item))
        {
            GameLog.DebugFormat("Slot was invalid or not null");
            return false;
        }

        SendSkillUpdate(SkillBook[slot], slot);
        return true;
    }

    public bool AddSpell(Castable castable)
    {
        if (SpellBook.IsFull(castable.Book))
        {
            SendSystemMessage("You cannot learn any more spells.");
            return false;
        }

        return AddSpell(castable, SpellBook.FindEmptySlot(castable.Book));
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

        GameLog.InfoFormat("Attempting to add spell to spellbook slot {0}", slot);


        if (!SpellBook.Insert(slot, item))
        {
            GameLog.ErrorFormat("Slot was invalid or not null");
            return false;
        }

        SendSpellUpdate(SpellBook[slot], slot);
        return true;
    }

    public bool AddItem(ItemObject itemObject, bool updateWeight = true)
    {
        Game.World.Insert(itemObject);
        if (!Inventory.IsFull)
            return AddItem(itemObject, Inventory.FindEmptySlot(), updateWeight);
        SendSystemMessage("You cannot carry any more items.");
        Map.Insert(itemObject, X, Y);
        return false;
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

        var inventoryItem = Inventory.FindById(itemObject.Name);

        if (inventoryItem != null && itemObject.Stackable)
        {
            if (itemObject.Count + inventoryItem.Count > inventoryItem.MaximumStack)
            {
                itemObject.Count = inventoryItem.Count + itemObject.Count - inventoryItem.MaximumStack;
                inventoryItem.Count = inventoryItem.MaximumStack;
                SendSystemMessage(string.Format("You can't carry any more {0}", itemObject.Name));
                Map.Insert(itemObject, X, Y);
                return false;
            }

            // Merge stack and destroy "added" ItemObject
            inventoryItem.Count += itemObject.Count;
            itemObject.Count = 0;
            SendItemUpdate(inventoryItem, Inventory.SlotOfId(inventoryItem.Name));
            Game.World.Remove(itemObject);
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
        itemObject.EvalFormula(this);
        if (updateWeight) UpdateAttributes(StatUpdateFlags.Primary);
        return true;
    }

    public bool AddItem(string itemName, ushort quantity = 1, bool updateWeight = true)
    {
        var xmlItem = World.WorldData.GetByIndex<Item>(itemName);

        if (xmlItem.Stackable)
        {
            if (Inventory.ContainsName(itemName))
            {
                var slots = Inventory.GetSlotsByName(itemName);

                foreach (var i in slots)
                    if (quantity > 0)
                    {
                        var slot = Inventory[i];
                        if (slot.Count < slot.MaximumStack)
                        {
                            var diff = slot.MaximumStack - slot.Count;

                            if (diff >= quantity)
                            {
                                slot.Count += quantity;
                                quantity = 0;
                            }
                            else
                            {
                                slot.Count += diff;
                                quantity -= (ushort) diff;
                            }

                            SendItemUpdate(slot, i);
                            if (updateWeight) Inventory.RecalculateWeight();
                        }
                    }

                if (quantity > 0)
                    do
                    {
                        var item = World.CreateItem(xmlItem.Id);
                        if (quantity > item.MaximumStack)
                        {
                            item.Count = item.MaximumStack;
                            quantity -= (ushort) item.MaximumStack;
                            AddItem(item, updateWeight);
                        }
                        else
                        {
                            item.Count = quantity;
                            quantity -= quantity;
                            AddItem(item, updateWeight);
                        }
                    } while (quantity > 0);
            }
            else
            {
                do
                {
                    var item = World.CreateItem(xmlItem.Id);
                    if (quantity > item.MaximumStack)
                    {
                        item.Count = item.MaximumStack;
                        quantity -= (byte) item.MaximumStack;
                        World.Insert(item);
                        AddItem(item, updateWeight);
                    }
                    else
                    {
                        item.Count = quantity;
                        quantity -= quantity;
                        World.Insert(item);
                        AddItem(item, updateWeight);
                    }
                } while (quantity > 0);
            }

            return true;
        }

        if (Inventory.EmptySlots >= quantity)
        {
            do
            {
                var item = World.CreateItem(xmlItem.Id);
                World.Insert(item);
                AddItem(item, updateWeight);
                quantity -= 1;
            } while (quantity > 0);

            return true;
        }

        return false;
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
        if (Inventory.ContainsName(itemName, quantity))
        {
            var remaining = (int) quantity;
            var slots = Inventory.GetSlotsByName(itemName);
            foreach (var i in slots)
                if (remaining > 0)
                {
                    if (Inventory[i].Stackable)
                    {
                        if (Inventory[i].Count <= remaining)
                        {
                            GameLog.Info(
                                $"RemoveItem {itemName}, quantity {quantity}: removing stack from slot {i} with {Inventory[i].Count}");
                            remaining -= Inventory[i].Count;
                            Inventory.Remove(i);
                            slotsToClear.Add(i);
                        }
                        else if (Inventory[i].Count > remaining)
                        {
                            GameLog.Info(
                                $"RemoveItem {itemName}, quantity {quantity}: removing quantity from stack, slot {i} with amount {Inventory[i].Count}");
                            Inventory[i].Count -= remaining;
                            remaining = 0;
                            slotsToUpdate.Add(i);
                        }
                    }
                    else
                    {
                        GameLog.Info(
                            $"RemoveItem {itemName}, quantity {quantity}: removing nonstackable item from slot {i} with amount {Inventory[i].Count}");
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

        ApplyBonuses(itemObject);
        UpdateAttributes(StatUpdateFlags.Stats);
        SendEquipItem(itemObject, slot);

        if (sendUpdate) Show();
        // TODO: target this recalculation, this is a mildly expensive operation
        if (itemObject.CastModifiers != null)
            SendSpells();
        return true;
    }

    public bool RemoveEquipment(byte slot, bool sendUpdate = true)
    {
        var item = Equipment[slot];
        // Process requirements
        if (item != null)
        {
            var f = Equipment.Where(predicate: x => x.Template.SlotRequirements.Any())
                .SelectMany(selector: itemReq => itemReq.Template.SlotRequirements);
            if (Equipment.Where(predicate: x => x.Template.SlotRequirements.Any())
                .SelectMany(selector: itemReq => itemReq.Template.SlotRequirements)
                .Any(predicate: req => req.Slot == (EquipmentSlot) slot))
            {
                // TODO: improve messaging here
                SendSystemMessage("Other equipment must be removed first.");
                return false;
            }
        }

        if (Equipment.Remove(slot))
        {
            SendRefreshEquipmentSlot(slot);
            SendSystemMessage($"Unequipped {item.Name}");
            RemoveBonuses(item);
            // TODO: target this recalculation, this is a mildly expensive operation
            if (item.CastModifiers != null)
                SendSpells();
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
        refreshPacket.WriteByte((byte) slot);
        Enqueue(refreshPacket);
    }

    public override void Refresh()
    {
        SendMapInfo();
        SendLocation();
        SendUpdateToUser();
        SendRefresh();


        foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
        {
            AoiEntry(obj);
            obj.AoiEntry(this);
        }
    }

    public void SwapItem(byte oldSlot, byte newSlot)
    {
        if (oldSlot == newSlot) return;
        var oldSlotItem = Inventory[oldSlot];
        var newSlotItem = Inventory[newSlot];

        if (newSlotItem != null && oldSlotItem != null && oldSlotItem.Name == newSlotItem.Name && newSlotItem.Stackable)
        {
            if (newSlotItem.Count >= newSlotItem.MaximumStack) return;
            var diff = newSlotItem.MaximumStack - newSlotItem.Count;

            if (diff > oldSlotItem.Count)
            {
                newSlotItem.Count += oldSlotItem.Count;
                RemoveItem(oldSlot);
                SendItemUpdate(newSlotItem, newSlot);
            }
            else
            {
                newSlotItem.Count += diff;
                oldSlotItem.Count -= diff;
                SendItemUpdate(oldSlotItem, oldSlot);
                SendItemUpdate(newSlotItem, newSlot);
            }
        }
        else
        {
            Inventory.Swap(oldSlot, newSlot);
            SendItemUpdate(Inventory[oldSlot], oldSlot);
            SendItemUpdate(Inventory[newSlot], newSlot);
        }
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

    public override void Damage(double damage, ElementType element = ElementType.None,
        DamageType damageType = DamageType.Direct, DamageFlags damageFlags = DamageFlags.None, Creature attacker = null,
        bool onDeath = true)
    {
        if (Condition.Comatose || !Condition.Alive) return;
        base.Damage(damage, element, damageType, damageFlags, attacker, false); // We handle ondeath for users here
        if (Stats.Hp == 0 && Group != null)
        {
            Stats.Hp = 1;
            var handler = Game.Config.Handlers?.Death?.Coma;
            if (handler?.Value != null && World.WorldData.TryGetValue(handler.Value, out Status status))
            {
                Condition.Comatose = true;
                ApplyStatus(new CreatureStatus(status, this, null, attacker));
            }
            else
            {
                GameLog.Warning("No coma handler or status found - user {Name} died!");
                OnDeath();
            }
        }
        else if (Stats.Hp == 0)
        {
            OnDeath();
        }
        else
        {
            foreach (var item in Equipment)
                if (item.EquipmentSlot != (byte) ItemSlots.Weapon && !item.Undamageable)
                    item.Durability -= 1 / (item.MaximumDurability * (100 - Stats.Ac == 0 ? 1 : 100 - Stats.Ac));
        }

        UpdateAttributes(StatUpdateFlags.Current);
    }

    public override void Heal(double heal, Creature source = null)
    {
        base.Heal(heal, source);
        if (this is User) UpdateAttributes(StatUpdateFlags.Current);
    }

    private bool CheckCastableRestrictions(List<EquipmentRestriction> restrictions, out string message)
    {
        message = string.Empty;

        if (restrictions.Count == 0)
            return true;

        // First restriction to be verified passes.
        foreach (var restriction in restrictions)
            /* <Restrictions>
                      <Item Slot="Necklace">Nadurra Necklace</Item><!--is it equipped?-->
                      <Item>Nadurrua Necklace</Item><!--is it carried-->
                      <Item Slot="Weapon" Type="Claw"><!-- are you wearing a claw weapon?-->  
                      <Item Slot="Weapon" Type="None"><!-- are you wearing no weapon?-->  
                   </Restrictions>
               * 
               * 
               */
            if (restriction.Slot == EquipmentSlot.None && restriction.Value != null)
            {
                // Inventory check
                if (Inventory.ContainsName(restriction.Value))
                    return true;
                message = $"You lack the needed {restriction.Value}.";
            }
            else if (restriction.Value != null)
            {
                // Named slot with specific item restriction
                var item = Equipment.FindByName(restriction.Value);
                if (item.EquipmentSlot != (byte) restriction.Slot - 1)
                    message = $"You must have {restriction.Value} equipped";
                else
                    return true;
            }
            else
            {
                if (restriction.Slot == EquipmentSlot.Weapon)
                {
                    if (Equipment.Weapon == null && restriction.Type == WeaponType.None)
                        return true;
                    if (Equipment.Weapon != null && restriction.Type == Equipment.Weapon.WeaponType)
                        return true;
                    message = "You can't use this with your current class of weapon.";
                }
                else
                {
                    // TODO: improve message and check
                    var slot = (byte) restriction.Slot - 1;
                    if (Equipment[(byte) slot] != null)
                        message = "You are missing some equipment.";
                }
            }

        return false;
    }

    public bool UseCastable(Castable castableXml, Creature target = null, bool castCost = true)
    {
        if (castableXml.Intents[0].UseType == SpellUseType.Prompt)
            //do something. 
            return false;

        // Check casting costs
        if (castCost)
        {
            if (!ProcessCastingCost(castableXml, target, out var message))
            {
                SendSystemMessage(message);
                return false;
            }
        }

        if (CheckCastableRestrictions(castableXml.Restrictions, out var restrictionMessage))
            return base.UseCastable(castableXml, target);
        SendSystemMessage(restrictionMessage);
        return false;
    }

    public void AssailAttack(Direction direction, Creature target = null)
    {
        target ??= GetDirectionalTarget(direction);
        var animation = false;

        foreach (var c in SkillBook.Where(c => c.Castable is { IsAssail: true }))
            if (target != null && target.GetType() != typeof(Merchant))
            {
                UseSkill(SkillBook.SlotOf(c.Castable.Name));
                animation = true;
            }

        if (!animation)
        {
            var motionId = (byte) 1;
            if (Class == Class.Warrior)
                if (SkillBook.Any(predicate: b => b.Castable.Name == "Wield Two-Handed Weapon"))
                    if (Equipment.Weapon?.WeaponType == WeaponType.TwoHand &&
                        Equipment.Armor?.Class == Class.Warrior)
                        motionId = 129;

            if (Class == Class.Monk)
            {
                if (Equipment.Armor?.Class == Class.Monk)
                {
                    motionId = 132;
                    if (Equipment.Weapon != null)
                        if (Equipment.Weapon?.WeaponType == WeaponType.OneHand ||
                            Equipment.Weapon?.WeaponType == WeaponType.Dagger ||
                            Equipment.Weapon?.WeaponType == WeaponType.Staff)
                            motionId = 1;
                }

                if (Equipment.Shield != null) motionId = 1;
            }

            var firstAssail = SkillBook.FirstOrDefault(x => x.Castable is {IsAssail:true});
            var soundId = firstAssail != null ? firstAssail.Castable.Effects.Sound.Id : (byte) 1;
            if (firstAssail != null && firstAssail.Castable.TryGetMotion(Class, out var motion))
                Motion(motion.Id, motion.Speed);
            PlaySound(soundId);
        }
    }


    private string GroupProfileSegment()
    {
        var sb = new StringBuilder();

        // Only build this string if the user's in a group. Otherwise an empty
        // string should be sent.
        if (!Grouped) return sb.ToString();
        sb.Append("Group members");
        sb.Append((char) 0x0A);

        // The user's name should go first, and should not have an asterisk.
        // In practice this will mean that the user's name appears first and
        // is grayed out, while all other names are white.
        sb.Append("  " + Name);
        sb.Append((char) 0x0A);

        foreach (var member in Group.Members)
            if (member.Name != Name)
            {
                sb.Append("  " + member.Name);
                sb.Append((char) 0x0A);
            }

        sb.Append($"Total {Group.Members.Count}");

        return sb.ToString();
    }

    /// <summary>
    ///     Send a player's profile to themselves (e.g. click on self or hit Y for group info)
    /// </summary>
    public void SendProfile()
    {
        var profile = new ServerPacketStructures.PlayerProfile
        {
            Player = this,
            NationFlag = Nation.Flag,
            GuildRank = GetGuildInfo().GuildRank,
            CurrentTitle = Title,
            Group = Group,
            IsGrouped = Grouped,
            CanGroup = Grouping,
            GroupRecruit = GroupRecruit ?? Group?.RecruitInfo ?? null,
            Class = (byte) Class,
            ClassName = IsMaster ? "Master" : Constants.REVERSE_CLASSES[(int) Class].Capitalize(),
            GuildName = GetGuildInfo().GuildName,
            PlayerDisplay = Equipment.Armor?.BodyStyle ?? 0
        };

        Enqueue(profile.Packet());
    }

    /// <summary>
    ///     Update a player's last login time in the database and the live object.
    /// </summary>
    public void UpdateLoginTime()
    {
        AuthInfo.LastLogin = DateTime.Now;
    }

    /// <summary>
    ///     Update a player's last logoff time in the database and the live object.
    /// </summary>
    public void UpdateLogoffTime()
    {
        AuthInfo.LastLogoff = DateTime.Now;
    }

    public void SendWorldMap(WorldMap map)
    {
        var x2E = new ServerPacket(0x2E);
        x2E.Write(map.GetBytes());
        x2E.DumpPacket();
        IsAtWorldMap = true;
        Enqueue(x2E);
    }

    public void SendAnimation(uint id, byte motion, short speed)
    {
        var anim = new ServerPacketStructures.PlayerAnimation { Animation = motion, Speed = speed, UserId = id };
        Enqueue(anim.Packet());
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
        if (CurrentMusicTrack == track) return;

        CurrentMusicTrack = track;

        var x19 = new ServerPacket(0x19);
        x19.WriteByte(0xFF);
        x19.WriteByte(track);
        Enqueue(x19);
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

    public void OpenManufacture(IEnumerable<ManufactureRecipe> recipes)
    {
        ManufactureState = new ManufactureState(this, recipes);
        ManufactureState.ShowWindow();
    }

    public void ShowLearnSkillMenu(Merchant merchant)
    {
        var merchantSkills = new MerchantSkills();
        merchantSkills.Skills = new List<MerchantSkill>();

        foreach (var skill in merchant.Template.Roles.Train
                     .Where(predicate: x => x.Type == "Skill" &&
                                            (x.Class.Contains(Class) || x.Class.Contains(Class.Peasant)))
                     .OrderBy(keySelector: y => y.Name))
            if (Game.World.WorldData.TryGetValueByIndex(skill.Name, out Castable result))
            {
                if (SkillBook.Contains(result.Id)) continue;
                merchantSkills.Skills.Add(new MerchantSkill
                {
                    IconType = 3,
                    Icon = result.Icon,
                    Color = 1,
                    Name = result.Name
                });
            }

        merchantSkills.Id = (ushort) MerchantMenuItem.LearnSkill;

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.MerchantSkills,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = Convert.ToByte(string.IsNullOrEmpty(Portrait)),
            Name = merchant.Name,
            Text = merchant.GetLocalString("learn_skill"),
            Skills = merchantSkills
        };

        Enqueue(packet.Packet());
    }

    public void ShowForgetSkillMenu(Merchant merchant)
    {
        var userSkills = new UserSkillBook
        {
            Id = (ushort) MerchantMenuItem.ForgetSkillAccept
        };

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.UserSkillBook,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("forget_skill"),
            UserSkills = userSkills
        };

        Enqueue(packet.Packet());
    }

    public void ShowForgetSkillAccept(Merchant merchant, byte slot)
    {
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("forget_castable_success"),
            Options = options
        };
        Enqueue(packet.Packet());

        SkillBook.Remove(slot);
        SendClearSkill(slot);
    }

    public void ShowForgetSpellMenu(Merchant merchant)
    {
        var userSpells = new UserSpellBook
        {
            Id = (ushort) MerchantMenuItem.ForgetSpellAccept
        };

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.UserSpellBook,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("forget_spell"),
            UserSpells = userSpells
        };

        Enqueue(packet.Packet());
    }

    public void ShowForgetSpellAccept(Merchant merchant, byte slot)
    {
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("forget_castable_success"),
            Options = options
        };
        Enqueue(packet.Packet());

        SpellBook.Remove(slot);
        SendClearSpell(slot);
    }

    public void ShowLearnSkill(Merchant merchant, Castable castable)
    {
        var skillDesc =
            castable.Descriptions.Single(predicate: x => x.Class.Contains(Class) || x.Class.Contains(Class.Peasant));

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();

        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort) MerchantMenuItem.LearnSkillAgree,
            Text = "Yes"
        });
        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort) MerchantMenuItem.LearnSkillDisagree,
            Text = "No"
        });

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("learn_skill", ("$NAME", castable.Name),
                ("$DESC", skillDesc.Value)),
            Options = options
        };

        PendingLearnableCastable = castable;

        Enqueue(packet.Packet());
    }

    public void ShowLearnSkillAgree(Merchant merchant)
    {
        var castable = PendingLearnableCastable;
        //now check requirements.
        var classReq = castable.Requirements.Single(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();
        var prompt = string.Empty;
        if (classReq.Level.Min > Stats.Level)
            prompt = merchant.GetLocalString("learn_skill_player_level", ("$NAME", castable.Name),
                ("$LEVEL", classReq.Level.Min.ToString()));
        if (classReq.Physical != null)
            if (Stats.Str < classReq.Physical.Str || Stats.Int < classReq.Physical.Int ||
                Stats.Wis < classReq.Physical.Wis || Stats.Con < classReq.Physical.Con ||
                Stats.Dex < classReq.Physical.Dex)
                prompt = merchant.GetLocalString("learn_skill_prereq_stats", ("$NAME", castable.Name),
                    ("$STATS",
                        $"\n[STR {classReq.Physical.Str} INT {classReq.Physical.Int} WIS {classReq.Physical.Wis} CON {classReq.Physical.Con} DEX {classReq.Physical.Dex}]")
                );
        if (classReq.Prerequisites != null)
            foreach (var preReq in classReq.Prerequisites)
            {
                BookSlot slot;
                if (Game.World.WorldData.TryGetValueByIndex(preReq.Value, out Castable castablePrereq))
                {
                    if (!SkillBook.Contains(castablePrereq.Id) && !SpellBook.Contains(castablePrereq.Id))
                    {
                        prompt = merchant.GetLocalString("learn_skill_prereq_level", ("$NAME", castable.Name),
                            ("$PREREQ", preReq.Value), ("$LEVEL", preReq.Level.ToString()));
                        break;
                    }

                    if (SkillBook.Contains(castablePrereq.Id))
                        slot = SkillBook.Single(predicate: x => x.Castable.Name == preReq.Value);
                    else
                        slot = SpellBook.Single(predicate: x => x.Castable.Name == preReq.Value);

                    if (Math.Floor(slot.UseCount / (double) slot.Castable.Mastery.Uses * 100) < preReq.Level)
                    {
                        prompt = merchant.GetLocalString("learn_skill_prereq_level", ("$NAME", castable.Name),
                            ("$PREREQ", preReq.Value), ("$LEVEL", preReq.Level.ToString()));
                        break;
                    }
                }
                else
                {
                    prompt = merchant.GetLocalString("learn_error");
                }
            }

        if (prompt == string.Empty) //this is so bad
        {
            var reqStr = string.Empty;
            //now we can learning!
            if (classReq.Items != null)
                reqStr = classReq.Items.Aggregate(reqStr,
                    func: (current, req) => current + req.Value + "(" + req.Quantity + "), ");

            if (classReq.Gold != 0)
                reqStr += classReq.Gold + " coins";
            else
                reqStr = reqStr.Remove(reqStr.Length - 1);

            prompt = merchant.GetLocalString("learn_skill_reqs", ("$NAME", castable.Name), ("$REQS", reqStr));

            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.LearnSkillAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.LearnSkillDisagree,
                Text = "No"
            });
        }


        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
        var classReq = castable.Requirements.Single(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);

        var prompt = string.Empty;
        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();
        //verify user has required items.
        if (!(Gold >= classReq.Gold)) prompt = merchant.GetLocalString("learn_skill_prereq_gold");
        if (prompt == string.Empty)
            if (classReq.Items.Any(predicate: itemReq => !Inventory.ContainsName(itemReq.Value, itemReq.Quantity)))
                prompt = merchant.GetLocalString("learn_skill_prereq_item");

        if ((SkillBook.IsPrimaryFull && castable.Book == Xml.Book.PrimarySkill) ||
            (SkillBook.IsSecondaryFull && castable.Book == Xml.Book.SecondarySkill) ||
            (SkillBook.IsUtilityFull && castable.Book == Xml.Book.UtilitySkill))
            prompt = merchant.GetLocalString("learn_skill_book_full");

        if (prompt == string.Empty)
        {
            RemoveGold(classReq.Gold);
            foreach (var req in classReq.Items) RemoveItem(req.Value, req.Quantity);
            SkillBook.Add(castable);
            SendInventory();
            SendSkills();
            prompt = merchant.GetLocalString("learn_skill_success");
        }

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();
        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("forget_castable_success"),
            Options = options
        };

        Enqueue(packet.Packet());
    }

    public void ShowLearnSpellMenu(Merchant merchant)
    {
        var merchantSpells = new MerchantSpells();
        merchantSpells.Spells = new List<MerchantSpell>();

        foreach (var spell in merchant.Template.Roles.Train
                     .Where(predicate: x => x.Type == "Spell" &&
                                            (x.Class.Contains(Class) || x.Class.Contains(Class.Peasant)))
                     .OrderBy(keySelector: y => y.Name))
        {
            // Verify the spell exists first
            if (!Game.World.WorldData.TryGetValueByIndex(spell.Name, out Castable result)) continue;
            if (SpellBook.Contains(result.Id)) continue;
            merchantSpells.Spells.Add(new MerchantSpell
            {
                IconType = 2,
                Icon = result.Icon,
                Color = 1,
                Name = result.Name
            });
        }

        merchantSpells.Id = (ushort) MerchantMenuItem.LearnSpell;

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.MerchantSpells,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("learn_spell"),
            Spells = merchantSpells
        };

        Enqueue(packet.Packet());
    }

    public void ShowLearnSpell(Merchant merchant, Castable castable)
    {
        var spellDesc =
            castable.Descriptions.Single(predicate: x => x.Class.Contains(Class) || x.Class.Contains(Class.Peasant));

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();

        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort) MerchantMenuItem.LearnSpellAgree,
            Text = "Yes"
        });
        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort) MerchantMenuItem.LearnSpellDisagree,
            Text = "No"
        });

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("learn_spell_choice", ("$NAME", castable.Name),
                ("$DESC", spellDesc.Value)),
            Options = options
        };

        PendingLearnableCastable = castable;

        Enqueue(packet.Packet());
    }

    public void ShowLearnSpellAgree(Merchant merchant)
    {
        var castable = PendingLearnableCastable;
        //now check requirements.
        var classReq = castable.Requirements.Single(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);
        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();
        var prompt = string.Empty;

        if (classReq.Level.Min > Stats.Level)
            prompt = merchant.GetLocalString("learn_spell_player_level", ("$NAME", castable.Name),
                ("$LEVEL", classReq.Level.Min.ToString()));
        if (classReq.Physical != null)
            if (Stats.Str < classReq.Physical.Str || Stats.Int < classReq.Physical.Int ||
                Stats.Wis < classReq.Physical.Wis || Stats.Con < classReq.Physical.Con ||
                Stats.Dex < classReq.Physical.Dex)
                prompt = merchant.GetLocalString("learn_spell_prereq_stats", ("$NAME", castable.Name),
                    ("$STATS",
                        $"\n[STR {classReq.Physical.Str} INT {classReq.Physical.Int} WIS {classReq.Physical.Wis} CON {classReq.Physical.Con} DEX {classReq.Physical.Dex}]")
                );
        if (classReq.Prerequisites != null)
            foreach (var preReq in classReq.Prerequisites)
            {
                BookSlot slot;
                if (Game.World.WorldData.TryGetValueByIndex(preReq.Value, out Castable castablePrereq))
                {
                    if (!SkillBook.Contains(castablePrereq.Id) && !SpellBook.Contains(castablePrereq.Id))
                    {
                        prompt = merchant.GetLocalString("learn_spell_prereq_level", ("$NAME", castable.Name),
                            ("$PREREQ", preReq.Value), ("$LEVEL", preReq.Level.ToString()));
                        break;
                    }

                    if (SkillBook.Contains(castablePrereq.Id))
                        slot = SkillBook.Single(predicate: x => x.Castable.Name == preReq.Value);
                    else
                        slot = SpellBook.Single(predicate: x => x.Castable.Name == preReq.Value);
                    if (Math.Floor(slot.UseCount / (double) slot.Castable.Mastery.Uses * 100) < preReq.Level)
                    {
                        prompt = merchant.GetLocalString("learn_spell_prereq_level", ("$NAME", castable.Name),
                            ("$PREREQ", preReq.Value), ("$LEVEL", preReq.Level.ToString()));
                        break;
                    }
                }
                else
                {
                    prompt = merchant.GetLocalString("learn_error");
                }
            }

        if (prompt == string.Empty) //this is so bad
        {
            var reqStr = string.Empty;
            //now we can learning!
            if (classReq.Items != null)
                reqStr = classReq.Items.Aggregate(reqStr,
                    func: (current, req) => current + req.Value + "(" + req.Quantity + "), ");

            if (classReq.Gold != 0)
            {
                if (reqStr != string.Empty)
                    reqStr += $" and {classReq.Gold} coins";
                else
                    reqStr += $"{classReq.Gold} coins";
            }
            else
            {
                reqStr = reqStr.Remove(reqStr.Length - 1);
            }

            prompt = merchant.GetLocalString("learn_spell_reqs", ("$NAME", castable.Name), ("$REQS", reqStr));

            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.LearnSpellAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.LearnSpellDisagree,
                Text = "No"
            });
        }

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
        var classReq = castable.Requirements.Single(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);
        var prompt = string.Empty;
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };
        //verify user has required items.
        if (!(Gold >= classReq.Gold)) prompt = merchant.GetLocalString("learn_spell_prereq_gold");
        if (prompt == string.Empty)
            if (classReq.Items.Any(predicate: itemReq => !Inventory.ContainsName(itemReq.Value, itemReq.Quantity)))
                prompt = merchant.GetLocalString("learn_spell_prereq_item");

        if ((SpellBook.IsPrimaryFull && castable.Book == Xml.Book.PrimarySpell) ||
            (SpellBook.IsSecondaryFull && castable.Book == Xml.Book.SecondarySpell) ||
            (SpellBook.IsUtilityFull && castable.Book == Xml.Book.UtilitySpell))
            prompt = merchant.GetLocalString("learn_spell_book_full");

        if (prompt == string.Empty)
        {
            RemoveGold(classReq.Gold);
            foreach (var req in classReq.Items) RemoveItem(req.Value, req.Quantity);
            SpellBook.Add(castable);
            SendInventory();
            SendSpells();
            prompt = merchant.GetLocalString("learn_spell_success");
        }

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("forget_castable_success"),
            Options = options
        };

        Enqueue(packet.Packet());
    }

    public void ShowBuyMenu(Merchant merchant)
    {
        var merchantItems = new MerchantShopItems();
        merchantItems.Items = new List<MerchantShopItem>();
        var itemsCount = 0;

        foreach (var item in merchant.GetOnHandInventory())
            if (item.OnHand > 0)
            {
                var worldItem = item.Item;
                merchantItems.Items.Add(new MerchantShopItem
                {
                    Tile = (ushort) (0x8000 + worldItem.Properties.Appearance.Sprite),
                    Color = (byte) worldItem.Properties.Appearance.Color,
                    Description = worldItem.Properties.Vendor?.Description ?? "",
                    Name = worldItem.Name,
                    Price = Convert.ToUInt32(worldItem.Properties.Physical.Value)
                });
                itemsCount++;
            }

        merchantItems.Id = (ushort) MerchantMenuItem.BuyItemQuantity;


        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.MerchantShopItems,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("buy"),
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
            var input = new MerchantInput();

            input.Id = (ushort) MerchantMenuItem.BuyItemAccept;


            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Input,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("buy_quantity"),
                Input = input
            };
            Enqueue(packet.Packet());
        }
        else //buy item
        {
            ShowBuyItem(merchant);
        }
    }

    public void ShowBuyItem(Merchant merchant, uint quantity = 1)
    {
        var prompt = string.Empty;
        var item = Game.World.WorldData.GetByIndex<Item>(PendingBuyableItem);
        var itemObj = Game.World.CreateItem(item.Id);
        var reqGold = itemObj.Value * quantity;
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        if (MaximumWeight < CurrentWeight + item.Properties.Physical.Weight)
            prompt = merchant.GetLocalString("buy_failure_weight");

        if (quantity > merchant.GetOnHand(PendingBuyableItem))
            prompt = merchant.GetLocalString("buy_failure_quantity");
        if (Gold < reqGold) prompt = merchant.GetLocalString("buy_failure_gold");

        if (prompt == string.Empty) //this is so bad
        {
            //check if user has item
            var hasItem = Inventory.ContainsName(itemObj.Name);
            if (hasItem)
            {
                if (itemObj.Stackable)
                {
                    merchant.ReduceInventory(PendingBuyableItem, quantity);
                    AddItem(itemObj.Name, (ushort) quantity);
                }
                else
                {
                    merchant.ReduceInventory(PendingBuyableItem, quantity);
                    AddItem(itemObj);
                }
            }
            else
            {
                if (itemObj.Stackable)
                {
                    merchant.ReduceInventory(PendingBuyableItem, quantity);
                    AddItem(itemObj.Name, (ushort) quantity);
                }
                else
                {
                    merchant.ReduceInventory(PendingBuyableItem, quantity);
                    AddItem(itemObj);
                }
            }

            RemoveGold(reqGold);
            SendCloseDialog();
        }
        else
        {
            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
        var inventoryItems = new UserInventoryItems();
        inventoryItems.InventorySlots = new List<byte>();
        inventoryItems.Id = (ushort) MerchantMenuItem.SellItemQuantity;
        var itemsCount = 0;

        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] == null) continue;
            if (Inventory[i].Exchangeable && Inventory[i].Durability == Inventory[i].MaximumDurability)
            {
                inventoryItems.InventorySlots.Add(i);
                itemsCount++;
            }
        }

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.UserInventoryItems,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("sell"),
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
            var input = new MerchantInput();

            input.Id = (ushort) MerchantMenuItem.SellItem;

            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Input,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("sell_quantity", ("$QUANTITY", item.Count.ToString()),
                    ("$ITEM", item.Name)),
                Input = input
            };
            Enqueue(packet.Packet());
        }
        else
        {
            ShowSellConfirm(merchant, slot);
        }
    }

    public void ShowSellConfirm(Merchant merchant, byte slot, uint quantity = 1)
    {
        PendingSellableSlot = slot;
        PendingSellableQuantity = quantity;
        var item = Inventory[slot];
        var offer = (uint) (Math.Round(item.Value * Game.Config.Constants.MerchantBuybackPercentage, 0) *
                            quantity);
        PendingMerchantOffer = offer;
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };
        var prompt = string.Empty;

        if (quantity > ushort.MaxValue) quantity = ushort.MaxValue;

        if (item.Durability != item.MaximumDurability)
            prompt = merchant.GetLocalString("sell_failure_durability");

        if (prompt == string.Empty)
            if (!Inventory.ContainsName(item.Name))
                prompt = merchant.GetLocalString("sell_failure_no_item");

        if (prompt == string.Empty)
            if (!Inventory.ContainsName(item.Name, (int) quantity))
                prompt = merchant.GetLocalString("sell_failure_quantity");

        if (prompt == string.Empty)
            if (PendingMerchantOffer + Gold > Constants.MAXIMUM_GOLD)
                prompt = merchant.GetLocalString("sell_failure_gold_limit");

        if (prompt == string.Empty)
        {
            var quant = quantity > 1 ? "those" : "that";

            prompt = merchant.GetLocalString("sell_offer", ("$GOLD", offer.ToString()), ("$QUANTITY", quant));

            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.SellItemAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.MainMenu,
                Text = "No"
            });
        }


        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
            DecreaseItem(PendingSellableSlot, (int) PendingSellableQuantity);
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

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("sell_success"),
            Options = options
        };

        Enqueue(packet.Packet());
    }

    public void ShowMerchantGoBack(Merchant merchant, string message,
        MerchantMenuItem menuItem = MerchantMenuItem.MainMenu)
    {
        var x2F = new ServerPacket(0x2F);
        x2F.WriteByte(0x00); // type!
        x2F.WriteByte(0x01); // obj type
        x2F.WriteUInt32(merchant.Id);
        x2F.WriteByte(0x01); // ??
        x2F.WriteUInt16((ushort) (0x4000 + merchant.Sprite));
        x2F.WriteByte(0x00); // color
        x2F.WriteByte(0x01); // ??
        x2F.WriteUInt16((ushort) (0x4000 + merchant.Sprite));
        x2F.WriteByte(0x00); // color
        x2F.WriteByte(0x00); // ??
        x2F.WriteString8(merchant.Name);
        x2F.WriteString16(message);
        x2F.WriteByte(1);
        x2F.WriteString8("Go back");
        x2F.WriteUInt16((ushort) menuItem);
        Enqueue(x2F);
    }

    public void ShowMerchantSendParcel(Merchant merchant)
    {
        var userItems = new UserInventoryItems { InventorySlots = new List<byte>() };
        var itemsCount = 0;

        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] == null) continue;
            if (Inventory[i].Exchangeable && Inventory[i].Durability == Inventory[i].MaximumDurability)
            {
                userItems.InventorySlots.Add(i);
                itemsCount++;
            }
        }

        userItems.Id = (ushort) MerchantMenuItem.SendParcelQuantity;

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.UserInventoryItems,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("send_parcel"),
            UserInventoryItems = userItems
        };
        Enqueue(packet.Packet());
    }

    public void ShowMerchantSendParcelQuantity(Merchant merchant, ItemObject item)
    {
        if (item.Stackable && item.Count > 1)
        {
            var input = new MerchantInput
            {
                Id = (ushort) MerchantMenuItem.SendParcelRecipient
            };

            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Input,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("send_parcel_recipient", ("$QUANTITY", item.Count.ToString()),
                    ("$ITEM", item.Name)),
                Input = input
            };
            Enqueue(packet.Packet());
        }
        else
        {
            ShowMerchantSendParcelRecipient(merchant);
        }

        PendingSendableParcel = item;
    }

    public void ShowMerchantSendParcelRecipient(Merchant merchant, uint quantity = 1)
    {
        PendingSendableQuantity = quantity;
        var input = new MerchantInput
        {
            Id = (ushort) MerchantMenuItem.SendParcelAccept
        };

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Input,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("send_parcel_recipient"),
            Input = input
        };


        Enqueue(packet.Packet());
    }

    public void ShowMerchantSendParcelAccept(Merchant merchant, string recipient)
    {
        var itemObj = PendingSendableParcel;
        var quantity = PendingSendableQuantity;
        PendingParcelRecipient = recipient;
        var prompt = string.Empty;
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };
        //verify user has required items.
        var parcelFee = (uint) Math.Ceiling(itemObj.Value * .10 * quantity);
        if (!Game.World.WorldData.TryGetAuthInfo(recipient, out var info))
            prompt = merchant.GetLocalString("parcel_recipient_nonexistent");
        if (prompt == string.Empty)
            if (!(Gold > parcelFee))
                prompt = merchant.GetLocalString("send_parcel_fail", ("$FEE", parcelFee.ToString()));
        if (prompt == string.Empty)
        {
            RemoveGold(parcelFee);
            RemoveItem(itemObj.Name, (ushort) quantity);
            SendInventory();
            prompt = merchant.GetLocalString("send_parcel_success");

            var guidRef = World.WorldData.GetGuidReference(recipient);
            var parcelStore = World.WorldData.GetOrCreate<ParcelStore>(guidRef);
            var recipientMailbox = World.WorldData.GetOrCreate<Mailbox>(guidRef);
            var mboxString = merchant.GetLocalString("send_parcel_mailbox_message",
                ("$SENDER", Name), ("$ITEM", $"{itemObj.Name} (qty {quantity})"));

            recipientMailbox.ReceiveMessage(new Message(recipient, merchant.Name,
                merchant.GetLocalString("send_parcel_mailbox_subject", ("$NAME", Name)), mboxString));
            parcelStore.AddItem(Name, itemObj.Name, quantity);
            parcelStore.Save();
            if (info.IsLoggedIn && Game.World.TryGetActiveUser(recipient, out var recipientUser))
            {
                recipientUser.SendSystemMessage(merchant.GetLocalString("send_parcel_system_msg",
                    ("$NAME", Name)));
                recipientUser.UpdateAttributes(StatUpdateFlags.UnreadMail);
            }

            PendingSellableQuantity = 0;
            PendingSendableParcel = null;
        }

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("receive_parcel"),
            Options = options
        };

        //TODO: Get Parcel from pending mail.
        ParcelStore.RemoveItem(this);

        Enqueue(packet.Packet());
    }

    public void ShowDepositGoldMenu(Merchant merchant)
    {
        var coins = "coin";
        if (Vault.CurrentGold > 1) coins = "coins";
        var prompt = merchant.GetLocalString("deposit_gold", ("$COINS", Vault.CurrentGold.ToString()),
            ("$REF", coins));

        var input = new MerchantInput();
        input.Id = (ushort) MerchantMenuItem.DepositGoldQuantity;

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Input,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
        if (amount > Gold)
        {
            prompt = merchant.GetLocalString("deposit_gold_failure_deficit");
            ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.DepositGoldMenu);
        }
        else
        {
            if (amount > Vault.RemainingGold)
            {
                var coins = "coin";
                if (amount > 1) coins = "coins";
                prompt = merchant.GetLocalString("deposit_gold_failure_surplus",
                    ("$COINS", Vault.RemainingGold.ToString()), ("$REF", coins));
                ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.DepositGoldMenu);
            }
            else
            {
                Vault.AddGold(amount);
                Vault.Save();
                RemoveGold(amount);
                var coins = "coin";
                if (amount > 1) coins = "coins";
                prompt = merchant.GetLocalString("deposit_gold_success", ("$COINS", amount.ToString()),
                    ("$REF", coins));
                merchant.Say(prompt);
                SendCloseDialog();
            }
        }
    }

    public void ShowWithdrawGoldMenu(Merchant merchant)
    {
        var coins = "coin";
        if (Vault.CurrentGold > 1) coins = "coins";

        var prompt = merchant.GetLocalString("withdraw_gold", ("$COINS", Vault.CurrentGold.ToString()),
            ("$REF", coins));

        var input = new MerchantInput
        {
            Id = (ushort) MerchantMenuItem.WithdrawGoldQuantity
        };

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Input,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
            prompt = merchant.GetLocalString("withdraw_gold_failure_deficit");
            ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.WithdrawGoldMenu);
        }
        else
        {
            if (amount > uint.MaxValue - Gold)
            {
                prompt = merchant.GetLocalString("withdraw_gold_failure_surplus");
                ShowMerchantGoBack(merchant, prompt, MerchantMenuItem.WithdrawGoldMenu);
            }
            else
            {
                Vault.RemoveGold(amount);
                Vault.Save();
                AddGold(amount);
                var coins = "coin";
                if (amount > 1) coins = "coins";
                prompt = merchant.GetLocalString("withdraw_gold_success", ("$COINS", amount.ToString()),
                    ("$REF", coins));
                merchant.Say(prompt);
                SendCloseDialog();
            }
        }
    }

    public void ShowDepositItemMenu(Merchant merchant)
    {
        var inventoryItems = new UserInventoryItems();
        inventoryItems.InventorySlots = new List<byte>();
        inventoryItems.Id = (ushort) MerchantMenuItem.DepositItemQuantity;

        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] == null) continue;
            if (Inventory[i].Exchangeable && Inventory[i].Durability == Inventory[i].MaximumDurability)
                inventoryItems.InventorySlots.Add(i);
        }

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.UserInventoryItems,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("deposit_item"),
            UserInventoryItems = inventoryItems
        };
        Enqueue(packet.Packet());
    }

    public void ShowDepositItemQuantity(Merchant merchant, byte slot)
    {
        var item = Inventory[slot];
        PendingDepositSlot = slot;
        if (item.Stackable && item.Count > 0)
        {
            var input = new MerchantInput
            {
                Id = (ushort) MerchantMenuItem.DepositItem
            };

            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Input,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("deposit_item_quantity", ("$QUANTITY", item.Count.ToString()),
                    ("$ITEM", item.Name)),
                Input = input
            };
            Enqueue(packet.Packet());
        }
        else
        {
            DepositItemConfirm(merchant, slot);
        }
    }

    public void DepositItemConfirm(Merchant merchant, byte slot, uint quantity = 1)
    {
        var failure = false;

        var item = Inventory[slot];

        if (quantity > ushort.MaxValue) quantity = ushort.MaxValue;

        var fee = (uint) (Math.Round(item.Value * 0.10, 0) * quantity);

        var prompt = string.Empty;

        if (item.Durability != item.MaximumDurability)
        {
            prompt = merchant.GetLocalString("deposit_item_failure_durability");
            failure = true;
        }


        if (!Inventory.ContainsName(item.Name) && !failure)
        {
            prompt = merchant.GetLocalString("deposit_item_failure_quantity");
            failure = true;
        }

        if (item.Stackable && item.Count < quantity && !failure)
        {
            prompt = merchant.GetLocalString("deposit_item_failure_quantity");
            failure = true;
        }

        if (fee > Gold && !failure)
        {
            var coins = "coin";
            if (fee > 1) coins = "coins";
            prompt = prompt = merchant.GetLocalString("deposit_item_failure_fee", ("$COINS", fee.ToString()),
                ("$REF", coins));
            failure = true;
        }


        if (prompt == string.Empty && !failure) //this is so bad
        {
            var coins = "coin";
            if (fee > 1) coins = "coins";
            //we can deposit!
            prompt = merchant.GetLocalString("deposit_item_success", ("$ITEM", item.Name),
                ("$QUANTITY", quantity.ToString()), ("$COINS", fee.ToString()), ("$REF", coins));
            Vault.AddItem(item.Name, (ushort) quantity);
            if (Inventory[slot].Stackable && Inventory[slot].Count > quantity)
                RemoveItem(item.Name, (ushort) quantity);
            else
                RemoveItem(slot);

            RemoveGold(fee);
            Vault.Save();
            failure = false;
        }

        if (failure)
        {
            var options = new MerchantOptions
            {
                Options = new List<MerchantDialogOption>()
            };
            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
        PendingRepairCost = 0;
        var inventoryItems = new UserInventoryItems();
        inventoryItems.InventorySlots = new List<byte>();
        inventoryItems.Id = (ushort) MerchantMenuItem.RepairItem;
        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] == null) continue;
            if (Inventory[i].Durability != Inventory[i].MaximumDurability) inventoryItems.InventorySlots.Add(i);
        }

        if (inventoryItems.InventorySlots.Count > 0)
        {
            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.UserInventoryItems,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("repair_item"),
                UserInventoryItems = inventoryItems
            };
            Enqueue(packet.Packet());
        }
        else
        {
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("repair_item_none"),
                Options = options
            };
            Enqueue(packet.Packet());
        }
    }

    public void ShowRepairItem(Merchant merchant, byte slot)
    {
        var prompt = string.Empty;
        var item = Inventory[slot];

        PendingRepairSlot = slot;

        PendingRepairCost =
            (uint) Math.Ceiling(item.Value - item.Durability / item.MaximumDurability * item.Value);

        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        if (PendingRepairCost <= 1)
        {
            prompt = merchant.GetLocalString("repair_item_nocost");
        }
        else
        {
            prompt = merchant.GetLocalString("repair_item_nocost", ("$COINS", PendingRepairCost.ToString()));
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.RepairItemAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort) MerchantMenuItem.MainMenu,
                Text = "No"
            });
        }

        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.Options,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
        if (Gold < PendingRepairCost)
        {
            var options = new MerchantOptions
            {
                Options = new List<MerchantDialogOption>()
            };

            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("repair_item_fail"),
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
            (merchant as IPursuitable).DisplayPursuits(this);
        }
    }

    public void ShowRepairAllItems(Merchant merchant)
    {
        var prompt = string.Empty;
        var repairableCount = 0;

        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] == null) continue;
            if (Inventory[i].Durability != Inventory[i].MaximumDurability)
            {
                var item = Inventory[i];
                PendingRepairCost +=
                    (uint) Math.Ceiling(item.Value - item.Durability / item.MaximumDurability * item.Value);
                repairableCount++;
            }
        }

        for (byte i = 1; i <= Equipment.Size; i++)
        {
            if (Equipment[i] == null) continue;
            if (Equipment[i].Durability != Equipment[i].MaximumDurability)
            {
                var item = Equipment[i];
                PendingRepairCost +=
                    (uint) Math.Ceiling(item.Value - item.Durability / item.MaximumDurability * item.Value);
                repairableCount++;
            }
        }

        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        if (repairableCount > 0)
        {
            if (PendingRepairCost <= 1)
            {
                prompt = merchant.GetLocalString("repair_item_nocost");
            }
            else
            {
                prompt = merchant.GetLocalString("repair_all_items_cost",
                    ("$COINS", PendingRepairCost.ToString()));
                options.Options.Add(new MerchantDialogOption
                {
                    Id = (ushort) MerchantMenuItem.RepairAllItemsAccept,
                    Text = "Yes"
                });
                options.Options.Add(new MerchantDialogOption
                {
                    Id = (ushort) MerchantMenuItem.MainMenu,
                    Text = "No"
                });
            }

            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
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
            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("repair_item_none"),
                Options = options
            };
            Enqueue(packet.Packet());
        }
    }

    public void ShowRepairAllItemsAccept(Merchant merchant)
    {
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };
        if (Gold < PendingRepairCost)
        {
            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("repair_item_fail"),
                Options = options
            };
            Enqueue(packet.Packet());
        }
        else
        {
            RemoveGold(PendingRepairCost);
            PendingRepairCost = 0;
            for (byte i = 1; i <= Inventory.Size; i++)
            {
                if (Inventory[i] == null) continue;
                if (Inventory[i].Durability != Inventory[i].MaximumDurability)
                {
                    Inventory[i].Durability = Inventory[i].MaximumDurability;
                    SendItemUpdate(Inventory[i], i);
                }
            }

            for (byte i = 1; i <= Equipment.Size; i++)
            {
                if (Equipment[i] == null) continue;
                if (Equipment[i].Durability != Equipment[i].MaximumDurability)
                {
                    Equipment[i].Durability = Equipment[i].MaximumDurability;
                    //SendItemUpdate(Equipment[i], i);
                    AddEquipment(Equipment[i], i);
                }
            }

            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("repair_all_items_success"),
                Options = options
            };
            Enqueue(packet.Packet());
        }
    }

    public void ShowWithdrawItemMenu(Merchant merchant)
    {
        var merchantItems = new MerchantShopItems
        {
            Items = new List<MerchantShopItem>()
        };

        foreach (var item in Vault.Items)
        {
            Game.World.WorldData.TryGetValueByIndex<Item>(item.Key, out var worldItem);
            if (worldItem == null) continue;
            merchantItems.Items.Add(new MerchantShopItem
            {
                Tile = (ushort) (0x8000 + worldItem.Properties.Appearance.Sprite),
                Color = (byte) worldItem.Properties.Appearance.Color,
                Description = worldItem.Properties.Vendor?.Description ?? "",
                Name = worldItem.Name,
                Price = item.Value
            });
            ;
        }

        merchantItems.Id = (ushort) MerchantMenuItem.WithdrawItemQuantity;


        var packet = new ServerPacketStructures.MerchantResponse
        {
            MerchantDialogType = MerchantDialogType.MerchantShopItems,
            MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
            ObjectId = merchant.Id,
            Tile1 = (ushort) (0x4000 + merchant.Sprite),
            Color1 = 0,
            Tile2 = (ushort) (0x4000 + merchant.Sprite),
            Color2 = 0,
            PortraitType = 0,
            Name = merchant.Name,
            Text = merchant.GetLocalString("withdraw_item"),
            ShopItems = merchantItems
        };
        Enqueue(packet.Packet());
    }

    public void ShowWithdrawItemQuantity(Merchant merchant, string item)
    {
        var worldItem = World.WorldData.GetByIndex<Item>(item);
        if (worldItem.Stackable)
        {
            PendingWithdrawItem = item;

            var input = new MerchantInput();
            input.Id = (ushort) MerchantMenuItem.WithdrawItem;

            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Input,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = merchant.GetLocalString("withdraw_item_quantity"),
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
        var worldItem = World.WorldData.GetByIndex<Item>(item);


        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();

        var prompt = string.Empty;

        if (quantity > Vault.Items[item])
        {
            prompt = merchant.GetLocalString("withdraw_item_failure_quantity_bank",
                ("$QUANTITY", quantity.ToString()), ("$ITEM", item));
            failure = true;
        }
        else if (!failure && worldItem.Stackable)
        {
            if (CurrentWeight + worldItem.Properties.Physical.Weight > MaximumWeight)
            {
                merchant.GetLocalString("withdraw_item_failure_weight");
            }
            else
            {
                if (Inventory.ContainsName(item))
                {
                    var maxQuantity = 0;
                    var existingStacks = Inventory.GetSlotsByName(item);
                    foreach (var slot in existingStacks)
                        maxQuantity += Inventory[slot].MaximumStack - Inventory[slot].Count;
                    maxQuantity +=
                        (Inventory.EmptySlots - 2) * worldItem.MaximumStack; //account for slot 0 and gold slot

                    if (quantity > maxQuantity)
                        prompt = merchant.GetLocalString("withdraw_item_failure_quantity_inventory_diff",
                            ("$ITEM", item), ("$QUANTITY", maxQuantity.ToString()));
                }
                else
                {
                    if (Inventory.EmptySlots == 0)
                        prompt = merchant.GetLocalString("withdraw_item_failure_slot");
                }
            }
        }
        else
        {
            if (Inventory.EmptySlots == 0)
                prompt = merchant.GetLocalString("withdraw_item_failure_slot");
            else if (CurrentWeight + worldItem.Properties.Physical.Weight > MaximumWeight)
                prompt = merchant.GetLocalString("withdraw_item_failure_weight");
        }

        if (!failure && prompt == string.Empty)
        {
            prompt = merchant.GetLocalString("withdraw_item_success", ("$ITEM", item),
                ("$QUANTITY", quantity.ToString()));
            if (worldItem.Stackable)
            {
                Vault.RemoveItem(item, (ushort) quantity);
                AddItem(item, (ushort) quantity);
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
            var packet = new ServerPacketStructures.MerchantResponse
            {
                MerchantDialogType = MerchantDialogType.Options,
                MerchantDialogObjectType = MerchantDialogObjectType.Merchant,
                ObjectId = merchant.Id,
                Tile1 = (ushort) (0x4000 + merchant.Sprite),
                Color1 = 0,
                Tile2 = (ushort) (0x4000 + merchant.Sprite),
                Color2 = 0,
                PortraitType = 0,
                Name = merchant.Name,
                Text = prompt,
                Options = options
            };

            Enqueue(packet.Packet());
        }
    }

    public void SendMessage(string message, MessageType type)
    {
        SendMessage(message, (byte) type);
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
            // IT'S CHOPPIN TIME
            transmit = transmit.Substring(0, 67);
        x0A.WriteString16(transmit);
        Enqueue(x0A);
    }

    public void SendRedirectAndLogoff(World world, Login login, string name, int transmitDelay = 1200)
    {
        GlobalConnectionManifest.DeregisterClient(Client);
        Client.Redirect(
            new Redirect(Client, world, Game.Login, name, Client.EncryptionSeed, Client.EncryptionKey), true,
            transmitDelay);
    }

    public bool IsHeartbeatValid(byte a, byte b) => Client.IsHeartbeatValid(a, b);

    public bool IsHeartbeatValid(int localTickCount, int clientTickCount) =>
        Client.IsHeartbeatValid(localTickCount, clientTickCount);

    public bool IsHeartbeatExpired() => Client.IsHeartbeatExpired();

    public void Logoff(bool disconnect = false)
    {
        UpdateLogoffTime();
        Save(true);
        if (!disconnect)
        {
            var redirect = new Redirect(Client, Game.World, Game.Login, "socket", Client.EncryptionSeed,
                Client.EncryptionKey);
            Client.Redirect(redirect, true);
        }
        else
        {
            try
            {
                Client.Disconnect();
            }
            catch (Exception)
            {
                PreviousConnectionId = Client?.ConnectionId ?? -1;
                Client = null;
            }
        }
    }

    public void SetEncryptionParameters(byte[] key, byte seed, string name)
    {
        Client.EncryptionKey = key;
        Client.EncryptionSeed = seed;
        Client.GenerateKeyTable(name);
    }

    /// <summary>
    ///     Send an exchange initiation request to the client (open exchange window)
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
    ///     Send a quantity prompt request to the client (when dealing with stacked items)
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
    ///     Send an exchange update packet for an ItemObject to an active exchange participant.
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
            ItemName = toAdd.Stackable && toAdd.Count > 1 ? $"{toAdd.Name} [{toAdd.Count}]" : toAdd.Name
        };
        Enqueue(update.Packet());
    }

    /// <summary>
    ///     Send an exchange update packet for gold to an active exchange participant.
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
    ///     Send a cancellation notice for an exchange.
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
    ///     Send a confirmation notice for an exchange.
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

    public void SendInventorySlot(byte slot)
    {
        if (Inventory[slot] == null) return;
        var x0F = new ServerPacket(0x0F);
        x0F.WriteByte(slot);
        x0F.WriteUInt16((ushort) (Inventory[slot].Sprite + 0x8000));
        x0F.WriteByte(Inventory[slot].Color);
        x0F.WriteString8(Inventory[slot].Name);
        x0F.WriteInt32(Inventory[slot].Count);
        x0F.WriteBoolean(Inventory[slot].Stackable);
        x0F.WriteUInt32(Inventory[slot].MaximumDurability);
        x0F.WriteUInt32(Inventory[slot].DisplayDurability);
        Enqueue(x0F);
    }

    public void SendInventory()
    {
        for (byte i = 1; i < Inventory.Size; i++)
        {
            if (Inventory[i] == null) continue;
            if (Inventory[i].Id == 0) Game.World.Insert(Inventory[i]);
            var x0F = new ServerPacket(0x0F);
            x0F.WriteByte(i);
            x0F.WriteUInt16((ushort) (Inventory[i].Sprite + 0x8000));
            x0F.WriteByte(Inventory[i].Color);
            x0F.WriteString8(Inventory[i].Name);
            x0F.WriteInt32(Inventory[i].Count);
            x0F.WriteBoolean(Inventory[i].Stackable);
            x0F.WriteUInt32(Inventory[i].MaximumDurability);
            x0F.WriteUInt32(Inventory[i].DisplayDurability);
            Enqueue(x0F);
        }
    }

    public void SendEquipment()
    {
        for (byte i = 1; i < Equipment.Size; i++)
            if (Equipment[i] != null)
                SendEquipItem(Equipment[i], i);
    }

    public void SendSkills()
    {
        for (byte i = 0; i < SkillBook.Size; i++)
            if (SkillBook[i]?.Castable != null)
                SendSkillUpdate(SkillBook[i], i);
    }

    public void SendSpells()
    {
        for (byte i = 0; i < SpellBook.Size; i++)
            if (SpellBook[i]?.Castable != null)
                SendSpellUpdate(SpellBook[i], i);
    }

    public void ReapplyStatuses()
    {
        foreach (var status in Statuses)
            try
            {
                ApplyStatus(new CreatureStatus(status, this));
            }
            catch (ArgumentException e)
            {
                GameLog.Error(
                    "User {user}: status {status} could not be reapplied - exception occurred (likely not found): {e}",
                    Name, status.Name, e);
            }

        UpdateAttributes(StatUpdateFlags.Full);
        Statuses.Clear();
    }


    public bool IsInViewport(VisibleObject obj) => Map.EntityTree.GetObjects(GetViewport()).Contains(obj);


    public void SendSystemMessage(string msg)
    {
        LastSystemMessage = msg;
        Client?.SendMessage(msg, 3);
    }

    public void CancelCasting()
    {
        if (!Condition.Casting) return;
        var packet = new ServerPacketStructures.CancelCast();
        Enqueue(packet.Packet());
        Condition.Casting = false;
    }

    #region Appearance settings

    [JsonProperty] public RestPosition RestPosition { get; set; }

    [JsonProperty] public SkinColor SkinColor { get; set; }

    [JsonProperty] internal bool Transparent { get; set; }

    [JsonProperty] public byte FaceShape { get; set; }

    [JsonProperty] public LanternSize LanternSize { get; set; }

    [JsonProperty] public NameDisplayStyle NameStyle { get; set; }

    [JsonProperty] public bool DisplayAsMonster { get; set; }

    [JsonProperty] public ushort MonsterSprite { get; set; }

    [JsonProperty] public ushort HairStyle { get; set; }

    [JsonProperty] public byte HairColor { get; set; }

    #endregion

    #region User

    // Some structs helping us to define various metadata 
    public AuthInfo AuthInfo => Game.World.WorldData.GetOrCreateByGuid<AuthInfo>(Guid, Name);

    [JsonProperty] public SkillBook SkillBook { get; private set; }

    [JsonProperty] public SpellBook SpellBook { get; private set; }

    [JsonProperty] public bool Grouping { get; set; }

    public UserStatus GroupStatus { get; set; }

    [JsonProperty] public byte[] PortraitData { get; set; }

    [JsonProperty] public string ProfileText { get; set; }

    public Castable PendingLearnableCastable { get; private set; }
    public ItemObject PendingSendableParcel { get; private set; }
    public uint PendingSendableQuantity { get; private set; }
    public string PendingParcelRecipient { get; private set; }
    public string PendingBuyableItem { get; private set; }
    public int PendingBuyableQuantity { get; private set; }
    public byte PendingSellableSlot { get; private set; }
    public uint PendingSellableQuantity { get; private set; }
    public uint PendingMerchantOffer { get; private set; }
    public byte PendingDepositSlot { get; private set; }
    public string PendingWithdrawItem { get; private set; }
    public byte PendingRepairSlot { get; private set; }
    public uint PendingRepairCost { get; private set; }

    [JsonProperty] public List<KillRecord> RecentKills { get; private set; }

    public List<SpokenEvent> MessagesReceived { get; private set; }

    [JsonProperty] public Guid GuildGuid { get; set; } = Guid.Empty;

    public List<string> UseCastRestrictions => _currentStatuses.Select(selector: e => e.Value.UseCastRestrictions)
        .Where(predicate: e => e != string.Empty).ToList();

    public List<string> ReceiveCastRestrictions => _currentStatuses
        .Select(selector: e => e.Value.ReceiveCastRestrictions)
        .Where(predicate: e => e != string.Empty).ToList();

    private Nation _nation;

    public Nation Nation
    {
        get => _nation ?? World.DefaultNation;
        set
        {
            _nation = value;
            Citizenship = value.Name;
        }
    }

    [JsonProperty] private string Citizenship { get; set; }

    public string NationName => Nation != null ? Nation.Name : string.Empty;

    [JsonProperty] public Legend Legend;
    [JsonProperty] public string Title;

    public AsyncDialogSession ActiveDialogSession { get; set; }
    public DialogState DialogState { get; set; }

    // Used by reactors and certain other objects to set an associate, so that functions called
    // from Lua later know who to "consult" for dialogs / etc.
    public IInteractable LastAssociate { get; set; }

    public Exchange ActiveExchange { get; set; }

    public bool IsAvailableForExchange => Condition.NoFlags;

    public ManufactureState ManufactureState { get; set; }

    #endregion
}