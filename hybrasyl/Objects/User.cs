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
// (C) 2020-2023 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using DALib.Networking.Packets.Server;
using Hybrasyl.Casting;
using Hybrasyl.Extensions;
using Hybrasyl.Extensions.Utility;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Attributes;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking;
using Hybrasyl.Subsystems.Messaging;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Subsystems.Formulas;
using Hybrasyl.Subsystems.Manufacturing;
using Hybrasyl.Subsystems.Players;
using Hybrasyl.Subsystems.Players.Grouping;
using Hybrasyl.Subsystems.Players.Guilds;
using Hybrasyl.Subsystems.Statuses;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Book = Hybrasyl.Casting.Book;
using Equipment = Hybrasyl.Subsystems.Players.Equipment;
using MessageType = Hybrasyl.Internals.Enums.MessageType;
using SpellUseType = Hybrasyl.Xml.Objects.SpellUseType;
using WireDoor = DALib.Networking.Packets.Server.Door;

namespace Hybrasyl.Objects;

public class KillRecord
{
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

[Persistable]
public class User : Creature
{
    private object _serializeLock = new();

    // Runtime connection back-reference; null between connects/after disconnect. All uses guard.
    private IClient? Client;

    [Persist] public uint LevelPoints;

    public User()
    {
        _initializeUser();
        LastAssociate = null;
    }

    public User(Guid serverGuid, long connectionId, string playername = "")
    {
        ServerGuid = serverGuid;
        if (GlobalConnectionManifest.ConnectedClients.TryGetValue(connectionId, out var client)) Client = client;
        _initializeUser(playername);
    }

    public User(Guid serverGuid, Client client, string playername = "")
    {
        ServerGuid = serverGuid;
        Client = client;
        _initializeUser(playername);
    }

    public string RemoteAddress => Client?.RemoteAddress ?? "unknown";

    public string StorageKey => string.Concat(GetType().Name, ':', Name.ToLower());

    public GuidReference GuidReference => Game.World.WorldState.GetGuidReference(this);

    [Persist] public Guid AccountGuid { get; set; } = Guid.Empty;
    public bool Connected => Client?.Connected ?? false;
    public long ConnectionId => Client?.ConnectionId ?? PreviousConnectionId;
    public long PreviousConnectionId { get; set; }

    [Persist] public Gender Gender { get; set; }

    [Persist] public Class Class { get; set; }

    [Persist] public Class PreviousClass { get; set; }

    [Persist] public bool IsMaster { get; set; }

    public string? AdHocScript { get; set; }
    public UserGroup? Group { get; set; }
    public GroupRecruit? GroupRecruit { get; set; }

    [Persist] private List<StatusSnapshot> Statuses { get; set; } = new();

    public int LevelCircle
    {
        get
        {
            if (Stats.Level < Game.ActiveConfiguration.Constants.LevelCircle1) return 0;
            if (Stats.Level < Game.ActiveConfiguration.Constants.LevelCircle2) return 1;
            if (Stats.Level < Game.ActiveConfiguration.Constants.LevelCircle3) return 2;
            if (Stats.Level < Game.ActiveConfiguration.Constants.LevelCircle4) return 3;
            return 4;
        }
    }

    public Mailbox Mailbox => Game.World.WorldState.GetOrCreateByGuid<Mailbox>(Guid, Name);
    public SentMail SentMailbox => Game.World.WorldState.GetOrCreateByGuid<SentMail>(Guid, Name);

    public Vault Vault =>
        Game.World.WorldState.GetOrCreateByGuid<Vault>(AccountGuid == Guid.Empty ? Guid : AccountGuid);

    public ParcelStore ParcelStore => Game.World.WorldState.GetOrCreateByGuid<ParcelStore>(Guid, Name);

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
            if (Stats.Level == Game.ActiveConfiguration.Constants.PlayerMaxLevel)
                return 0;

            var levelExp = (uint) FormulaParser.Eval(Game.ActiveConfiguration.Formulas.XpToNextLevel, new FormulaEvaluation
            {
                Source = this,
                User = this,
            });

            if (Stats.Experience >= levelExp)
                return 0;

            return levelExp - Stats.Experience;
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
    public string LastSaid { get; set; } = string.Empty;
    public int NumSaidRepeated { get; set; }

    // Throttling checks for messaging
    public DateTime LastBoardMessageSent { get; set; }
    public string? LastBoardMessageTarget { get; set; }
    public DateTime LastMailboxMessageSent { get; set; }
    public string? LastMailboxRecipient { get; set; }
    public Dictionary<string, bool> Flags { get; private set; } = new();

    public bool CollisionsDisabled => Flags.ContainsKey("disablecollisions") ? Flags["disablecollisions"] : false;

    public DateTime LastAttack { get; set; }

    public bool Grouped => Group != null;

    [Persist] public Dictionary<byte, bool> ClientSettings { get; set; } = new();


    [Persist] public bool IsMuted { get; set; }

    [Persist] public bool IsIgnoringWhispers { get; set; }

    [Persist]
    public bool IsAtWorldMap
    {
        get => Location.WorldMap;
        set => Location.WorldMap = value;
    }

    /// <summary>The world map the user is currently viewing (transient; not persisted). Gates 0x3F
    /// click handling so a forged click can only teleport to a destination this map actually offers.</summary>
    internal WorldMap? ActiveWorldMap { get; set; }

    public string GroupText =>
        // This also eventually needs to consider marriages
        Grouping ? "Grouped!" : "Adventuring Alone";

    /**
         * Returns the current weight as perceived by the client. The actual inventory or equipment
         * weight may be less than zero, but this method will never return a negative value (negative
         * values will appear as zero as the client expects).
         */

    public ushort VisibleWeight => (ushort)Math.Max(0, CurrentWeight);

    /**
         * Returns the true weight of the user's inventory + equipment, which could be negative.
         * Note that you should use VisibleWeight when communicating with the client since negative
         * weights should be invisible to users.
         */
    public int CurrentWeight => Inventory.Weight + Equipment.Weight;

    public ushort MaximumWeight => (ushort) FormulaParser.Eval(Game.ActiveConfiguration.Formulas.AllowedCarryWeight,
        new FormulaEvaluation
        {
            Source = this,
            User = this
        });

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
        // Null/empty = citizenship never chosen; a distinguishable state that must not
        // be collapsed into the default nation on save
        if (!string.IsNullOrEmpty(Citizenship))
        {
            Nation theNation;
            Nation = World.WorldData.TryGetValue(Citizenship, out theNation) ? theNation : World.DefaultNation;
        }
    }

