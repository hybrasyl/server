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
using System.Collections.Generic;
using System.Drawing;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Messaging;
using Hybrasyl.Scripting;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

public class VisibleObject : WorldObject, IVisible
{
    [JsonProperty]
    public LocationInfo Location { get; set; }
    // TODO: Clean these up later and simply use Location instead
    public Map Map { get => Location.Map;
        set => Location.Map = value;
    }
    public Xml.Direction Direction { get => Location.Direction;
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

    public ushort Sprite { get; set; }
    public string Portrait { get; set; }
    public string DisplayText { get; set; }

    // Whether or not to allow a ghost (a dead player) to interact with this object
    public bool AllowDead { get; set; }

    public string DeathPileOwner { get; set; }
    public List<string> ItemDropAllowedLooters { get; set; }
    public DateTime? ItemDropTime { get; set; }
    public ItemDropType ItemDropType { get; set; }

    public HashSet<User> viewportUsers { get; private set; }

    public VisibleObject()
    {
        DisplayText = string.Empty;
        DeathPileOwner = string.Empty;
        ItemDropAllowedLooters = new List<string>();
        ItemDropTime = null;
        viewportUsers = new HashSet<User>();
        Location = new LocationInfo();
        ItemDropType = ItemDropType.Normal;
        AllowDead = false;
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
        if (ItemDropType == ItemDropType.Normal) { return true; }
        else if (ItemDropType == ItemDropType.MonsterLootPile)
        {
            if (ItemDropAllowedLooters.Contains(username)) return true;
            if (timeDropDifference > Constants.MONSTER_LOOT_DROP_RANDO_TIMEOUT) return true;
        }
        else // (ItemDropType == ItemDropType.UserDeathPile)
        {
            if (DeathPileOwner.Equals(username)) return true;
            if (ItemDropAllowedLooters.Contains(username) && timeDropDifference > Constants.DEATHPILE_GROUP_TIMEOUT) return true;
            if (timeDropDifference > Constants.DEATHPILE_RANDO_TIMEOUT) return true;
        }
        error = "These items are cursed.";

        return false;
    }

    public virtual void OnClick(User invoker) { }
    public virtual void OnDeath() { }
    public virtual void OnDamage(DamageEvent damageEvent) { }
    public virtual void OnHeal(Creature healer, uint damage) { }
    public virtual void OnHear(SpokenEvent e) { }
    public virtual void ShowTo(IVisible target) { }

    public Rectangle GetBoundingBox()
    {
        return new Rectangle(X, Y, 1, 1);
    }

    public Rectangle GetViewport()
    {
        return new Rectangle((X - Constants.VIEWPORT_SIZE / 2),
            (Y - Constants.VIEWPORT_SIZE / 2), Constants.VIEWPORT_SIZE,
            Constants.VIEWPORT_SIZE);
    }

    public Rectangle GetShoutViewport()
    {
        return new Rectangle((X - Constants.VIEWPORT_SIZE),
            (Y - Constants.VIEWPORT_SIZE), Constants.VIEWPORT_SIZE * 2,
            Constants.VIEWPORT_SIZE * 2);
    }

    public int Distance(IVisible target)
    {
        return 3;
    }

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
    }

    public virtual void HideFrom(VisibleObject obj)
    {
    }

    public virtual void Teleport(ushort mapid, byte x, byte y)
    {
        if (!World.WorldData.ContainsKey<Map>(mapid)) return;
        Map?.Remove(this);
        GameLog.DebugFormat("Teleporting {0} to {1}.", Name, World.WorldData.Get<Map>(mapid).Name);
        World.WorldData.Get<Map>(mapid).Insert(this, x, y);
    }

    public virtual void Teleport(string name, byte x, byte y)
    {
        if (string.IsNullOrEmpty(name) || !World.WorldData.TryGetValueByIndex(name, out Map targetMap)) return;
        Map?.Remove(this);
        GameLog.DebugFormat("Teleporting {0} to {1}.", Name, targetMap.Name);
        targetMap.Insert(this, x, y);
    }

    public virtual void SendMapInfo()
    {
    }

    public virtual void SendLocation()
    {
    }

    public virtual void Say(string message, string from="")
    {
        foreach (var obj in Map.EntityTree.GetObjects(GetViewport()))
        {
            obj.OnHear(new SpokenEvent(this, message, from));
        }
    }

    public virtual void Shout(string message, string from="")
    {           
        foreach (var obj in Map.EntityTree.GetObjects(GetShoutViewport()))
        {
            obj.OnHear(new SpokenEvent(this, message, from, true));
        }
    }

    public virtual void Effect(short x, short y, ushort effect, short speed)
    {
        foreach (var user in viewportUsers)
        {
            user.SendEffect(x, y, effect, speed);
        }
    }

    public virtual void Effect(ushort effect, short speed)
    {
        foreach (var user in viewportUsers)
        {
            user.SendEffect(Id, effect, speed);
        }
    }

    public virtual void PlaySound(byte Id)
    {
        var soundPacket = new ServerPacketStructures.PlaySound() { Sound = Id };

        foreach (var user in viewportUsers)
        {
            var nPacket = (ServerPacket) soundPacket.Packet().Clone();
            user.Enqueue(nPacket);
        }
    }

    public Xml.Direction GetIntentDirection(Xml.IntentDirection intentDirection)
    {
        switch (intentDirection)
        {
            case Xml.IntentDirection.Back:
                if (Direction == Xml.Direction.North) return Xml.Direction.South;
                if (Direction == Xml.Direction.South) return Xml.Direction.North;
                if (Direction == Xml.Direction.East) return Xml.Direction.West;
                if (Direction == Xml.Direction.West) return Xml.Direction.East;
                break;
            case Xml.IntentDirection.Front:
                return Direction;
            case Xml.IntentDirection.Left:
                if (Direction == Xml.Direction.North) return Xml.Direction.West;
                if (Direction == Xml.Direction.South) return Xml.Direction.East;
                if (Direction == Xml.Direction.East) return Xml.Direction.North;
                if (Direction == Xml.Direction.West) return Xml.Direction.South;
                break;
            case Xml.IntentDirection.Right:
                if (Direction == Xml.Direction.North) return Xml.Direction.East;
                if (Direction == Xml.Direction.South) return Xml.Direction.West;
                if (Direction == Xml.Direction.East) return Xml.Direction.South;
                if (Direction == Xml.Direction.West) return Xml.Direction.North;
                break;
        }
        // We shouldn't be here
        return Xml.Direction.North;
    }


}