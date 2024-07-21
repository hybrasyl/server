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

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking.ServerPackets;
using Hybrasyl.Subsystems.Messaging;
using Hybrasyl.Subsystems.Scripting;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Hybrasyl.Objects;

public class VisibleObject : WorldObject, IVisible
{
    // TODO: Clean these up later and simply use Location instead
    public MapObject Map
    {
        get => Location.Map;
        set => Location.Map = value;
    }

    public Direction Direction
    {
        get => Location.Direction;
        set => Location.Direction = value;
    }

    public override byte X
    {
        get => Location?.X ?? 0;
        set => Location.X = value;
    }

    public override byte Y
    {
        get => Location?.Y ?? 0;
        set => Location.Y = value;
    }

    // Whether or not to allow a ghost (a dead player) to interact with this object
    public bool AllowDead { get; set; }

    public string DeathPileOwner { get; set; } = string.Empty;
    public List<string> ItemDropAllowedLooters { get; set; } = new();
    public DateTime? ItemDropTime { get; set; }
    public ItemDropType ItemDropType { get; set; } = ItemDropType.Normal;

    public HashSet<User> viewportUsers { get; private set; } = new();

    public SpokenEvent LastHeard { get; set; }

    [JsonProperty] public LocationInfo Location { get; set; } = new();

    public ushort Sprite { get; set; }
    public string Portrait { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public virtual void ShowTo(IVisible target) { }

    public int Distance(IVisible target) => Point.Distance(this, target);

    public static Direction Opposite(Direction d)
    {
        return d switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            Direction.East => Direction.West,
            _ => throw new ArgumentOutOfRangeException(nameof(d), d, null)
        };
    }

    public virtual void AoiEntry(VisibleObject obj)
    {
        if (obj is User user)
            viewportUsers.Add(user);
    }

    public virtual void AoiDeparture(VisibleObject obj)
    {
        if (obj is User user)
            viewportUsers.Remove(user);
    }

    public bool CanBeLooted(string username, out string error)
    {
        error = string.Empty;
        // Let's just be sure here
        if (!(this is Gold || this is ItemObject))
        {
            error = "You can't do that.";
            return false;
        }

        if (ItemDropTime == null)
            // Item was inserted by the system, a script, or some other mechanism
            return true;

        var timeDropDifference = (DateTime.Now - ItemDropTime.Value).TotalSeconds;

        // Check if the item is a normal dropped item, monster loot or deathpile
        if (ItemDropType == ItemDropType.Normal) return true;

        if (ItemDropType == ItemDropType.MonsterLootPile)
        {
            if (ItemDropAllowedLooters.Contains(username)) return true;
            if (timeDropDifference > Game.ActiveConfiguration.Constants.MonsterLootDropTimeout) return true;
        }
        else // (ItemDropType == ItemDropType.UserDeathPile)
        {
            if (DeathPileOwner.Equals(username)) return true;
            if (ItemDropAllowedLooters.Contains(username) &&
                timeDropDifference > Game.ActiveConfiguration.Constants.DeathpileOtherTimeout) return true;
            if (timeDropDifference > Game.ActiveConfiguration.Constants.DeathpileGroupTimeout) return true;
        }

        error = "These items are cursed.";

        return false;
    }

    public virtual void OnClick(User invoker) { }
    public virtual void OnDeath() { }
    public virtual void OnDamage(DamageEvent damageEvent) { }
    public virtual void OnHeal(HealEvent healEvent) { }

    public virtual void OnHear(SpokenEvent e)
    {
        LastHeard = e;
        if (Script == null) return;
        var env = ScriptEnvironment.Create(("text", e.Message), ("shout", e.Shout),
            ("origin", this), ("source", e.Speaker));

        env.Add("event", e);
        Script.ExecuteFunction("OnHear", env);
    }

    public Rectangle GetBoundingBox() => new(X, Y, 1, 1);

    public Rectangle GetViewport() =>
        new(
            X - Game.ActiveConfiguration.Constants.ViewportSize / 2,
            Y - Game.ActiveConfiguration.Constants.ViewportSize / 2,
            Game.ActiveConfiguration.Constants.ViewportSize + 1,
            Game.ActiveConfiguration.Constants.ViewportSize + 1);