    public override void Say(string message, string from = "")
    {
        if (Location.Map is { AllowSpeaking: true })
        {
            if (World.WorldState.TryGetSocialEvent(this, out var e) &&
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
        if (Location.Map is { AllowSpeaking: true })
        {
            if (World.WorldState.TryGetSocialEvent(this, out var e) &&
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

    public bool GetClientSetting(string key) => ClientSettings[Game.ActiveConfiguration.GetSettingNumber(key)];

    public bool ToggleClientSetting(string key)
    {
        var num = Game.ActiveConfiguration.GetSettingNumber(key);
        ClientSettings[num] = !ClientSettings[num];
        return ClientSettings[num];
    }

    public bool ToggleClientSetting(byte number)
    {
        ClientSettings[number] = !ClientSettings[number];
        return ClientSettings[number];
    }

    public void Enqueue(DALib.Networking.Wire.IServerPacket packet, bool flush = false, int transmitDelay = 0)
    {
        GameLog.DebugFormat("Sending 0x{0:X2} to {1}", packet.Opcode, Name);
        if (packet is NpcMenuPacket menu) ReportMerchantFormMismatches(menu);
        try
        {
            Client?.Enqueue(packet, flush, transmitDelay);
        }
        catch (ObjectDisposedException)
        {
            GameLog.Warning("User {user}: socket enqueue failed due to disconnect, removing", Name);
            // Forcibly destroy client and remove user from world.
            if (Client is { } client)
            {
                PreviousConnectionId = client.ConnectionId;
                Client = null;
            }
            World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.CleanupUser, CleanupType.ByName,
                Name));
        }
    }

    // Logged rather than thrown: the menu itself is well-formed, and the misparse it predicts is a
    // registration bug the player should not lose their dialog over.
    private void ReportMerchantFormMismatches(NpcMenuPacket menu)
    {
        foreach (var mismatch in MerchantResponseFormCheck.Mismatches(menu, World.MerchantMenuHandlers))
            GameLog.Error("Merchant menu to {User}: {Mismatch}", Name, mismatch);
    }

    /// <summary>
    ///     Send a 0x31 board/mail response, carrying its required transmit delay (the board list
    ///     needs one for the messaging pane to display correctly).
    /// </summary>
    internal void SendBoardResponse(MessagingResponse response) =>
        Enqueue(response.Packet(), transmitDelay: response.TransmitDelay);

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
        if (obj is Creature c && c.Condition.IsInvisible && Condition.SeeInvisible)
            return;
        base.AoiDeparture(obj);
        GameLog.Debug("Removing ItemObject with ID {Id}", obj.Id);
        Enqueue(new RemoveObjectPacket { SourceId = obj.Id });
    }

    public void AoiDeparture(VisibleObject obj, int transmitDelay = 0)
    {
        base.AoiDeparture(obj);
        GameLog.Debug("Removing ItemObject with ID {Id}", obj.Id);
        Enqueue(new RemoveObjectPacket { SourceId = obj.Id }, transmitDelay: transmitDelay);
    }

    /// <summary>
    ///     Send a close dialog packet to the client. This will terminate any open dialog.
    /// </summary>
    public void SendCloseDialog() =>
        // The client returns from the deserializer immediately after the type byte, so a close
        // body is the type byte alone.
        Enqueue(new NpcDialogPacket
        {
            DialogType = NpcDialogType.Close,
            Body = new CloseDialog()
        });

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
        var elapsed = DateTime.Now - status.Start;
        var remaining = status.Duration - elapsed.TotalSeconds;
        // The client's enum has Yellow=3, so Orange/Red/White are 4/5/6.
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
            color = StatusBarColor.None;

        GameLog.DebugFormat(
            "{Name} - status update - sending Icon: {Icon}, Color: {BarColor}",
            Name, status.Icon, color);
        GameLog.DebugFormat(
            "{Name} - status: {Status}, expired: {Expired}, remaining: {Remaining}, duration: {Duration}",
            Name, status.Name, status.Expired, remaining, status.Duration);
        Enqueue(new StatusBarPacket { Icon = status.Icon, Color = color });
    }

    public override void OnHear(SpokenEvent e)
    {
        LastHeard = e;
        if (e.Speaker != this)
            MessagesReceived.Add(e);
        var from = !string.IsNullOrEmpty(e.From) ? e.From : e.Speaker.Name;
        Enqueue(new PublicMessagePacket
        {
            Type = e.Shout ? PublicMessagePacket.TypeShout : PublicMessagePacket.TypeSay,
            SourceId = e.Speaker.Id,
            Message = e.Shout ? $"{from}! {e.Message}" : $"{from}: {e.Message}"
        });
    }

    /// <summary>
    ///     Sadly, all things in this world must come to an end.
    /// </summary>
    public override void OnDeath()
    {
        // we cannot die twice
        if (!Condition.Alive) return;
        var handler = Game.ActiveConfiguration.Handlers?.Death;
        if (!(handler?.Active ?? true))
        {
            SendSystemMessage("Death disabled by server configuration");
            Stats.Hp = 1;
            UpdateAttributes(StatUpdateFlags.Full);
            return;
        }

        // Save the death location immediately: status/script hooks fired during death
        // processing can move or remove us, and the death pile must land where we died.
        if (Location.Map is { } diedOn)
        {
            Location.DeathMap = diedOn;
            Location.DeathMapX = X;
            Location.DeathMapY = Y;
        }

        // Drops land on the current map, or the death-time snapshot if we were removed
        // mid-processing. Both null means there is nowhere for the pile to go, and
        // processing death anyway would destroy it.
        if ((Location.Map ?? Location.DeathMap) is not { } map)
        {
            GameLog.UserActivityFatal("{Name}: OnDeath with no map or death map, death not processed", Name);
            return;
        }

        GameLog.UserActivityInfo(
            "{Name}: died on {Map} last hit by {LastHitter} on map {LastHitterMap}",
            Name, Location.MapName, LastHitter?.Name ?? "unknown",
            LastHitter?.Location.MapName ?? "Unknown");
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
            if (Inventory[i] is not { } item) continue;
            if (item.Bound)
                continue;
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
            map.AddItem(X, Y, item);
        }

        // Now process equipment
        for (byte i = 1; i <= Equipment.Size; i++)
        {
            var item = Equipment[i];
            if (item == null)
                continue;
            if (item.Bound)
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
            map.AddItem(X, Y, item);
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
            map.AddGold(X, Y, newGold);
            Stats.Gold = 0;
        }

        // Experience penalty
        if (handler?.Penalty != null)
        {
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

                if (handler.Penalty.Hp.Contains('.'))
                    hpPenalty = (uint)Math.Ceiling(Stats.BaseHp * Convert.ToDouble(handler.Penalty.Hp));
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

        SendSystemMessage("Your items are ripped from your body.");

        if (Game.ActiveConfiguration.Handlers?.Death?.Map != null)
        {
            Teleport(Game.ActiveConfiguration.Handlers.Death.Map.Value,
                Game.ActiveConfiguration.Handlers.Death.Map.X,
                Game.ActiveConfiguration.Handlers.Death.Map.Y);
            // Location.Map is non-null here: the player is on a map during death handling (and was
            // just Teleported above).
            if (Location.Map!.Name != Game.ActiveConfiguration.Handlers.Death.Map.Value)
                GameLog.UserActivityFatal("{Name}: died, but not on death map..?", Name);
        }
        else
        {
            GameLog.Warning("Death handler not found: {Name} not removed from {Map}", Name, Location.Map!.Name);
        }

        if (Game.ActiveConfiguration.Handlers?.Death?.GroupNotify ?? true)
            Group?.SendMessage($"{Name} has died!");
    }


    /// <summary>
    ///     End a user's coma status (skulling).
    /// </summary>
    public void EndComa()
    {
        if (!Condition.Comatose) return;
        Condition.Comatose = false;
        var handler = Game.ActiveConfiguration.Handlers?.Death;
        if (handler?.Coma != null && Game.World.WorldData.TryGetValue(handler.Coma.Value, out Status status))
            RemoveStatus(status.Icon);
    }

    /// <summary>
    ///     Resurrect a player, optionally, instantly returning them to their point of death.
    /// </summary>
    /// <param name="recall">If true, resurrect at exact point of death.</param>
    public void Resurrect(bool recall = false)
    {
        var handler = Game.ActiveConfiguration.Handlers?.Death;
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

        // Handlers?.Death is null when no Death config exists; then no legend mark is added on resurrect.
        if (handler?.LegendMark != null)
        {
            if (Legend.TryGetMark(handler.LegendMark.Prefix, out var deathMark) && handler.LegendMark.Increment)
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
        PortraitData = [];
        ProfileText = string.Empty;
        DialogState = new DialogState(this);
        ClientSettings = new Dictionary<byte, bool>();
        Group = null;
        Flags = new Dictionary<string, bool>();
        CurrentStatuses = new ConcurrentDictionary<ushort, ICreatureStatus>();
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

        var group = Group ??= new UserGroup(this);
        return group.Add(invitee);
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
                    exp = (uint)Math.Ceiling(exp * 0.40);
                    break;
                case 4:
                    exp = (uint)Math.Ceiling(exp * 0.80);
                    break;
                case -6:
                    exp = (uint)Math.Ceiling(exp * 1.15);
                    break;
                case -5:
                    exp = (uint)Math.Ceiling(exp * 1.10);
                    break;
                case -4:
                    exp = (uint)Math.Ceiling(exp * 1.05);
                    break;
                case < -7:
                    exp = (uint)Math.Ceiling(exp * 1.20);
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
                gold -= (uint)(gold * (Stats.ExtraXp / 100) * -1);
                break;
            case > 0:
                gold += (uint)(gold * (Stats.ExtraXp / 100));
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

        if (Stats.Level == Game.ActiveConfiguration.Constants.PlayerMaxLevel || exp < ExpToLevel)
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

                    var bonusHpGain = (int) FormulaParser.Eval(Game.ActiveConfiguration.Formulas.HpGainPerLevel,
                        new FormulaEvaluation
                        {
                            Source = this,
                            User = this
                        });
                    var bonusMpGain = (int) FormulaParser.Eval(Game.ActiveConfiguration.Formulas.MpGainPerLevel,
                        new FormulaEvaluation
                        {
                            Source = this,
                            User = this
                        });

                    Stats.BaseHp += bonusHpGain;
                    Stats.BaseMp += bonusMpGain;

                    GameLog.UserActivityInfo(
                        "User {Name}: level increased to {Level}, CON {Con}, WIS {Wis}: HP {BonusHp} MP {BonusMp}",
                        Name, Stats.Level, Stats.Con, Stats.Wis, bonusHpGain, bonusMpGain);
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
        // Update ur mom accor

        UpdateAttributes(StatUpdateFlags.Experience);
    }

    public void TakeExperience(uint exp) { }

    public bool AssociateConnection(Guid serverGuid, long connectionId)
    {
        ServerGuid = serverGuid;
        if (!GlobalConnectionManifest.ConnectedClients.TryGetValue(connectionId, out var client)) return false;
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
            case (byte)ItemSlots.Necklace:
                Stats.BaseOffensiveElement = toApply.Element;
                break;
            case (byte)ItemSlots.Waist:
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
            case (byte)ItemSlots.Necklace:
                Stats.BaseOffensiveElement = ElementType.None;
                break;
            case (byte)ItemSlots.Waist:
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


        // The equipment block is the fixed 18-slot profile *display* order, in which Accessory1
        // precedes Boots (rung-1: darkages-741 052-0x34, client mapping 0..11, 13, 12, 14..17).
        // GetEquipmentDisplayList already emits the wire order — it achieves the same swap by
        // writing FirstAcc's item at the Foot position and vice versa — so zip it onto DALib's
        // canonical order rather than re-deriving the mapping.
        var displayList = Equipment.GetEquipmentDisplayList();
        var equipment = DALib.Networking.Packets.Server.ProfilePacket.EquipmentDisplayOrder
            .Select((slot, i) => new DALib.Networking.Packets.Server.ProfileEquipmentSlot(
                slot, displayList[i].Item1, displayList[i].Item2))
            .ToList();

        // TakeLast, not Take: keep the newest marks if a legend ever exceeds the wire u8 cap.
        // Defensive only — see the note in SendProfile.
        var publicMarks = Legend.Where(predicate: mark => mark.Public)
            .TakeLast(DALib.Networking.Packets.Server.ProfilePacket.MaxLegendMarks)
            .Select(mark => new DALib.Networking.Packets.Server.LegendMark
            {
                Icon = (byte)mark.Icon,
                Color = (byte)mark.Color,
                Prefix = mark.Prefix ?? string.Empty,
                Text = mark.ToString()
            })
            .ToList();

        invoker.Enqueue(new DALib.Networking.Packets.Server.ProfilePacket
        {
            Id = Id,
            Equipment = equipment,
            SocialStatus = (DALib.Networking.Packets.Server.SocialStatus)(byte)GroupStatus,
            Name = Name,
            NationFlag = Nation.Flag, // This should pull from town / nation
            Title = string.Empty,
            GroupOpen = Grouping,
            GuildRank = guildInfo.GuildRank,
            ClassName = Game.ActiveConfiguration.GetClassName((byte)Class),
            GuildName = guildInfo.GuildName,
            Legend = publicMarks,
            Portrait = PortraitData,
            ProfileText = ProfileText
        });
    }

    private (string GuildName, string GuildRank) GetGuildInfo()
    {
        var guild = World.WorldState.Get<Guild>(GuildGuid);
        return guild?.GetUserDetails(GuildGuid) ?? ("", "");
    }

    public void Save(bool serializeStatus = false)
    {
        lock (_serializeLock)
        {
            var cache = World.DatastoreConnection.GetDatabase();
            if (serializeStatus)
            {
                if (ActiveStatusCount > 0)
                    Statuses = CurrentStatuses.Count > 0
                        ? CurrentStatuses.Values.Select(selector: e => e.Snapshot).ToList()
                        : new List<StatusSnapshot>();
                else
                    Statuses.Clear();
            }

            AuthInfo.Save();
            Mailbox.Save();
            SentMailbox.Save();
            cache.Set(GetStorageKey(Name), this);
        }
    }

    public void SendLightLevel()
    {
        var time = new HybrasylTime(DateTime.Now);
        Enqueue(new LightLevelPacket { LightLevel = (byte)time.Hour });
    }

    public override void SendMapInfo(int transmitDelay = 0)
    {
        if (Location.Map is not { } map) return;
        // I also hate this
        byte flags = 0;
        if (map.Flags.HasFlag(MapFlags.Snow))
            flags |= 1;
        if (map.Flags.HasFlag(MapFlags.Rain))
            flags |= 2;
        if (map.Flags.HasFlag(MapFlags.Dark)) {
            flags |= 1;
            flags |= 2;
        }
        if (map.Flags.HasFlag(MapFlags.NoMap))
            flags |= 64;
        if (map.Flags.HasFlag(MapFlags.Snow))
            flags |= 128;
        Enqueue(new MapInfoPacket
        {
            MapId = map.Id,
            Width = map.X,
            Height = map.Y,
            Flags = flags,
            // Crc16.Calculate returns byteswapped CCITT; DALib writes big-endian true CCITT,
            // so swap back — wire bytes stay identical.
            Checksum = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(map.Checksum),
            Name = map.Name
        }, transmitDelay: transmitDelay);
        if (map.Music != 0xFF) SendMusic(map.Music);
        if (!string.IsNullOrEmpty(map.Message)) SendMessage(map.Message, 18);
    }

    public override void SendLocation(int transmitDelay = 0)
    {
        Enqueue(new LocationPacket { X = X, Y = Y, Unknown1 = 0, Unknown2 = 0 },
            transmitDelay: transmitDelay);

        var doors = GetDoorsCoordsInView(GetViewport());

        if (doors.Count <= 0 || Location.Map is not { } map) return;

        //skip static side panels of center-only 3-tile doors — they don't toggle and sending an update for them
        //would make the client swap an irrelevant sprite that retail places freely alongside the actual door.
        foreach (var door in doors)
        {
            var panel = map.Doors[door];
            if (!panel.Toggles) continue;
            SendDoorUpdate(door.Item1, door.Item2, panel.Closed, panel.IsLeftRight);
        }
    }

    public List<(byte X, byte Y)> GetDoorsCoordsInView(Rectangle viewPort)
    {
        var ret = new List<(byte X, byte Y)>();
        if (Location.Map is not { } map) return ret;

        for (var x = viewPort.X; x < viewPort.X + viewPort.Width; x++)
            for (var y = viewPort.Y; y < viewPort.Y + viewPort.Height; y++)
            {
                var coords = ((byte)x, (byte)y);
                ;
                if (map.Doors.ContainsKey(coords)) ret.Add(coords);
            }

        return ret;
    }

    public void SendRefresh() => Enqueue(new RefreshPacket(), transmitDelay: 100);

    public void DisplayIncomingWhisper(string charname, string message)
    {
        Client?.SendMessage($"{charname}\" {message}", 0x0);
    }

    public void DisplayOutgoingWhisper(string charname, string message)
    {
        Client?.SendMessage($"{charname}> {message}", 0x0);
    }

    public bool CanTalkTo(User target, out string msg)
    {
        msg = string.Empty;
        // First, make sure a) we can send a message and b) the target is not ignoring whispers.
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
            Client?.SendMessage(err, 0x0);
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
                    member.Client?.SendMessage($"[!{Name}] {message}", MessageTypes.GROUP);
                else
                    Client?.SendMessage(err, 0x0);
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
        Enqueue(new DrawObjectsPacket
        {
            Objects =
            [
                new ItemWorldObject
                {
                    X = gold.X,
                    Y = gold.Y,
                    Id = gold.Id,
                    Sprite = (ushort)(gold.Sprite + 0x8000)
                }
            ]
        });
    }

    internal void UseSkill(byte slot)
    {
        if (Location.Map is not { } map) return;
        if (!map.AllowCasting)
            if (!AuthInfo.IsPrivileged)
            {
                SendSystemMessage("You can't use that here.");
                return;
            }

        if (SkillBook[slot] is not { } bookSlot)
        {
            GameLog.UserActivityWarning("{Name}: UseSkill: no skill in slot {Slot}, ignoring", Name, slot);
            return;
        }

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
            if (!bookSlot.Castable.IsAssail && bookSlot.Castable.Effects?.Sound?.Id != null)
                PlaySound(bookSlot.Castable.Effects.Sound.Id);
        }

        if (UseCastable(bookSlot.Castable))
        {
            if (bookSlot.UseCount != uint.MaxValue)
                bookSlot.UseCount += 1;
            if (bookSlot.UseCount <= bookSlot.Castable.Mastery.Uses)
                SendSkillUpdate(bookSlot, slot);

            bookSlot.Castable.LastCast = DateTime.Now;
            Client?.Enqueue(new CooldownPacket
            {
                IsSkill = true,
                Slot = slot,
                Seconds = (uint)bookSlot.Castable.Cooldown
            });
        }
    }

    internal void UseSpell(byte slot, uint target = 0)
    {
        if (Location.Map is not { } map) return;
        if (!map.AllowCasting)
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

        if (SpellBook[slot] is not { } bookSlot)
        {
            GameLog.UserActivityWarning("{Name}: UseSpell: no spell in slot {Slot}, ignoring", Name, slot);
            return;
        }

        var targetCreature = map.EntityTree.OfType<Creature>().SingleOrDefault(predicate: x => x.Id == target) ?? null;

        if (bookSlot.OnCooldown)
        {
            SendSystemMessage("You must wait longer to use that.");
            return;
        }

        if (bookSlot.Castable.Intents[0].UseType == SpellUseType.Target)
        {
            if (targetCreature == null || targetCreature.Location.Map != Location.Map)
                return;

            if (Distance(targetCreature) > Game.ActiveConfiguration.Constants.PlayerMaxCastDistance)
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
        if (bookSlot.UseCount != uint.MaxValue)
            bookSlot.UseCount += 1;
        if (bookSlot.UseCount <= bookSlot.Castable.Mastery.Uses)
            SendSpellUpdate(bookSlot, slot);
        if (bookSlot.Castable.Cooldown > 0)
            Client?.Enqueue(new CooldownPacket
            {
                IsSkill = false,
                Slot = slot,
                Seconds = (uint)bookSlot.Castable.Cooldown
            });
        bookSlot.LastCast = DateTime.Now;
    }

    /// <summary>
    ///     Process the casting cost for a castable. If all requirements were not met, return false.
    /// </summary>
    /// <param name="castable">The castable that is being cast.</param>
    /// <returns>True or false depending on success.</returns>
    public bool ProcessCastingCost(Castable castable, Creature? target, out string message)
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

        if (Condition.IsMpDecreaseProhibited)
            cost.Mp = 0;

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
        if ((int)cost.Gold > 0) RemoveGold(new Gold(cost.Gold));
        cost.Items?.ForEach(action: itemReq => RemoveItem(itemReq.Item, itemReq.Quantity));

        UpdateAttributes(StatUpdateFlags.Current);
        return true;
    }

    public void SendVisibleItem(ItemObject itemObject)
    {
        GameLog.DebugFormat("Sending add visible ItemObject packet");
        Enqueue(new DrawObjectsPacket
        {
            Objects =
            [
                new ItemWorldObject
                {
                    X = itemObject.X,
                    Y = itemObject.Y,
                    Id = itemObject.Id,
                    Sprite = (ushort)(itemObject.Sprite + 0x8000),
                    Color = itemObject.Color
                }
            ]
        });
    }

    public void SendVisibleCreature(Creature creature)
    {
        GameLog.DebugFormat("Sending add visible creature packet");
        Enqueue(new DrawObjectsPacket
        {
            Objects =
            [
                new CreatureWorldObject
                {
                    X = creature.X,
                    Y = creature.Y,
                    Id = creature.Id,
                    Sprite = (ushort)(creature.Sprite + 0x4000),
                    Direction = (byte)creature.Direction,
                    Type = creature is Merchant ? CreatureWorldObject.TypeNamed : (byte)0,
                    Name = creature is Merchant ? creature.Name : string.Empty
                }
            ]
        });
    }

    public void SetHairstyle(ushort hairStyle)
    {
        HairStyle = hairStyle;
        SendUpdateToUser();

        if (Location.Map is not { } map) return;
        foreach (var obj in map.EntityTree.GetObjects(GetViewport()))
        {
            obj.AoiEntry(this);
            AoiEntry(obj);
        }
    }

    public void SetHairColor(ItemColor itemColor)
    {
        HairColor = (byte)itemColor;
        SendUpdateToUser();

        if (Location.Map is not { } map) return;
        foreach (var obj in map.EntityTree.GetObjects(GetViewport()))
        {
            obj.AoiEntry(this);
            AoiEntry(obj);
        }
    }

    public void SendUpdateToUser(IClient? client = null)
    {
        var offset = Equipment.Armor?.BodyStyle ?? 0;
        if (!Condition.Alive)
            offset += 0x20;
        else if (Condition.IsInvisible)
            offset += 0x40;


        GameLog.Debug("Offset is: {Offset}", offset.ToString("X"));
        // Figure out what we're sending as the "helmet"
        var helmet = Equipment.Helmet?.DisplaySprite ?? HairStyle;
        helmet = Equipment.DisplayHelm?.DisplaySprite ?? helmet;
        var helmcolor = Equipment.DisplayHelm?.Color ?? 0;
        var color = helmcolor == 0 ? HairColor : helmcolor;
        // Why is this so difficult?
        var bootSprite = Equipment.Armor?.HideBoots ?? false ? 0 : Equipment.Boots?.DisplaySprite ?? 0;
        var bootsColor = Equipment.Boots?.Color ?? 0;
        var armor = Equipment.Armor?.DisplaySprite ?? 0;

        // The appearance block is discriminated on its leading u16: 0xFFFF selects the
        // creature-sprite form, anything else is the head sprite of the equipment form.
        DisplayUserAppearance appearance = DisplayAsMonster
            ? new CreatureSpriteAppearance
            {
                // The client subtracts 0x4000 and loads mns%03d.mpf, so the sprite goes on
                // the wire tagged — same convention as the 0x07 creature form above.
                Sprite = (ushort)(MonsterSprite + 0x4000),
                HeadColor = color,
                BootsColor = bootsColor
            }
            : new EquipmentAppearance
            {
                HeadSprite = helmet,
                BodySprite = (byte)((byte)Gender * 16 + offset),
                // Both armor fields carry the same sprite on purpose: they are two
                // depth-distinct body passes (layers 7 and 5), not a duplicate.
                ArmorSprite1 = armor,
                BootsSprite = (byte)bootSprite,
                ArmorSprite2 = armor,
                ShieldSprite = (byte)(Equipment.Shield?.DisplaySprite ?? 0),
                WeaponSprite = Equipment.Weapon?.DisplaySprite ?? 0,
                HeadColor = color,
                BootsColor = bootsColor,
                AccessoryColor1 = Equipment.FirstAcc?.Color ?? 0,
                AccessorySprite1 = Equipment.FirstAcc?.DisplaySprite ?? 0,
                AccessoryColor2 = Equipment.SecondAcc?.Color ?? 0,
                AccessorySprite2 = Equipment.SecondAcc?.DisplaySprite ?? 0,
                AccessoryColor3 = Equipment.ThirdAcc?.Color ?? 0,
                AccessorySprite3 = Equipment.ThirdAcc?.DisplaySprite ?? 0,
                LanternSize = (byte)LanternSize,
                RestPosition = (byte)RestPosition,
                OvercoatSprite = Equipment.Overcoat?.DisplaySprite ?? 0,
                OvercoatColor = Equipment.Overcoat?.Color ?? 0,
                BodyColor = (byte)SkinColor,
                // Client-side this is the translucency flag, not a visibility toggle:
                // a player hidden from this viewer is sent with zeroed appearance instead.
                IsHidden = Condition.IsInvisible,
                FaceSprite = FaceShape
            };

        (client ?? Client)?.Enqueue(new DisplayUserPacket
        {
            X = X,
            Y = Y,
            Direction = (DALib.Enums.Direction)(byte)Direction,
            Id = Id,
            Appearance = appearance,
            NameTagStyle = (byte)NameStyle,
            Name = Name,
            GroupName = GroupRecruit?.Name ?? string.Empty
        });
    }

    // DALib's default Padding is the same two inert zero bytes the legacy site wrote.
    public void RequestPortrait() => Enqueue(new RequestPortraitPacket());

    public override void SendId() =>
        // The client's parser stops at Gender; nothing may follow it.
        Enqueue(new UserAppearancePacket
        {
            Id = Id,
            Direction = (byte)Direction,
            Class = (byte)Class,
            Gender = (DALib.Enums.Gender)(byte)Gender
        });

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

        Enqueue(new AddEquipmentPacket
        {
            Slot = (DALib.Enums.EquipmentSlot)(byte)slot,
            Sprite = (ushort)(itemObject.Sprite + 0x8000),
            Color = itemObject.Color,
            Name = itemObject.Name,
            MaxDurability = itemObject.MaximumDurability,
            CurrentDurability = itemObject.DisplayDurability
        });
        SendSystemMessage(itemObject.EquipmentSlot == (byte)EquipmentSlot.Weapon
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
        Enqueue(new RemoveItemPacket { Slot = (byte)slot });
    }

    public void SendClearSkill(int slot)
    {
        Enqueue(new RemoveSkillPacket { Slot = (byte)slot });
    }

    public void SendClearSpell(int slot)
    {
        Enqueue(new RemoveSpellPacket { Slot = (byte)slot });
    }

    /// <summary>
    ///     Send an ItemObject update packet (essentially placing the ItemObject in a given slot, as far as the client is
    ///     concerned.
    /// </summary>
    /// <param name="itemObject">The ItemObject we are sending to the user.</param>
    /// <param name="slot">The client's ItemObject slot.</param>
    public void SendItemUpdate(ItemObject? itemObject, int slot)
    {
        if (itemObject == null)
        {
            SendClearItem(slot);
            return;
        }

        GameLog.DebugFormat("Adding {0} qty {1} to slot {2}",
            itemObject.Name, itemObject.Count, slot);
        Enqueue(new AddItemPacket
        {
            Slot = (byte)slot,
            Sprite = (ushort)(itemObject.Sprite + 0x8000),
            Color = itemObject.Color,
            Name = itemObject.Name,
            Count = (uint)itemObject.Count,
            Stackable = itemObject.Stackable,
            MaxDurability = itemObject.MaximumDurability,
            CurrentDurability = itemObject.DisplayDurability
        });
    }

    public void SendSkillUpdate(BookSlot? item, int slot)
    {
        if (item == null)
        {
            SendClearSkill(slot);
            return;
        }

        GameLog.DebugFormat("Adding skill {0} to slot {2}",
            item.Castable.Name, slot);

        string name;
        if (item.Castable.Mastery.Uses != 1)
        {
            double percent;
            if (item.UseCount > item.Castable.Mastery.Uses) percent = 100;
            else percent = Math.Floor(item.UseCount / (double)item.Castable.Mastery.Uses * 100);

            name = $"{item.Castable.Name} (Lev:{percent}/100)";
        }
        else
        {
            name = item.Castable.Name;
        }

        Enqueue(new AddSkillPacket { Slot = (byte)slot, Icon = item.Castable.Icon, Name = name });
    }

    public void SendCooldown(BookSlot item, bool clear = false)
    {
        if (item == null) return;
        var slot = item.Castable.IsSkill
            ? SkillBook.IndexOf(item.Castable.Name)
            : SpellBook.IndexOf(item.Castable.Name);

        if (slot == -1) return;

        // Pre-existing: the slot is looked up in the skill *or* spell book above, but IsSkill is
        // hardcoded, so a spell sweeps the skill pane at the spell's index. Fixing it changes the
        // emitted bytes, which is why the conversion left it alone. HS-1591.
        Client?.Enqueue(new CooldownPacket
        {
            IsSkill = true,
            Slot = (byte)(slot + 1),
            Seconds = (uint)(clear ? 1 : item.Castable.Cooldown)
        });
    }

    public void SendSpellUpdate(BookSlot? item, int slot)
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
            else percent = Math.Floor(item.UseCount / (double)item.Castable.Mastery.Uses * 100);

            name = $"{item.Castable.Name} (Lev:{percent}/100)";
        }
        else
        {
            name = item.Castable.Name;
        }