    public Rectangle GetShoutViewport() =>
        new(
            X - Game.ActiveConfiguration.Constants.ViewportSize,
            Y - Game.ActiveConfiguration.Constants.ViewportSize,
            Game.ActiveConfiguration.Constants.ViewportSize * 2 + 1,
            Game.ActiveConfiguration.Constants.ViewportSize * 2 + 1);

    public virtual void Show()
    {
        var withinViewport = Map.EntityTree.GetObjects(GetViewport());
        GameLog.DebugFormat("WithinViewport contains {0} objects", withinViewport.Count);

        foreach (var obj in withinViewport)
        {
            GameLog.DebugFormat("Object type is {0} and its name is {1}", obj.GetType(), obj.Name);
            obj.AoiEntry(this);
        }
    }

    public virtual void Hide()
    {
        var withinViewport = Map.EntityTree.GetObjects(GetViewport());
        GameLog.DebugFormat("WithinViewport contains {0} objects", withinViewport.Count);

        foreach (var obj in withinViewport)
        {
            GameLog.DebugFormat("Object type is {0} and its name is {1}", obj.GetType(), obj.Name);
            obj.AoiDeparture(this);
        }
    }

    public virtual void HideFrom(VisibleObject obj) { }

    public virtual void Teleport(ushort mapid, byte x, byte y)
    {
        if (!World.WorldState.ContainsKey<MapObject>(mapid)) return;
        Map?.Remove(this);
        GameLog.DebugFormat("Teleporting {0} to {1}", Name, World.WorldState.Get<MapObject>(mapid).Name);
        World.WorldState.Get<MapObject>(mapid).Insert(this, x, y);
    }

    public virtual void Teleport(string name, byte x, byte y)
    {
        if (string.IsNullOrEmpty(name) || !World.WorldState.TryGetValueByIndex(name, out MapObject targetMap))
        {
            GameLog.Warning($"Teleport to nonexistent map {name}");
            return;
        }

        Map?.Remove(this);
        GameLog.DebugFormat("Teleporting {0} to {1}", Name, targetMap.Name);
        targetMap.Insert(this, x, y);
    }

    public virtual void SendMapInfo(int transmitDelay = 0) { }

    public virtual void SendLocation(int transmitDelay = 0) { }

    public virtual void Say(string message, string from = "")
    {
        foreach (var obj in Map.EntityTree.GetObjects(GetViewport())) obj.OnHear(new SpokenEvent(this, message, from));
    }

    public virtual void Shout(string message, string from = "")
    {
        foreach (var obj in Map.EntityTree.GetObjects(GetShoutViewport()))
            obj.OnHear(new SpokenEvent(this, message, from, true));
    }

    public virtual void Effect(short x, short y, ushort effect, short speed)
    {
        foreach (var user in viewportUsers) user.SendEffect(x, y, effect, speed);
    }

    public virtual void Effect(ushort effect, short speed)
    {
        foreach (var user in viewportUsers) user.SendEffect(Id, effect, speed);
    }

    public virtual void PlaySound(byte Id)
    {
        var soundPacket = new PlaySound { Sound = Id };

        foreach (var user in viewportUsers)
        {
            var nPacket = soundPacket.Packet().Clone();
            user.Enqueue(nPacket);
        }
    }

    public Direction GetIntentDirection(IntentDirection intentDirection)
    {
        switch (intentDirection)
        {
            case IntentDirection.Back:
                if (Direction == Direction.North) return Direction.South;
                if (Direction == Direction.South) return Direction.North;
                if (Direction == Direction.East) return Direction.West;
                if (Direction == Direction.West) return Direction.East;
                break;
            case IntentDirection.Front:
                return Direction;
            case IntentDirection.Left:
                if (Direction == Direction.North) return Direction.West;
                if (Direction == Direction.South) return Direction.East;
                if (Direction == Direction.East) return Direction.North;
                if (Direction == Direction.West) return Direction.South;
                break;
            case IntentDirection.Right:
                if (Direction == Direction.North) return Direction.East;
                if (Direction == Direction.South) return Direction.West;
                if (Direction == Direction.East) return Direction.South;
                if (Direction == Direction.West) return Direction.North;
                break;
        }

        // We shouldn't be here
        return Direction.North;
    }
}