        Enqueue(new AddSpellPacket
        {
            Slot = (byte)slot,
            Icon = item.Castable.Icon,
            // value-cast: Hybrasyl.Xml and DALib SpellUseType share byte layout (0-7)
            UseType = (DALib.Networking.Packets.Server.SpellUseType)(byte)item.Castable.Intents[0].UseType,
            Name = name,
            Prompt = "\0", // preserved: DALib default is empty; dropping it shifts CastLines
            CastLines = (byte)CalculateLines(item.Castable)
        });
    }

    private int CalculateLines(Castable castable)
    {
        try
        {
            // TODO: potentially add additional equipment types. for now only weapons
            if (Equipment.Weapon?.CastModifiers != null)
            {
                object? modifier = null;
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
        if (UnreadMail || HasParcels) flags |= StatUpdateFlags.UnreadMail;

        if (CollisionsDisabled)
            flags |= StatUpdateFlags.GameMasterA;

        // DALib re-derives the flag byte from populated sections + standalone bits; populate each
        // section iff its flag bit is set (matching the legacy guards) so the derived flag byte
        // equals (byte)flags. GameMasterA/B (flag bits 6-7) carry through DALib's 2-bit MovementMode.
        var x08 = new AttributesPacket
        {
            UnreadMail = flags.HasFlag(StatUpdateFlags.UnreadMail),
            ReservedFlag = flags.HasFlag(StatUpdateFlags.Unknown),
            MovementMode = (byte)(((byte)flags & 0xC0) >> 6)
        };

        if (flags.HasFlag(StatUpdateFlags.Primary))
            x08.Primary = new PrimaryAttributes
            {
                Level = Stats.Level,
                Ability = Stats.Ability,
                MaxHp = Stats.MaximumHp,
                MaxMp = Stats.MaximumMp,
                Str = Stats.Str,
                Int = Stats.Int,
                Wis = Stats.Wis,
                Con = Stats.Con,
                Dex = Stats.Dex,
                UnspentPoints = (byte)LevelPoints,
                MaxWeight = MaximumWeight,
                CurrentWeight = VisibleWeight
            };

        if (flags.HasFlag(StatUpdateFlags.Current))
            x08.Current = new CurrentAttributes { Hp = Stats.Hp, Mp = Stats.Mp };

        if (flags.HasFlag(StatUpdateFlags.Experience))
            x08.Experience = new ExperienceAttributes
            {
                Experience = Stats.Experience,
                ExpToLevel = ExpToLevel,
                AbilityExp = Stats.AbilityExp,
                Gold = Gold
            };

        if (flags.HasFlag(StatUpdateFlags.Secondary))
            x08.Secondary = new SecondaryAttributes
            {
                Blinded = (byte)(Condition.Blinded ? 0x08 : 0x00),
                MailStatus = (byte)MailStatus,
                OffensiveElement = (byte)Stats.BaseOffensiveElement,
                DefensiveElement = (byte)Stats.BaseDefensiveElement,
                MrRating = Stats.MrRating,
                Ac = Stats.Ac,
                DmgRating = Stats.DmgRating,
                HitRating = Stats.HitRating
            };

        Enqueue(x08);
    }

    public int GetCastableMaxLevel(Castable castable) => IsMaster ? 100 : castable.GetMaxLevelByClass(Class);


    public User? GetFacingUser()
    {
        if (Location.Map is not { } map) return null;
        List<VisibleObject> contents;

        switch (Direction)
        {
            case Direction.North:
                contents = map.GetTileContents(X, Y - 1);
                break;
            case Direction.South:
                contents = map.GetTileContents(X, Y + 1);
                break;
            case Direction.West:
                contents = map.GetTileContents(X - 1, Y);
                break;
            case Direction.East:
                contents = map.GetTileContents(X + 1, Y);
                break;
            default:
                contents = new List<VisibleObject>();
                break;
        }

        return contents.FirstOrDefault(predicate: y => y is User) as User;
    }

    /// <summary>
    ///     Returns all the objects that are directly facing the user.
    /// </summary>
    /// <returns>A list of visible objects.</returns>
    public List<VisibleObject> GetFacingObjects(int distance = 1)
    {
        var contents = new List<VisibleObject>();
        if (Location.Map is not { } map) return contents;

        switch (Direction)
        {
            case Direction.North:
                {
                    for (var i = 1; i <= distance; i++) contents.AddRange(map.GetTileContents(X, Y - i));
                }
                break;
            case Direction.South:
                {
                    for (var i = 1; i <= distance; i++) contents.AddRange(map.GetTileContents(X, Y + i));
                }
                break;
            case Direction.West:
                {
                    for (var i = 1; i <= distance; i++) contents.AddRange(map.GetTileContents(X - i, Y));
                }
                break;
            case Direction.East:
                {
                    for (var i = 1; i <= distance; i++) contents.AddRange(map.GetTileContents(X + i, Y));
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
        if (Location.Map is not { } map) return false;
        int oldX = X, oldY = Y, newX = X, newY = Y;
        var arrivingViewport = Rectangle.Empty;
        var departingViewport = Rectangle.Empty;
        var commonViewport = Rectangle.Empty;
        var halfViewport = Game.ActiveConfiguration.Constants.ViewportSize / 2;

        if (Condition.Disoriented)
        {
            direction = (Direction)Random.Shared.Next(4);
            SendSystemMessage("You stumble around, unable to gather your bearings.");
        }

        if (CurrentWeight > MaximumWeight && Condition.Alive)
        {
            SendSystemMessage("You cannot move, you are overburdened.");
            Refresh();
            return false;
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
                    Game.ActiveConfiguration.Constants.ViewportSize, 1);
                departingViewport = new Rectangle(oldX - halfViewport + 2, oldY + halfViewport - 2,
                    Game.ActiveConfiguration.Constants.ViewportSize, 1);
                break;
            case Direction.South:
                ++newY;
                arrivingViewport = new Rectangle(oldX - halfViewport - 2, oldY + halfViewport - 4,
                    Game.ActiveConfiguration.Constants.ViewportSize, 1);
                departingViewport = new Rectangle(oldX - halfViewport + 2, newY - halfViewport + 2,
                    Game.ActiveConfiguration.Constants.ViewportSize, 1);
                break;
            case Direction.West:
                --newX;
                arrivingViewport = new Rectangle(newX - halfViewport + 4, oldY - halfViewport + 2, 1,
                    Game.ActiveConfiguration.Constants.ViewportSize);
                departingViewport = new Rectangle(oldX + halfViewport - 2, oldY - halfViewport - 2, 1,
                    Game.ActiveConfiguration.Constants.ViewportSize);
                break;
            case Direction.East:
                ++newX;
                arrivingViewport = new Rectangle(oldX + halfViewport - 4, oldY - halfViewport + 2, 1,
                    Game.ActiveConfiguration.Constants.ViewportSize);
                departingViewport = new Rectangle(oldX - halfViewport + 2, oldY - halfViewport + 2, 1,
                    Game.ActiveConfiguration.Constants.ViewportSize);
                break;
        }

        map.Warps.TryGetValue(new Tuple<byte, byte>((byte)newX, (byte)newY), out var targetWarp);
        map.Reactors.TryGetValue(((byte)newX, (byte)newY), out var newReactors);
        map.Reactors.TryGetValue(((byte)oldX, (byte)oldY), out var oldReactors);

        // Now that we know where we are going, perform some sanity checks.
        // Is the player trying to walk into a wall, or off the map?

        if (newX > map.X || newY > map.Y || newX < 0 || newY < 0)
        {
            Refresh();
            return false;
        }
        // Allow a user to walk into walls, if and only if collisions are disabled (implies privileged user)

        if (map.IsWall(newX, newY) && !CollisionsDisabled)
        {
            Refresh();
            return false;
        }

        // Is the player trying to walk into an occupied tile?
        foreach (var obj in map.GetTileContents((byte)newX, (byte)newY))
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
        if (targetWarp is not null)
        {
            if (targetWarp.MinimumLevel > Stats.Level)
            {
                Client?.SendMessage("You're too afraid to even approach it!", 3);
                Refresh();
                return false;
            }

            if (targetWarp.MaximumLevel < Stats.Level)
            {
                Client?.SendMessage("Your honor forbids you from entering.", 3);
                Refresh();
                return false;
            }
        }

        // Is the user trying to move into a reactor tile with blocking (meaning the reactor can't be "walked" on)?
        if (newReactors is not null && newReactors.Values.Any(predicate: x => x.Blocking))
        {
            Client?.SendMessage("Your path is blocked!", 3);
            Refresh();
        }

        // Calculate the common viewport between the old and new position

        commonViewport = new Rectangle(oldX - halfViewport, oldY - halfViewport,
            Game.ActiveConfiguration.Constants.ViewportSize,
            Game.ActiveConfiguration.Constants.ViewportSize);
        commonViewport.Intersect(new Rectangle(newX - halfViewport, newY - halfViewport,
            Game.ActiveConfiguration.Constants.ViewportSize,
            Game.ActiveConfiguration.Constants.ViewportSize));
        GameLog.DebugFormat("Moving from {0},{1} to {2},{3}", oldX, oldY, newX, newY);
        GameLog.DebugFormat("Arriving viewport is a rectangle starting at {0}, {1}", arrivingViewport.X,
            arrivingViewport.Y);
        GameLog.DebugFormat("Departing viewport is a rectangle starting at {0}, {1}", departingViewport.X,
            departingViewport.Y);
        GameLog.DebugFormat("Common viewport is a rectangle starting at {0}, {1} of size {2}, {3}", commonViewport.X,
            commonViewport.Y, commonViewport.Width, commonViewport.Height);

        X = (byte)newX;
        Y = (byte)newY;
        Direction = direction;

        // Transmit update to the moving client, as we are actually walking now
        Enqueue(new ConfirmWalkPacket
        {
            Direction = (DALib.Enums.Direction)(byte)direction,
            OldX = (byte)oldX,
            OldY = (byte)oldY
        });

        // Retail sends an empty door packet after every self-move
        Enqueue(new DoorPacket());

        // Objects in the common viewport receive a "walk" (0x0C) packet
        // Objects in the arriving viewport receive a "show to" (0x33) packet
        // Objects in the departing viewport receive a "remove object" (0x0E) packet

        foreach (var obj in map.EntityTree.GetObjects(commonViewport))
        {
            if (obj != this && obj is User)
            {
                var user = (User)obj;
                GameLog.DebugFormat("Sending walk packet for {0} to {1}", Name, user.Name);
                user.Enqueue(new CreatureWalkPacket
                {
                    SourceId = Id,
                    OldX = (byte)oldX,
                    OldY = (byte)oldY,
                    Direction = (DALib.Enums.Direction)(byte)direction
                });
            }

            // Reactors receive an OnMove event
            if (obj != this && obj is Reactor)
            {
                var reactor = (Reactor)obj;
                reactor.OnMove(this);
            }
        }

        foreach (var obj in map.EntityTree.GetObjects(arrivingViewport))
        {
            obj.AoiEntry(this);
            AoiEntry(obj);
        }

        foreach (var obj in map.EntityTree.GetObjects(departingViewport))
        {
            obj.AoiDeparture(this);
            AoiDeparture(obj);
        }

        if (targetWarp is not null) return targetWarp.Use(this);

        // Handle stepping onto a reactor, leaving a reactor, or both
        if (newReactors is not null)
            foreach (var reactor in newReactors.Values)
                reactor.OnEntry(this);

        if (oldReactors is not null)
            foreach (var reactor in oldReactors.Values)
                reactor.OnLeave(this);

        HasMoved = true;
        map.EntityTree.Move(this);
        return true;
    }

    public bool AddGold(Gold gold) => AddGold(gold.Amount);

    public bool AddGold(uint amount)
    {
        if (Gold + amount > Game.ActiveConfiguration.Constants.PlayerMaxGold)
        {
            Client?.SendMessage("You cannot carry any more gold.", 3);
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
        if (Location.Map is { } map)
            map.Insert(itemObject, X, Y);
        else
            GameLog.UserActivityError("{Name}: AddItem: no map to drop {Item}, item lost", Name, itemObject.Name);
        return false;
    }

    public bool AddItem(ItemObject itemObject, byte slot, bool updateWeight = true)
    {
        // Weight check

        if (itemObject.Weight + CurrentWeight > MaximumWeight && !itemObject.Bound)
        {
            SendSystemMessage("It's too heavy.");
            if (Location.Map is { } map)
                map.Insert(itemObject, X, Y);
            else
                GameLog.UserActivityError("{Name}: AddItem: no map to drop {Item}, item lost", Name,
                    itemObject.Name);
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
                if (Location.Map is { } map)
                    map.Insert(itemObject, X, Y);
                else
                    GameLog.UserActivityError("{Name}: AddItem: no map to drop {Item}, item lost", Name,
                        itemObject.Name);
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
            if (Location.Map is { } map)
                map.Insert(itemObject, X, Y);
            else
                GameLog.UserActivityError("{Name}: AddItem: no map to drop {Item}, item lost", Name,
                    itemObject.Name);
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
                        var slot = Inventory[i]!;
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
                                quantity -= (ushort)diff;
                            }

                            SendItemUpdate(slot, i);
                            if (updateWeight) Inventory.RecalculateWeight();
                        }
                    }

                if (quantity > 0)
                    do
                    {
                        var item = World.CreateItem(xmlItem);
                        if (quantity > item.MaximumStack)
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
                    } while (quantity > 0);
            }
            else
            {
                do
                {
                    var item = World.CreateItem(xmlItem);
                    if (quantity > item.MaximumStack)
                    {
                        item.Count = item.MaximumStack;
                        quantity -= (byte)item.MaximumStack;
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
                var item = World.CreateItem(xmlItem);
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

    public bool RemoveItem(string itemName, ushort quantity = 0x01, bool updateWeight = true, bool force = false)
    {
        var slotsToUpdate = new List<byte>();
        var slotsToClear = new List<byte>();
        if (Inventory.ContainsName(itemName, quantity))
        {
            var remaining = (int)quantity;
            var slots = Inventory.GetSlotsByName(itemName);
            foreach (var i in slots)
                if (remaining > 0)
                {
                    if (Inventory[i] is not { } item) continue;
                    if (item.Stackable)
                    {
                        if (item.Count <= remaining)
                        {
                            GameLog.Info(
                                "RemoveItem {Item}, quantity {Quantity}: removing stack from slot {Slot} with {Count}",
                                itemName, quantity, i, item.Count);
                            remaining -= item.Count;
                            Inventory.Remove(i);
                            slotsToClear.Add(i);
                        }
                        else if (item.Count > remaining)
                        {
                            GameLog.Info(
                                "RemoveItem {Item}, quantity {Quantity}: removing quantity from stack, slot {Slot} with amount {Count}",
                                itemName, quantity, i, item.Count);
                            item.Count -= remaining;
                            remaining = 0;
                            slotsToUpdate.Add(i);
                        }
                    }
                    else
                    {
                        GameLog.Info(
                            "RemoveItem {Item}, quantity {Quantity}: removing nonstackable item from slot {Slot} with amount {Count}",
                            itemName, quantity, i, item.Count);
                        Inventory.Remove(i);
                        remaining--;
                        slotsToClear.Add(i);
                    }
                }
                else
                {
                    GameLog.Info("RemoveItem {Item}, quantity {Quantity}: done, remaining {Remaining}", itemName, quantity, remaining);
                    break;
                }

            foreach (var slot in slotsToClear)
            {
                GameLog.Info("clearing slot {Slot}", slot);
                SendClearItem(slot);
            }

            foreach (var slot in slotsToUpdate)
                SendItemUpdate(Inventory[slot], slot);
            UpdateAttributes(StatUpdateFlags.Current);
            return true;
        }

        GameLog.Info("RemoveItem {Item}, quantity {Quantity}: not found", itemName, quantity);
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
                .Any(predicate: req => req.Slot == (EquipmentSlot)slot))
            {
                SendSystemMessage("Other equipment must be removed first.");
                return false;
            }
        }

        if (Equipment.Remove(slot))
        {
            SendRefreshEquipmentSlot(slot);
            // Equipment.Remove(slot) returned true, so the slot was occupied and item is non-null.
            SendSystemMessage($"Unequipped {item!.Name}");
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
        Enqueue(new RemoveEquipmentPacket { Slot = (DALib.Enums.EquipmentSlot)(byte)slot });
    }

    public override void Refresh()
    {
        if (Location.Map is not { } map) return;
        SendMapInfo();
        SendLocation();
        SendUpdateToUser();
        SendRefresh();


        foreach (var obj in map.EntityTree.GetObjects(GetViewport()))
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

    public override void RegenerateMp(double mp, Creature? regenerator = null)
    {
        base.RegenerateMp(mp, regenerator);
        UpdateAttributes(StatUpdateFlags.Current);
    }

    public override void OnDamage(DamageEvent damageEvent)
    {
        SendCombatLogMessage(damageEvent);
    }

    public override void OnHeal(HealEvent healEvent)
    {
        SendCombatLogMessage(healEvent);
    }

    public override void Damage(double damage, ElementType element = ElementType.None,
        DamageType damageType = DamageType.Direct, DamageFlags damageFlags = DamageFlags.None, Creature? attacker = null,
        Castable? castable = null, bool onDeath = true)
    {
        if (Condition.Comatose || !Condition.Alive) return;
        base.Damage(damage, element, damageType, damageFlags, attacker, castable,
            false); // We handle ondeath for users here
        if (Stats.Hp == 0 && Group != null)
        {
            Stats.Hp = 1;
            var handler = Game.ActiveConfiguration.Handlers?.Death?.Coma;
            if (handler?.Value != null && World.WorldData.TryGetValue(handler.Value, out Status status))
            {
                Condition.Comatose = true;
                ApplyStatus(new CreatureStatus(status, this, null, attacker));
            }
            else
            {
                GameLog.Warning("No coma handler or status found - user {Name} died!", Name);
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
                if (item.EquipmentSlot != (byte)ItemSlots.Weapon && !item.Undamageable)
                    item.Durability -= 1 / (item.MaximumDurability * (100 - Stats.Ac == 0 ? 1 : 100 - Stats.Ac));
        }

        UpdateAttributes(StatUpdateFlags.Current);
    }

    public override void Heal(double heal, Creature? source = null, Castable? castable = null)
    {
        base.Heal(heal, source, castable);
        if (Condition.IsHpIncreaseProhibited)
            SendSystemMessage("You cannot currently receive healing.");
        UpdateAttributes(StatUpdateFlags.Current);
    }

    private bool CheckCastableRestriction(EquipmentRestriction restriction, out string message)
    {
        // This code is intentionally verbose due to the number of combinations that can occur in xml
        message = string.Empty;

        switch (restriction.RestrictionType)
        {
            case RestrictionType.InInventory:
                {
                    // <Item RestrictionType="InInventory">Slice of Ham</Item> <!-- Must have slice of ham in inventory-->
                    // <Item RestrictionType="InInventory"/> <!-- Nonsensical - return true if inventory is not empty -->

                    if (string.IsNullOrEmpty(restriction.Value))
                    {
                        if (!Inventory.IsEmpty)
                            return true;
                    }
                    else if (Inventory.ContainsName(restriction.Value))
                    {
                        return true;
                    }
                }
                break;
            case RestrictionType.NotInInventory:
                {
                    // <Item RestrictionType="NotInInventory">Slice of Ham</Item> <!-- Must have slice of ham in inventory-->
                    // <Item RestrictionType="NotInInventory"/> <!-- Nonsensical - return true if inventory is empty -->

                    if (string.IsNullOrEmpty(restriction.Value))
                    {
                        if (Inventory.IsEmpty)
                            return true;
                    }
                    else if (!Inventory.ContainsName(restriction.Value))
                    {
                        return true;
                    }
                }
                break;
            case RestrictionType.Equipped:
                {
                    switch (restriction.Slot)
                    {
                        case EquipmentSlot.Weapon:
                            {
                                var item = Equipment.Weapon;
                                if (item == null)
                                    break;
                                // <Item Slot="Weapon" RestrictionType="Equipped"/> <!-- Any weapon must be equipped (None is default for WeaponType field) -->
                                // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="Equipped"/> <!-- Claw must be equipped -->
                                if (string.IsNullOrEmpty(restriction.Value))
                                {
                                    if (item.WeaponType == restriction.WeaponType || restriction.WeaponType == WeaponType.None)
                                        return true;
                                }
                                // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="Equipped">Ham Slicer</Item> <!-- A claw weapon named Ham Slicer must be equipped -->
                                else
                                {
                                    if (restriction.WeaponType == WeaponType.None && item.Name == restriction.Value)
                                        return true;
                                    if (item.WeaponType == restriction.WeaponType && item.Name == restriction.Value)
                                        return true;
                                }

                                break;
                            }
                        // All other slots
                        // <Item Slot="None" RestrictionType="Equipped"/> <!-- Somewhat nonsensical, we interpret as "player has anything equipped" -->
                        case EquipmentSlot.None when string.IsNullOrEmpty(restriction.Value):
                            {
                                if (!Equipment.IsEmpty)
                                    return true;
                                break;
                            }
                        // <Item Slot="None" RestrictionType="Equipped">Ham Slapper</Item> <!-- Player has any item equipped named "Ham Slapper" -->
                        case EquipmentSlot.None when Equipment.FindByName(restriction.Value) != null:
                            return true;
                        // Failure of test above
                        case EquipmentSlot.None:
                            break;
                        // Nominal cases:
                        // <Item Slot="Foot" RestrictionType="Equipped">Ham Boots</Item> <!-- Player must have Ham Boots equipped in Foot slot -->
                        // <Item Slot="Foot" RestrictionType="Equipped"> <!-- Player must have something equipped in Foot slot -->
                        default:
                            {
                                var item = Equipment[restriction.Slot];
                                if (item == null) break;
                                if (string.IsNullOrEmpty(restriction.Value))
                                    return true;
                                if (item.Name == restriction.Value)
                                    return true;
                                break;
                            }
                    }
                }
                break;
            case RestrictionType.NotEquipped:
                {
                    switch (restriction.Slot)
                    {
                        case EquipmentSlot.Weapon:
                            {
                                var item = Equipment.Weapon;
                                // <Item Slot="Weapon" RestrictionType="NotEquipped"/> <!-- A weapon must not be equipped (None is default for WeaponType field) -->
                                if (item == null)
                                    return true;
                                // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="NotEquipped"/> <!-- Claw must not be equipped -->
                                if (string.IsNullOrEmpty(restriction.Value))
                                {
                                    if (restriction.WeaponType != WeaponType.None && restriction.WeaponType != item.WeaponType)
                                        return true;
                                }
                                // <Item Slot="Weapon" WeaponType="Claw" RestrictionType="NotEquipped">Ham Slicer</Item> <!-- A claw weapon named Ham Slicer must not be equipped -->
                                // <Item Slot="Weapon" RestrictionType="NotEquipped">Ham Slicer</Item> <!-- A weapon named Ham Slicer must not be equipped -->
                                else
                                {
                                    if (restriction.WeaponType == WeaponType.None)
                                    {
                                        if (item.Name != restriction.Value)
                                            return true;
                                    }
                                    else if (item.WeaponType != restriction.WeaponType)
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        if (item.Name != restriction.Value)
                                            return true;
                                    }
                                }

                                break;
                            }
                        // All other slots
                        // <Item Slot="None" RestrictionType="NotEquipped"/> <!-- Somewhat nonsensical, we interpret as "player has nothing equipped" -->
                        case EquipmentSlot.None when string.IsNullOrEmpty(restriction.Value):
                            {
                                if (Equipment.IsEmpty)
                                    return true;
                                break;
                            }
                        // <Item Slot="None" RestrictionType="NotEquipped">Ham Slapper</Item> <!-- Player has no item equipped named "Ham Slapper" -->
                        case EquipmentSlot.None when Equipment.FindByName(restriction.Value) == null:
                            return true;
                        // Failure of test above
                        case EquipmentSlot.None:
                            break;
                        // Nominal cases:
                        // <Item Slot="Foot" RestrictionType="NotEquipped">Ham Boots</Item> <!-- Player must not have Ham Boots equipped in Foot slot -->
                        // <Item Slot="Foot" RestrictionType="NotEquipped"> <!-- Player must not have something equipped in Foot slot -->
                        default:
                            {
                                var item = Equipment[restriction.Slot];
                                if (item == null) return true;
                                if (!string.IsNullOrEmpty(restriction.Value) && item.Name != restriction.Value)
                                    return true;
                                break;
                            }
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException($"Restriction type {restriction} is not supported");
        }

        message = !string.IsNullOrEmpty(restriction.Message)
            ? Game.World.GetLocalString(restriction.Message)
            : "It cannot be done";
        return false;
    }

    private bool CheckCastableRestrictions(List<EquipmentRestriction> restrictions, out string message)
    {
        message = string.Empty;

        if (restrictions.Count == 0)
            return true;

        foreach (var restriction in restrictions)
            // By design intent, equipment restrictions are OR and not AND, so first one wins
            if (CheckCastableRestriction(restriction, out message))
                return true;

        return false;
    }

    public bool UseCastable(Castable castableXml, Creature? target = null, bool castCost = true,
        bool evalRestrictions = true)
    {
        if (castableXml.Intents[0].UseType == SpellUseType.Prompt)
            //do something.
            return false;

        // Check for target immunity

        // Check casting costs
        if (castCost)
            if (!ProcessCastingCost(castableXml, target, out var message))
            {
                SendSystemMessage(message);
                return false;
            }

        if (evalRestrictions)
        {
            if (CheckCastableRestrictions(castableXml.Restrictions, out var restrictionMessage))
            {
                if (castableXml.BreakStealth && Condition.IsInvisible)
                {
                    Condition.IsInvisible = false;
                    // Remove statuses that cause invisibility
                    foreach (var activeStatus in CurrentStatuses.Values.ToList()) {
                        if (activeStatus.ConditionChanges?.Set.HasFlag(CreatureCondition.Invisible) == true) {
                            RemoveStatus(activeStatus.Icon);
                        }
                    }
                }
                return base.UseCastable(castableXml, target);
            }

            SendSystemMessage(restrictionMessage);
            return false;
        }

        return base.UseCastable(castableXml, target);
    }

    public void AssailAttack(Direction direction, Creature? target = null)
    {
        target ??= GetDirectionalTarget(direction);
        var animation = false;

        foreach (var c in SkillBook.Where(predicate: c => c.Castable is { IsAssail: true }))
            if (target != null && target.GetType() != typeof(Merchant))
            {
                UseSkill(SkillBook.SlotOf(c.Castable.Name));
                LastTarget = target;
                animation = true;
            }

        if (!animation)
        {
            var motionId = (byte)1;
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

            if (Condition.IsInvisible)
                Condition.IsInvisible = false;

            var firstAssail = SkillBook.FirstOrDefault(predicate: x => x.Castable is { IsAssail: true });
            var soundId = (byte)(firstAssail != null ? firstAssail.Castable.Effects?.Sound?.Id ?? 1 : 1);
            if (firstAssail != null && firstAssail.Castable.TryGetMotion(Class, out var motion))
                Motion(motion.Id, motion.Speed);
            PlaySound(Equipment?.Weapon?.AssailSound ?? soundId);
        }
    }


    private string GroupProfileSegment()
    {
        var sb = new StringBuilder();

        // Only build this string if the user's in a group. Otherwise an empty
        // string should be sent. Snapshot: the group can be disbanded cross-thread.
        if (Group is not { } group) return sb.ToString();
        sb.Append("Group members");
        sb.Append((char)0x0A);

        // The user's name should go first, and should not have an asterisk.
        // In practice this will mean that the user's name appears first and
        // is grayed out, while all other names are white.
        sb.Append("  " + Name);
        sb.Append((char)0x0A);

        foreach (var member in group.Members)
            if (member.Name != Name)
            {
                sb.Append("  " + member.Name);
                sb.Append((char)0x0A);
            }

        sb.Append($"Total {Group.Members.Count}");

        return sb.ToString();
    }

    /// <summary>
    ///     Send a player's profile to themselves (e.g. click on self or hit Y for group info)
    /// </summary>
    public void SendProfile()
    {
        var recruit = GroupRecruit ?? Group?.RecruitInfo;
        var guildInfo = GetGuildInfo();

        var profile = new SelfProfilePacket
        {
            NationFlag = Nation.Flag,
            GuildRank = guildInfo.GuildRank,
            CurrentTitle = Title,
            GroupStatusText = Group is not { } group
                ? SelfProfilePacket.GroupStatusSolo
                : SelfProfilePacket.FormatGroupRoster(group.Founder.Name, group.Members.Select(m => m.Name)),
            CanGroup = Grouping,
            Recruit = recruit?.ToRecruitInfo(),
            Class = (byte)Class,
            ClassName = IsMaster ? "Master" : Class.ToString(),
            GuildName = guildInfo.GuildName ?? string.Empty,
            // Wire u8 cap. Defensive only today — Legend.MaximumLegendSize (254) is enforced on
            // add, so this can't currently trigger. TakeLast, not Take: _legend is keyed by
            // timestamp ascending, so if the cap ever rises this keeps the newest marks rather
            // than freezing the pane on the oldest 255. DALib throws above 255 rather than
            // emitting a count/row desync the way the legacy site did.
            Legend = Legend.TakeLast(SelfProfilePacket.MaxLegendMarks)
                .Select(mark => new DALib.Networking.Packets.Server.LegendMark
                {
                    Icon = (byte)mark.Icon,
                    Color = (byte)mark.Color,
                    Prefix = mark.Prefix ?? string.Empty,
                    Text = mark.ToString()
                })
                .ToList()
        };

        // The client's parse ends at the legend loop; nothing may follow it.
        Enqueue(profile);
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
        // Screen coords are raw u16 — a %255 quadrant/offset split corrupts them at >=255 — and
        // each node carries structured routing (map_id plus destination coords).
        var nodes = new List<WorldMapNode>();
        foreach (var point in map.Points)
        {
            ushort mapId = 0;
            if (World.WorldState.TryGetValueByIndex<MapObject>(point.DestinationMap, out var destMap))
                mapId = destMap.Id;
            else
                GameLog.Warning("SendWorldMap: node {Node} targets unknown map {Map}; map_id=0",
                    point.Name, point.DestinationMap);

            nodes.Add(new WorldMapNode
            {
                X = (ushort)point.X,
                Y = (ushort)point.Y,
                Text = point.Name,
                CheckSum = 0,
                MapId = mapId,
                DestinationX = point.DestinationX,
                DestinationY = point.DestinationY
            });
        }

        IsAtWorldMap = true;
        ActiveWorldMap = map; // remember the offered destinations for 0x3F click validation
        Enqueue(new WorldMapPacket
        {
            FieldName = map.ClientMap,
            ImageIndex = 0, // current_node_index: Hybrasyl does not track it (legacy emitted 0x00)
            Nodes = nodes
        });
    }

    public void SendAnimation(uint id, byte motion, short speed) =>
        Enqueue(new PlayerAnimationPacket { SourceId = id, Animation = motion, Speed = (ushort)speed });

    public void SendEffect(uint id, ushort effect, short speed)
    {
        GameLog.DebugFormat("SendEffect: id {0}, effect {1}, speed {2} ", id, effect, speed);
        Enqueue(new SpellAnimationPacket
        {
            TargetId = id,
            SourceId = id,
            TargetAnimation = effect,
            Speed = (ushort)speed
        });
    }

    public void SendEffect(uint targetId, ushort targetEffect, uint srcId, ushort srcEffect, short speed)
    {
        GameLog.DebugFormat("SendEffect: targetId {0}, targetEffect {1}, srcId {2}, srcEffect {3}, speed {4}",
            targetId, targetEffect, srcId, srcEffect, speed);
        Enqueue(new SpellAnimationPacket
        {
            TargetId = targetId,
            SourceId = srcId,
            TargetAnimation = targetEffect,
            SourceAnimation = srcEffect,
            Speed = (ushort)speed
        });
    }

    public void SendEffect(short x, short y, ushort effect, short speed)
    {
        GameLog.DebugFormat("SendEffect: x {0}, y {1}, effect {2}, speed {3}", x, y, effect, speed);
        // TargetId 0 selects the area form: [u32 0][anim][speed][x][y]
        Enqueue(new SpellAnimationPacket
        {
            TargetId = 0,
            TargetAnimation = effect,
            Speed = (ushort)speed,
            X = (ushort)x,
            Y = (ushort)y
        });
    }

    public void SendMusic(byte track)
    {
        if (CurrentMusicTrack == track) return;

        CurrentMusicTrack = track;
        Enqueue(new PlaySoundPacket { Sound = PlaySoundPacket.MusicMarker, MusicTrack = track });
    }

    public void SendSound(byte sound)
    {
        GameLog.DebugFormat("SendSound {0}", sound);
        Enqueue(new PlaySoundPacket { Sound = sound });
    }

    public void SendDoorUpdate(byte x, byte y, bool state, bool leftright) =>
        Enqueue(new DoorPacket
        {
            Doors = [new WireDoor { X = x, Y = y, Closed = state, OpenRight = leftright }]
        });

    public void OpenManufacture(IEnumerable<ManufactureRecipe> recipes)
    {
        ManufactureState = new ManufactureState(this, recipes);
        ManufactureState.ShowWindow();
    }

    // 0x2F merchant menus. The prefix is identical at every site, so it lives here once: the
    // client's ignored four-byte secondary group gets a repeat of the speaker sprite, which is
    // what the legacy builder wrote. DALib's defaults already supply the rest of the legacy
    // constants (Unknown1 = 0, Unknown2 = 1, Color/Color2 = 0, IllustrationIndex = 0).
    //
    // Menu type and body shape are paired in these overloads rather than at the call sites, so a
    // site cannot pair them wrongly. Note every Hybrasyl pursuit id is in the 0xFF00+ private
    // range, so DALib's pursuit-keyed row forks (0x4B item, 0x4E inventory) never fire.
    private static NpcMenuPacket MerchantMenu(Merchant merchant, NpcMenuType type, string text, NpcMenu menu) =>
        new()
        {
            MenuType = type,
            SourceId = merchant.Id,
            Sprite = (ushort)(0x4000 + merchant.Sprite),
            Sprite2 = (ushort)(0x4000 + merchant.Sprite),
            Name = merchant.Name,
            Text = text,
            Menu = menu
        };

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, MerchantOptions options) =>
        MerchantMenu(merchant, NpcMenuType.Options, text, new OptionsMenu
        {
            Options = options.Options.Select(o => new NpcMenuOption(o.Text, o.Id)).ToList()
        });

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, MerchantInput input) =>
        MerchantMenu(merchant, NpcMenuType.TextEntry, text, new TextEntryMenu { PursuitId = input.Id });

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, MerchantShopItems items) =>
        MerchantMenu(merchant, NpcMenuType.ItemList, text, new ItemListMenu
        {
            PursuitId = items.Id,
            Items = items.Items
                .Select(i => new NpcMenuItem(i.Tile, i.Color, i.Price, i.Name, i.Description)).ToList()
        });

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, UserInventoryItems items) =>
        MerchantMenu(merchant, NpcMenuType.PlayerItemList, text, new PlayerItemListMenu
        {
            PursuitId = items.Id,
            Slots = items.InventorySlots
        });

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, MerchantSpells spells) =>
        MerchantMenu(merchant, NpcMenuType.SpellList, text, new SpellListMenu
        {
            PursuitId = spells.Id,
            Spells = spells.Spells
                .Select(s => new NpcMenuCastable(s.IconType, s.Icon, s.Color, s.Name)).ToList()
        });

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, MerchantSkills skills) =>
        MerchantMenu(merchant, NpcMenuType.SkillList, text, new SkillListMenu
        {
            PursuitId = skills.Id,
            Skills = skills.Skills
                .Select(s => new NpcMenuCastable(s.IconType, s.Icon, s.Color, s.Name)).ToList()
        });

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, UserSpellBook book) =>
        MerchantMenu(merchant, NpcMenuType.PlayerSpellList, text,
            new PlayerSpellListMenu { PursuitId = book.Id });

    private static NpcMenuPacket MerchantMenu(Merchant merchant, string text, UserSkillBook book) =>
        MerchantMenu(merchant, NpcMenuType.PlayerSkillList, text,
            new PlayerSkillListMenu { PursuitId = book.Id });

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
                var requirement =
                    result.Requirements.FirstOrDefault(x => x.Class.Contains(Class) || x.Class.Contains(Class.None));
                if (requirement != null)
                {
                    if (!string.IsNullOrWhiteSpace(requirement.ForbidCookie) &&
                        GetCookie(requirement.ForbidCookie) != null)
                        continue;
                    if (!string.IsNullOrWhiteSpace(requirement.RequireCookie) &&
                        GetCookie(requirement.RequireCookie) == null)
                        continue;
                }
                merchantSkills.Skills.Add(new MerchantSkill
                {
                    IconType = 3,
                    Icon = result.Icon,
                    Color = 1,
                    Name = result.Name
                });
            }

        merchantSkills.Id = (ushort)MerchantMenuItem.LearnSkill;

        var packet = MerchantMenu(merchant, merchant.GetLocalString("learn_skill"), merchantSkills);

        Enqueue(packet);
    }

    public void ShowForgetSkillMenu(Merchant merchant)
    {
        var userSkills = new UserSkillBook
        {
            Id = (ushort)MerchantMenuItem.ForgetSkillAccept
        };

        var packet = MerchantMenu(merchant, merchant.GetLocalString("forget_skill"), userSkills);

        Enqueue(packet);
    }

    public void ShowForgetSkillAccept(Merchant merchant, byte slot)
    {
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        var packet = MerchantMenu(merchant, merchant.GetLocalString("forget_castable_success"), options);
        Enqueue(packet);

        SkillBook.Remove(slot);
        SendClearSkill(slot);
    }

    public void ShowForgetSpellMenu(Merchant merchant)
    {
        var userSpells = new UserSpellBook
        {
            Id = (ushort)MerchantMenuItem.ForgetSpellAccept
        };

        var packet = MerchantMenu(merchant, merchant.GetLocalString("forget_spell"), userSpells);

        Enqueue(packet);
    }

    public void ShowForgetSpellAccept(Merchant merchant, byte slot)
    {
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        var packet = MerchantMenu(merchant, merchant.GetLocalString("forget_castable_success"), options);
        Enqueue(packet);

        SpellBook.Remove(slot);
        SendClearSpell(slot);
    }

    public void ShowLearnSkill(Merchant merchant, Castable castable)
    {
        var skillDesc =
            castable.Descriptions.First(predicate: x => x.Class.Contains(Class) || x.Class.Contains(Class.Peasant));

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();

        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort)MerchantMenuItem.LearnSkillAgree,
            Text = "Yes"
        });
        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort)MerchantMenuItem.LearnSkillDisagree,
            Text = "No"
        });

        var packet = MerchantMenu(merchant, merchant.GetLocalString("learn_skill", ("$NAME", castable.Name), ("$DESC", skillDesc.Value)), options);

        PendingLearnable = new PendingLearnable(castable, merchant.Id, IsSkillFlow: true);

        Enqueue(packet);
    }

    public void ShowLearnSkillAgree(Merchant merchant)
    {
        // Established by the preceding learn-skill step at this merchant; anything else
        // (absent, spell flow, different merchant) is a crafted or out-of-order packet.
        if (PendingLearnable is not { IsSkillFlow: true } pending || pending.MerchantId != merchant.Id)
        {
            GameLog.UserActivityWarning("{Name}: ShowLearnSkillAgree: no pending skill for merchant {Merchant}, ignoring",
                Name, merchant.Name);
            return;
        }

        var castable = pending.Castable;
        //now check requirements.
        var classReq = castable.Requirements.First(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);

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
        {
            if (!string.IsNullOrWhiteSpace(classReq.Prerequisites.ForbidCookie) &&
                GetCookie(classReq.Prerequisites.ForbidCookie) != null)
                prompt = classReq.Prerequisites.ForbidMessage;
            else if (!string.IsNullOrWhiteSpace(classReq.Prerequisites.RequireCookie) &&
                GetCookie(classReq.Prerequisites.RequireCookie) == null)
                prompt = classReq.Prerequisites.RequireMessage;
            else
            {
                foreach (var preReq in classReq.Prerequisites.Prerequisite)
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

                        if (Math.Floor(slot.UseCount / (double)slot.Castable.Mastery.Uses * 100) < preReq.Level)
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
                Id = (ushort)MerchantMenuItem.LearnSkillAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort)MerchantMenuItem.LearnSkillDisagree,
                Text = "No"
            });
        }


        var packet = MerchantMenu(merchant, prompt, options);

        Enqueue(packet);
    }

    public void ShowLearnSkillAccept(Merchant merchant)
    {
        // Established by the preceding learn-skill step at this merchant; anything else
        // (absent, spell flow, different merchant) is a crafted or out-of-order packet.
        if (PendingLearnable is not { IsSkillFlow: true } pending || pending.MerchantId != merchant.Id)
        {
            GameLog.UserActivityWarning("{Name}: ShowLearnSkillAccept: no pending skill for merchant {Merchant}, ignoring",
                Name, merchant.Name);
            return;
        }

        // Consume before any side effects: a replayed accept must restart the dialog
        PendingLearnable = null;
        var castable = pending.Castable;

        if (SkillBook.Contains(castable.Id))
        {
            GameLog.UserActivityWarning("{Name}: ShowLearnSkillAccept: already knows {Castable}, ignoring", Name,
                castable.Name);
            return;
        }
        var classReq = castable.Requirements.First(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);

        var prompt = string.Empty;
        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();
        //verify user has required items.
        if (!(Gold >= classReq.Gold)) prompt = merchant.GetLocalString("learn_skill_prereq_gold");
        if (prompt == string.Empty)
            if (classReq.Items.Any(predicate: itemReq => !Inventory.ContainsName(itemReq.Value, itemReq.Quantity)))
                prompt = merchant.GetLocalString("learn_skill_prereq_item");

        if ((SkillBook.IsPrimaryFull && castable.Book == Xml.Objects.Book.PrimarySkill) ||
            (SkillBook.IsSecondaryFull && castable.Book == Xml.Objects.Book.SecondarySkill) ||
            (SkillBook.IsUtilityFull && castable.Book == Xml.Objects.Book.UtilitySkill))
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

        var packet = MerchantMenu(merchant, prompt, options);

        Enqueue(packet);
    }

    public void ShowLearnSkillDisagree(Merchant merchant)
    {
        PendingLearnable = null;

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();
        var packet = MerchantMenu(merchant, merchant.GetLocalString("forget_castable_success"), options);

        Enqueue(packet);
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
            var requirement =
                result.Requirements.FirstOrDefault(x => x.Class.Contains(Class) || x.Class.Contains(Class.None));
            if (requirement != null)
            {
                if (!string.IsNullOrWhiteSpace(requirement.ForbidCookie) &&
                    GetCookie(requirement.ForbidCookie) != null)
                    continue;
                if (!string.IsNullOrWhiteSpace(requirement.RequireCookie) &&
                    GetCookie(requirement.RequireCookie) == null)
                    continue;
            }
            merchantSpells.Spells.Add(new MerchantSpell
            {
                IconType = 2,
                Icon = result.Icon,
                Color = 1,
                Name = result.Name
            });
        }

        merchantSpells.Id = (ushort)MerchantMenuItem.LearnSpell;

        var packet = MerchantMenu(merchant, merchant.GetLocalString("learn_spell"), merchantSpells);

        Enqueue(packet);
    }

    public void ShowLearnSpell(Merchant merchant, Castable castable)
    {
        var spellDesc =
            castable.Descriptions.First(predicate: x => x.Class.Contains(Class) || x.Class.Contains(Class.Peasant));

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();

        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort)MerchantMenuItem.LearnSpellAgree,
            Text = "Yes"
        });
        options.Options.Add(new MerchantDialogOption
        {
            Id = (ushort)MerchantMenuItem.LearnSpellDisagree,
            Text = "No"
        });

        var packet = MerchantMenu(merchant, merchant.GetLocalString("learn_spell_choice", ("$NAME", castable.Name), ("$DESC", spellDesc.Value)), options);

        PendingLearnable = new PendingLearnable(castable, merchant.Id, IsSkillFlow: false);

        Enqueue(packet);
    }

    public void ShowLearnSpellAgree(Merchant merchant)
    {
        // Established by the preceding learn-spell step at this merchant; anything else
        // (absent, skill flow, different merchant) is a crafted or out-of-order packet.
        if (PendingLearnable is not { IsSkillFlow: false } pending || pending.MerchantId != merchant.Id)
        {
            GameLog.UserActivityWarning("{Name}: ShowLearnSpellAgree: no pending spell for merchant {Merchant}, ignoring",
                Name, merchant.Name);
            return;
        }

        var castable = pending.Castable;
        //now check requirements.
        var classReq = castable.Requirements.First(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);
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
        {
            if (!string.IsNullOrWhiteSpace(classReq.Prerequisites.ForbidCookie) &&
                GetCookie(classReq.Prerequisites.ForbidCookie) != null)
                prompt = classReq.Prerequisites.ForbidMessage;
            else if (!string.IsNullOrWhiteSpace(classReq.Prerequisites.RequireCookie) &&
                     GetCookie(classReq.Prerequisites.RequireCookie) == null)
                prompt = classReq.Prerequisites.RequireMessage;
            else
            {
                foreach (var preReq in classReq.Prerequisites.Prerequisite)
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
                        if (Math.Floor(slot.UseCount / (double)slot.Castable.Mastery.Uses * 100) < preReq.Level)
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
                Id = (ushort)MerchantMenuItem.LearnSpellAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort)MerchantMenuItem.LearnSpellDisagree,
                Text = "No"
            });
        }

        var packet = MerchantMenu(merchant, prompt, options);

        Enqueue(packet);
    }

    public void ShowLearnSpellAccept(Merchant merchant)
    {
        // Established by the preceding learn-spell step at this merchant; anything else
        // (absent, skill flow, different merchant) is a crafted or out-of-order packet.
        if (PendingLearnable is not { IsSkillFlow: false } pending || pending.MerchantId != merchant.Id)
        {
            GameLog.UserActivityWarning("{Name}: ShowLearnSpellAccept: no pending spell for merchant {Merchant}, ignoring",
                Name, merchant.Name);
            return;
        }

        // Consume before any side effects: a replayed accept must restart the dialog
        PendingLearnable = null;
        var castable = pending.Castable;

        if (SpellBook.Contains(castable.Id))
        {
            GameLog.UserActivityWarning("{Name}: ShowLearnSpellAccept: already knows {Castable}, ignoring", Name,
                castable.Name);
            return;
        }
        var classReq = castable.Requirements.First(predicate: x => x.Class.Contains(Class) || Class == Class.Peasant);
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

        if ((SpellBook.IsPrimaryFull && castable.Book == Xml.Objects.Book.PrimarySpell) ||
            (SpellBook.IsSecondaryFull && castable.Book == Xml.Objects.Book.SecondarySpell) ||
            (SpellBook.IsUtilityFull && castable.Book == Xml.Objects.Book.UtilitySpell))
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

        var packet = MerchantMenu(merchant, prompt, options);

        Enqueue(packet);
    }

    public void ShowLearnSpellDisagree(Merchant merchant)
    {
        PendingLearnable = null;

        var options = new MerchantOptions();
        options.Options = new List<MerchantDialogOption>();

        var packet = MerchantMenu(merchant, merchant.GetLocalString("forget_castable_success"), options);

        Enqueue(packet);
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
                    Tile = (ushort)(0x8000 + worldItem.Properties.Appearance.Sprite),
                    Color = (byte)worldItem.Properties.Appearance.Color,
                    Description = worldItem.Properties.Vendor?.Description ?? "",
                    Name = worldItem.Name,
                    Price = Convert.ToUInt32(worldItem.Properties.Physical.Value)
                });
                itemsCount++;
            }

        merchantItems.Id = (ushort)MerchantMenuItem.BuyItemQuantity;


        var packet = MerchantMenu(merchant, merchant.GetLocalString("buy"), merchantItems);
        Enqueue(packet);
    }

    public void ShowBuyMenuQuantity(Merchant merchant, string name)
    {
        var item = Game.World.WorldData.GetByIndex<Item>(name);
        PendingBuyableItem = name;
        if (item.Stackable)
        {
            var input = new MerchantInput();

            input.Id = (ushort)MerchantMenuItem.BuyItemAccept;


            var packet = MerchantMenu(merchant, merchant.GetLocalString("buy_quantity"), input);
            Enqueue(packet);
        }
        else //buy item
        {
            ShowBuyItem(merchant);
        }
    }

    public void ShowBuyItem(Merchant merchant, uint quantity = 1)
    {
        // Set by the preceding buy dialog step; absent only via crafted or
        // out-of-order merchant packets, so log and ignore.
        if (PendingBuyableItem is not { Length: > 0 } pendingBuyable ||
            !Game.World.WorldData.TryGetValueByIndex(pendingBuyable, out Item item))
        {
            GameLog.UserActivityWarning("{Name}: ShowBuyItem: no pending buyable item, ignoring", Name);
            return;
        }

        var prompt = string.Empty;
        var itemObj = Game.World.CreateItem(item);
        var reqGold = itemObj.Value * quantity;
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        if (MaximumWeight < CurrentWeight + item.Properties.Physical.Weight)
            prompt = merchant.GetLocalString("buy_failure_weight");

        if (quantity > merchant.GetOnHand(pendingBuyable))
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
                    merchant.ReduceInventory(pendingBuyable, quantity);
                    AddItem(itemObj.Name, (ushort)quantity);
                }
                else
                {
                    merchant.ReduceInventory(pendingBuyable, quantity);
                    AddItem(itemObj);
                }
            }
            else
            {
                if (itemObj.Stackable)
                {
                    merchant.ReduceInventory(pendingBuyable, quantity);
                    AddItem(itemObj.Name, (ushort)quantity);
                }
                else
                {
                    merchant.ReduceInventory(pendingBuyable, quantity);
                    AddItem(itemObj);
                }
            }

            RemoveGold(reqGold);
            SendCloseDialog();
        }
        else
        {
            var packet = MerchantMenu(merchant, prompt, options);

            Enqueue(packet);
        }
    }

    public void ShowSellMenu(Merchant merchant)
    {
        var inventoryItems = new UserInventoryItems();
        inventoryItems.InventorySlots = new List<byte>();
        inventoryItems.Id = (ushort)MerchantMenuItem.SellItemQuantity;
        var itemsCount = 0;

        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] is not { } item) continue;
            if (item.Exchangeable && item.Durability == item.MaximumDurability)
            {
                inventoryItems.InventorySlots.Add(i);
                itemsCount++;
            }
        }

        var packet = MerchantMenu(merchant, merchant.GetLocalString("sell"), inventoryItems);
        Enqueue(packet);
    }

    public void ShowSellQuantity(Merchant merchant, byte slot)
    {
        if (Inventory[slot] is not { } item)
        {
            GameLog.UserActivityWarning("{Name}: ShowSellQuantity: no item in slot {Slot}, ignoring", Name, slot);
            return;
        }

        PendingSellableSlot = slot;
        if (item.Stackable)
        {
            var input = new MerchantInput();

            input.Id = (ushort)MerchantMenuItem.SellItem;

            var packet = MerchantMenu(merchant, merchant.GetLocalString("sell_quantity", ("$QUANTITY", item.Count.ToString()), ("$ITEM", item.Name)), input);
            Enqueue(packet);
        }
        else
        {
            ShowSellConfirm(merchant, slot);
        }
    }

    public void ShowSellConfirm(Merchant merchant, byte slot, uint quantity = 1)
    {
        if (Inventory[slot] is not { } item)
        {
            GameLog.UserActivityWarning("{Name}: ShowSellConfirm: no item in slot {Slot}, ignoring", Name, slot);
            return;
        }

        PendingSellableSlot = slot;
        PendingSellableQuantity = quantity;
        var offer = (uint)(Math.Round(item.Value * Game.ActiveConfiguration.Constants.MerchantBuybackPercentage, 0) *
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
            if (!Inventory.ContainsName(item.Name, (int)quantity))
                prompt = merchant.GetLocalString("sell_failure_quantity");

        if (prompt == string.Empty)
            if (PendingMerchantOffer + Gold > Game.ActiveConfiguration.Constants.PlayerMaxGold)
                prompt = merchant.GetLocalString("sell_failure_gold_limit");

        if (prompt == string.Empty)
        {
            var quant = quantity > 1 ? "those" : "that";

            prompt = merchant.GetLocalString("sell_offer", ("$GOLD", offer.ToString()), ("$QUANTITY", quant));

            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort)MerchantMenuItem.SellItemAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort)MerchantMenuItem.MainMenu,
                Text = "No"
            });
        }


        var packet = MerchantMenu(merchant, prompt, options);

        Enqueue(packet);
    }

    public void SellItemAccept(Merchant merchant)
    {
        if (PendingSellableSlot == 0 || PendingSellableSlot > Inventory.Size)
        {
            SendSystemMessage("That didn't work.");
            return;
        }

        var item = Inventory[PendingSellableSlot];
        if (item == null)
        {
            SendSystemMessage("You don't have that item.");
            return;
        }

        if (item.Count > PendingSellableQuantity)
        {
            DecreaseItem(PendingSellableSlot, (int)PendingSellableQuantity);
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

        var packet = MerchantMenu(merchant, merchant.GetLocalString("sell_success"), options);

        Enqueue(packet);
    }

    public void ShowMerchantGoBack(Merchant merchant, string message,
        MerchantMenuItem menuItem = MerchantMenuItem.MainMenu)
    {
        var options = new MerchantOptions
        {
            Options = [new MerchantDialogOption { Id = (ushort)menuItem, Text = "Go back" }]
        };

        Enqueue(MerchantMenu(merchant, message, options));
    }

    public void ShowMerchantSendParcel(Merchant merchant)
    {
        var userItems = new UserInventoryItems { InventorySlots = new List<byte>() };
        var itemsCount = 0;

        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] is not { } item) continue;
            if (item.Exchangeable && item.Durability == item.MaximumDurability)
            {
                userItems.InventorySlots.Add(i);
                itemsCount++;
            }
        }

        userItems.Id = (ushort)MerchantMenuItem.SendParcelQuantity;

        var packet = MerchantMenu(merchant, merchant.GetLocalString("send_parcel"), userItems);
        Enqueue(packet);
    }

    public void ShowMerchantSendParcelQuantity(Merchant merchant, ItemObject item)
    {
        if (item.Stackable && item.Count > 1)
        {
            var input = new MerchantInput
            {
                Id = (ushort)MerchantMenuItem.SendParcelRecipient
            };

            var packet = MerchantMenu(merchant, merchant.GetLocalString("send_parcel_recipient", ("$QUANTITY", item.Count.ToString()), ("$ITEM", item.Name)), input);
            Enqueue(packet);
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
            Id = (ushort)MerchantMenuItem.SendParcelAccept
        };

        var packet = MerchantMenu(merchant, merchant.GetLocalString("send_parcel_recipient"), input);


        Enqueue(packet);
    }

    public void ShowMerchantSendParcelAccept(Merchant merchant, string recipient)
    {
        // Set by the preceding parcel dialog step; absent only via crafted or
        // out-of-order merchant packets, so log and ignore.
        if (PendingSendableParcel is not { } itemObj)
        {
            GameLog.UserActivityWarning("{Name}: ShowMerchantSendParcelAccept: no pending parcel, ignoring", Name);
            return;
        }

        var quantity = PendingSendableQuantity;
        PendingParcelRecipient = recipient;
        var prompt = string.Empty;
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };
        //verify user has required items.
        var parcelFee = (uint)Math.Ceiling(itemObj.Value * .10 * quantity);
        if (!Game.World.WorldState.TryGetAuthInfo(recipient, out var info))
            prompt = merchant.GetLocalString("parcel_recipient_nonexistent");
        if (prompt == string.Empty)
            if (!(Gold > parcelFee))
                prompt = merchant.GetLocalString("send_parcel_fail", ("$FEE", parcelFee.ToString()));
        if (prompt == string.Empty)
        {
            RemoveGold(parcelFee);
            RemoveItem(itemObj.Name, (ushort)quantity);
            SendInventory();
            prompt = merchant.GetLocalString("send_parcel_success");

            // TryGetAuthInfo succeeded above (prompt is still empty), which guarantees the
            // recipient's guid reference exists and info is non-null.
            var guidRef = World.WorldState.GetGuidReference(recipient)!;
            var parcelStore = World.WorldState.GetOrCreate<ParcelStore>(guidRef);
            var recipientMailbox = World.WorldState.GetOrCreate<Mailbox>(guidRef);
            var mboxString = merchant.GetLocalString("send_parcel_mailbox_message",
                ("$SENDER", Name), ("$ITEM", $"{itemObj.Name} (qty {quantity})"));

            recipientMailbox.ReceiveMessage(new Message(recipient, merchant.Name,
                merchant.GetLocalString("send_parcel_mailbox_subject", ("$NAME", Name)), mboxString));
            parcelStore.AddItem(Name, itemObj.Name, quantity);
            parcelStore.Save();
            if (info!.IsLoggedIn && Game.World.TryGetActiveUser(recipient, out var recipientUser))
            {
                recipientUser.SendSystemMessage(merchant.GetLocalString("send_parcel_system_msg",
                    ("$NAME", Name)));
                recipientUser.UpdateAttributes(StatUpdateFlags.UnreadMail);
            }

            PendingSellableQuantity = 0;
            PendingSendableParcel = null;
        }

        var packet = MerchantMenu(merchant, prompt, options);

        Enqueue(packet);
    }

    public void ShowMerchantReceiveParcelAccept(Merchant merchant)
    {
        var options = new MerchantOptions
        {
            Options = new List<MerchantDialogOption>()
        };

        var packet = MerchantMenu(merchant, merchant.GetLocalString("receive_parcel"), options);

        //TODO: Get Parcel from pending mail.
        ParcelStore.RemoveItem(this);

        Enqueue(packet);
    }

    public void ShowDepositGoldMenu(Merchant merchant)
    {
        var coins = "coin";
        if (Vault.CurrentGold > 1) coins = "coins";
        var prompt = merchant.GetLocalString("deposit_gold", ("$COINS", Vault.CurrentGold.ToString()),
            ("$REF", coins));

        var input = new MerchantInput();
        input.Id = (ushort)MerchantMenuItem.DepositGoldQuantity;

        var packet = MerchantMenu(merchant, prompt, input);

        Enqueue(packet);
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
            Id = (ushort)MerchantMenuItem.WithdrawGoldQuantity
        };

        var packet = MerchantMenu(merchant, prompt, input);

        Enqueue(packet);
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
        inventoryItems.Id = (ushort)MerchantMenuItem.DepositItemQuantity;

        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] is not { } item) continue;
            if (item.Exchangeable && item.Durability == item.MaximumDurability)
                inventoryItems.InventorySlots.Add(i);
        }

        var packet = MerchantMenu(merchant, merchant.GetLocalString("deposit_item"), inventoryItems);
        Enqueue(packet);
    }

    public void ShowDepositItemQuantity(Merchant merchant, byte slot)
    {
        if (Inventory[slot] is not { } item)
        {
            GameLog.UserActivityWarning("{Name}: ShowDepositItemQuantity: no item in slot {Slot}, ignoring", Name,
                slot);
            return;
        }

        PendingDepositSlot = slot;
        if (item.Stackable && item.Count > 0)
        {
            var input = new MerchantInput
            {
                Id = (ushort)MerchantMenuItem.DepositItem
            };

            var packet = MerchantMenu(merchant, merchant.GetLocalString("deposit_item_quantity", ("$QUANTITY", item.Count.ToString()), ("$ITEM", item.Name)), input);
            Enqueue(packet);
        }
        else
        {
            DepositItemConfirm(merchant, slot);
        }
    }

    public void DepositItemConfirm(Merchant merchant, byte slot, uint quantity = 1)
    {
        if (Inventory[slot] is not { } item)
        {
            GameLog.UserActivityWarning("{Name}: DepositItemConfirm: no item in slot {Slot}, ignoring", Name, slot);
            return;
        }

        var failure = false;

        if (quantity > ushort.MaxValue) quantity = ushort.MaxValue;

        var fee = (uint)(Math.Round(item.Value * 0.10, 0) * quantity);

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
            Vault.AddItem(item.Name, (ushort)quantity);
            if (item.Stackable && item.Count > quantity)
                RemoveItem(item.Name, (ushort)quantity);
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
            var packet = MerchantMenu(merchant, prompt, options);

            Enqueue(packet);
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
        inventoryItems.Id = (ushort)MerchantMenuItem.RepairItem;
        for (byte i = 1; i <= Inventory.Size; i++)
        {
            if (Inventory[i] is not { } item) continue;
            if (item.Durability != item.MaximumDurability) inventoryItems.InventorySlots.Add(i);
        }

        if (inventoryItems.InventorySlots.Count > 0)
        {
            var packet = MerchantMenu(merchant, merchant.GetLocalString("repair_item"), inventoryItems);
            Enqueue(packet);
        }
        else
        {
            var options = new MerchantOptions();
            options.Options = new List<MerchantDialogOption>();
            var packet = MerchantMenu(merchant, merchant.GetLocalString("repair_item_none"), options);
            Enqueue(packet);
        }
    }

    public void ShowRepairItem(Merchant merchant, byte slot)
    {
        if (Inventory[slot] is not { } item)
        {
            GameLog.UserActivityWarning("{Name}: ShowRepairItem: no item in slot {Slot}, ignoring", Name, slot);
            return;
        }

        var prompt = string.Empty;

        PendingRepairSlot = slot;

        PendingRepairCost =
            (uint)Math.Ceiling(item.Value - item.Durability / item.MaximumDurability * item.Value);

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
                Id = (ushort)MerchantMenuItem.RepairItemAccept,
                Text = "Yes"
            });
            options.Options.Add(new MerchantDialogOption
            {
                Id = (ushort)MerchantMenuItem.MainMenu,
                Text = "No"
            });
        }

        var packet = MerchantMenu(merchant, prompt, options);
        Enqueue(packet);
    }

    public void ShowRepairItemAccept(Merchant merchant)
    {
        // PendingRepairSlot is 0 when no repair is pending (its reset value); the slot may also
        // have been emptied since ShowRepairItem was displayed. Both are only reachable via
        // crafted or out-of-order packets, so log and ignore.
        if (PendingRepairSlot == 0 || Inventory[PendingRepairSlot] is not { } item)
        {
            GameLog.UserActivityWarning("{Name}: ShowRepairItemAccept: no pending repair item in slot {Slot}, ignoring",
                Name, PendingRepairSlot);
            return;
        }

        if (Gold < PendingRepairCost)
        {
            var options = new MerchantOptions
            {
                Options = new List<MerchantDialogOption>()
            };

            var packet = MerchantMenu(merchant, merchant.GetLocalString("repair_item_fail"), options);
            Enqueue(packet);
        }
        else
        {
            RemoveGold(PendingRepairCost);
            item.Durability = item.MaximumDurability;
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
            if (Inventory[i] is not { } item) continue;
            if (item.Durability != item.MaximumDurability)
            {
                PendingRepairCost +=
                    (uint)Math.Ceiling(item.Value - item.Durability / item.MaximumDurability * item.Value);
                repairableCount++;
            }
        }

        for (byte i = 1; i <= Equipment.Size; i++)
        {
            if (Equipment[i] is not { } item) continue;
            if (item.Durability != item.MaximumDurability)
            {
                PendingRepairCost +=
                    (uint)Math.Ceiling(item.Value - item.Durability / item.MaximumDurability * item.Value);
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
                    Id = (ushort)MerchantMenuItem.RepairAllItemsAccept,
                    Text = "Yes"
                });
                options.Options.Add(new MerchantDialogOption
                {
                    Id = (ushort)MerchantMenuItem.MainMenu,
                    Text = "No"
                });
            }

            var packet = MerchantMenu(merchant, prompt, options);
            Enqueue(packet);
        }
        else
        {
            var packet = MerchantMenu(merchant, merchant.GetLocalString("repair_item_none"), options);
            Enqueue(packet);
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
            var packet = MerchantMenu(merchant, merchant.GetLocalString("repair_item_fail"), options);
            Enqueue(packet);
        }
        else
        {
            RemoveGold(PendingRepairCost);
            PendingRepairCost = 0;
            for (byte i = 1; i <= Inventory.Size; i++)
            {
                if (Inventory[i] is not { } item) continue;
                if (item.Durability != item.MaximumDurability)
                {
                    item.Durability = item.MaximumDurability;
                    SendItemUpdate(item, i);
                }
            }

            for (byte i = 1; i <= Equipment.Size; i++)
            {
                if (Equipment[i] is not { } item) continue;
                if (item.Durability != item.MaximumDurability)
                {
                    item.Durability = item.MaximumDurability;
                    //SendItemUpdate(Equipment[i], i);
                    AddEquipment(item, i);
                }
            }

            var packet = MerchantMenu(merchant, merchant.GetLocalString("repair_all_items_success"), options);
            Enqueue(packet);
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
                Tile = (ushort)(0x8000 + worldItem.Properties.Appearance.Sprite),
                Color = (byte)worldItem.Properties.Appearance.Color,
                Description = worldItem.Properties.Vendor?.Description ?? "",
                Name = worldItem.Name,
                Price = item.Value
            });
            ;
        }

        merchantItems.Id = (ushort)MerchantMenuItem.WithdrawItemQuantity;


        var packet = MerchantMenu(merchant, merchant.GetLocalString("withdraw_item"), merchantItems);
        Enqueue(packet);
    }

    public void ShowWithdrawItemQuantity(Merchant merchant, string item)
    {
        var worldItem = World.WorldData.GetByIndex<Item>(item);
        if (worldItem.Stackable)
        {
            PendingWithdrawItem = item;

            var input = new MerchantInput();
            input.Id = (ushort)MerchantMenuItem.WithdrawItem;

            var packet = MerchantMenu(merchant, merchant.GetLocalString("withdraw_item_quantity"), input);
            Enqueue(packet);
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
                    {
                        if (Inventory[slot] is not { } stack) continue;
                        maxQuantity += stack.MaximumStack - stack.Count;
                    }
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
                Vault.RemoveItem(item, (ushort)quantity);
                AddItem(item, (ushort)quantity);
            }
            else
            {
                var itemObj = World.CreateItem(worldItem);
                Vault.RemoveItem(item);
                AddItem(itemObj);
            }

            Vault.Save();
            merchant.Say(prompt);
            SendCloseDialog();
        }
        else
        {
            var packet = MerchantMenu(merchant, prompt, options);

            Enqueue(packet);
        }
    }

    public void SendMessage(string message, MessageType type)
    {
        SendMessage(message, (byte)type);
    }

    public void SendMessage(string message, byte type) =>
        Enqueue(new SystemMessagePacket { MessageType = (SystemMessageType)type, Message = message });

    public void SendWorldMessage(string sender, string message)
    {
        // Hilariously we need to check the length of this string (total length needs
        // to be <67) otherwise we will cause a buffer overflow / crash on the client side
        // (For right now we assume the color code ({=c) isn't counted but that needs testing)
        var transmit = string.Format("{{=c[{0}] {1}", sender, message);
        if (transmit.Length > 67)
            // IT'S CHOPPIN TIME
            transmit = transmit.Substring(0, 67);
        Enqueue(new SystemMessagePacket { MessageType = SystemMessageType.Whisper, Message = transmit });
    }

    public void SendRedirect(World world, Login login, string name, bool logoff = true, int transmitDelay = 1200)
    {
        if (Client is not { EncryptionKey: { } encryptionKey } client)
        {
            GameLog.Warning("User {user}: redirect requested but client is gone or has no key, ignoring", Name);
            return;
        }

        client.Redirect(
            new Redirect(client, world, Game.Login, name, client.EncryptionSeed, encryptionKey), logoff,
            transmitDelay);
    }

    public bool IsHeartbeatValid(byte a, byte b) => Client?.IsHeartbeatValid(a, b) ?? false;

    public bool IsHeartbeatValid(int localTickCount, int clientTickCount) =>
        Client?.IsHeartbeatValid(localTickCount, clientTickCount) ?? false;

    public void Logoff(bool disconnect = false)
    {
        UpdateLogoffTime();
        Save(true);
        if (!disconnect)
        {
            if (Client is { EncryptionKey: { } encryptionKey } client)
            {
                var redirect = new Redirect(client, Game.World, Game.Login, "socket", client.EncryptionSeed,
                    encryptionKey);
                client.Redirect(redirect, true);
            }
        }
        else
        {
            try
            {
                Client?.Disconnect();
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
        if (Client is not { } client)
        {
            GameLog.Warning("User {user}: encryption parameter update requested but client is gone, ignoring", Name);
            return;
        }

        client.EncryptionKey = key;
        client.EncryptionSeed = seed;
        client.GenerateKeyTable(name);
    }

    private const string ExchangeCancelMessage = "Exchange was cancelled.";
    private const string ExchangeConfirmMessage = "You exchanged.";

    /// <summary>
    ///     Send an exchange initiation request to the client (open exchange window)
    /// </summary>
    /// <param name="requestor">The user requesting the trade</param>
    public void SendExchangeInitiation(User requestor)
    {
        if (!Condition.InExchange || !requestor.Condition.InExchange) return;
        Enqueue(new StartExchangeResponsePacket
        {
            OtherUserId = requestor.Id,
            OtherUserName = requestor.Name
        });
    }

    /// <summary>
    ///     Send a quantity prompt request to the client (when dealing with stacked items)
    /// </summary>
    /// <param name="itemSlot">The ItemObject slot containing a stacked ItemObject that will be split (client side)</param>
    public void SendExchangeQuantityPrompt(byte itemSlot)
    {
        if (!Condition.InExchange) return;
        Enqueue(new RequestExchangeAmountPacket { SourceSlot = itemSlot });
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
        Enqueue(new AddExchangeItemResponsePacket
        {
            RightSide = !source,
            ExchangeIndex = slot,
            Sprite = (ushort)(toAdd.Sprite + 0x8000),
            Color = toAdd.Color,
            Name = toAdd.Stackable && toAdd.Count > 1 ? $"{toAdd.Name} [{toAdd.Count}]" : toAdd.Name
        });
    }

    /// <summary>
    ///     Send an exchange update packet for gold to an active exchange participant.
    /// </summary>
    /// <param name="gold">The amount of gold to be added to the window.</param>
    /// <param name="source">Boolean indicating which "side" of the transaction will be updated (source / "left side" == true)</param>
    public void SendExchangeUpdate(uint gold, bool source = true)
    {
        if (!Condition.InExchange) return;
        Enqueue(new SetExchangeGoldResponsePacket { RightSide = !source, GoldAmount = gold });
    }

    /// <summary>
    ///     Send a cancellation notice for an exchange.
    /// </summary>
    /// <param name="source">The "side" responsible for cancellation (source / "left side" == true)</param>
    public void SendExchangeCancellation(bool source = true)
    {
        if (!Condition.InExchange) return;
        Enqueue(new CancelExchangeResponsePacket { RightSide = !source, Message = ExchangeCancelMessage });
    }

    /// <summary>
    ///     Send a confirmation notice for an exchange.
    /// </summary>
    /// <param name="source">The "side" responsible for confirmation (source / "left side" == true)</param>
    public void SendExchangeConfirmation(bool source = true)
    {
        if (!Condition.InExchange) return;
        Enqueue(new AcceptExchangeResponsePacket { RightSide = !source, Message = ExchangeConfirmMessage });
    }

    public void SendInventorySlot(byte slot)
    {
        if (Inventory[slot] is not { } item) return;
        Enqueue(new AddItemPacket
        {
            Slot = slot,
            Sprite = (ushort)(item.Sprite + 0x8000),
            Color = item.Color,
            Name = item.Name,
            Count = (uint)item.Count,
            Stackable = item.Stackable,
            MaxDurability = item.MaximumDurability,
            CurrentDurability = item.DisplayDurability
        });
    }

    public void SendInventory()
    {
        for (byte i = 1; i < Inventory.Size; i++)
        {
            if (Inventory[i] is not { } item) continue;
            if (item.Id == 0) Game.World.Insert(item);
            Enqueue(new AddItemPacket
            {
                Slot = i,
                Sprite = (ushort)(item.Sprite + 0x8000),
                Color = item.Color,
                Name = item.Name,
                Count = (uint)item.Count,
                Stackable = item.Stackable,
                MaxDurability = item.MaximumDurability,
                CurrentDurability = item.DisplayDurability
            });
        }
    }

    public void SendEquipment()
    {
        for (byte i = 1; i < Equipment.Size; i++)
            if (Equipment[i] is { } item)
                SendEquipItem(item, i);
    }

    public void SendSkills()
    {
        for (byte i = 0; i < SkillBook.Size; i++)
            if (SkillBook[i] is { Castable: not null } bookSlot)
                SendSkillUpdate(bookSlot, i);
    }

    public void SendSpells()
    {
        for (byte i = 0; i < SpellBook.Size; i++)
            if (SpellBook[i] is { Castable: not null } bookSlot)
                SendSpellUpdate(bookSlot, i);
    }

    public void ReapplyStatuses()
    {
        Statuses ??= new List<StatusSnapshot>();
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


    public bool IsInViewport(VisibleObject obj) =>
        Location.Map is { } map && map.EntityTree.GetObjects(GetViewport()).Contains(obj);


    public void SendSystemMessage(string msg)
    {
        LastSystemMessage = msg;
        Client?.SendMessage(msg, 3);
    }

    public void SendCombatLogMessage(ICombatEvent e)
    {
        CombatEvents.Push(e);
        if (GetCookie("combatlog") != "on") return;

        foreach (var line in (e.ToString() ?? string.Empty).Split("\n"))
            Client?.SendMessage(line, (byte)MessageType.Group);
    }


    public void CancelCasting()
    {
        if (!Condition.Casting) return;
        Enqueue(new CancelCastPacket());
        Condition.Casting = false;
    }

    #region Appearance settings

    [Persist] public RestPosition RestPosition { get; set; }

    [Persist] public SkinColor SkinColor { get; set; }

    [Persist] internal bool Transparent { get; set; }

    [Persist] public byte FaceShape { get; set; }

    [Persist] public LanternSize LanternSize { get; set; }

    [Persist] public NameDisplayStyle NameStyle { get; set; }

    [Persist] public bool DisplayAsMonster { get; set; }

    [Persist] public ushort MonsterSprite { get; set; }

    [Persist] public ushort HairStyle { get; set; }

    [Persist] public byte HairColor { get; set; }

    #endregion

    #region User

    // Some structs helping us to define various metadata
    public AuthInfo AuthInfo => Game.World.WorldState.GetOrCreateByGuid<AuthInfo>(Guid, Name);

    [Persist] public SkillBook SkillBook { get; private set; } = new();

    [Persist] public SpellBook SpellBook { get; private set; } = new();

    [Persist] public bool Grouping { get; set; }

    public UserStatus GroupStatus { get; set; }

    [Persist] public byte[] PortraitData { get; set; } = [];

    [Persist] public string ProfileText { get; set; } = string.Empty;

    public PendingLearnable? PendingLearnable { get; private set; }
    public ItemObject? PendingSendableParcel { get; private set; }
    public uint PendingSendableQuantity { get; private set; }
    public string? PendingParcelRecipient { get; private set; }
    public string? PendingBuyableItem { get; private set; }
    public int PendingBuyableQuantity { get; private set; }
    public byte PendingSellableSlot { get; private set; }
    public uint PendingSellableQuantity { get; private set; }
    public uint PendingMerchantOffer { get; private set; }
    public byte PendingDepositSlot { get; private set; }
    public string? PendingWithdrawItem { get; private set; }
    public byte PendingRepairSlot { get; private set; }
    public uint PendingRepairCost { get; private set; }

    /// <summary>
    ///     Drop every half-finished merchant interaction and close the menu. Each merchant flow
    ///     is multi-step and parks its state on the user between steps; abandoning one partway
    ///     used to leave that state set, because the abort path was an exception thrown out of
    ///     the handler. Anything the client sends that a flow can't act on should end the whole
    ///     interaction rather than leave a step's worth of state for a later one to pick up.
    /// </summary>
    public void AbortMerchantMenu()
    {
        PendingLearnable = null;
        PendingSendableParcel = null;
        PendingSendableQuantity = 0;
        PendingParcelRecipient = null;
        PendingBuyableItem = null;
        PendingBuyableQuantity = 0;
        PendingSellableSlot = 0;
        PendingSellableQuantity = 0;
        PendingMerchantOffer = 0;
        PendingDepositSlot = 0;
        PendingWithdrawItem = null;
        PendingRepairSlot = 0;
        PendingRepairCost = 0;

        SendCloseDialog();
    }

    [Persist] public List<KillRecord> RecentKills { get; private set; } = new();

    public Stack<ICombatEvent> CombatEvents { get; } = new(50);

    public List<SpokenEvent> MessagesReceived { get; private set; } = new();

    [Persist] public Guid GuildGuid { get; set; } = Guid.Empty;

    public List<string> UseCastRestrictions =>
        CurrentStatuses.SelectMany(selector: e => e.Value.UseCastRestrictions).ToList();

    public List<string> ReceiveCastRestrictions => CurrentStatuses
        .SelectMany(selector: e => e.Value.ReceiveCastRestrictions).ToList();

    private Nation? _nation;

    public Nation Nation
    {
        get => _nation ?? World.DefaultNation;
        set
        {
            _nation = value;
            Citizenship = value.Name;
        }
    }

    // Null/empty = never chose citizenship; distinguishable from any nation on save
    [Persist] private string? Citizenship { get; set; }

    public string NationName => Nation != null ? Nation.Name : string.Empty;

    [Persist] public Legend Legend = new();
    [Persist] public string Title = string.Empty;

    public AsyncDialogSession? ActiveDialogSession { get; set; }

    // Always assigned in _initializeUser (runtime state, not persisted); constructor initializer
    // can't reference 'this', so the invariant is upheld by the ctor rather than an inline default.
    public DialogState DialogState { get; set; } = null!;

    // Used by reactors and certain other objects to set an associate, so that functions called
    // from Lua later know who to "consult" for dialogs / etc.
    public IInteractable? LastAssociate { get; set; }

    public Exchange? ActiveExchange { get; set; }

    public bool IsAvailableForExchange => Condition.NoFlags;

    public ManufactureState? ManufactureState { get; set; }

    #endregion
}
