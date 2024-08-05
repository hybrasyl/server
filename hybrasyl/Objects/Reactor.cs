﻿// This file is part of Project Hybrasyl.
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
using Hybrasyl.Networking;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Subsystems.Scripting;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hybrasyl.Objects;

public sealed class Reactor : VisibleObject, IPursuitable, ISpawnable
{
    public bool Blocking;
    public CreatureSnapshot Caster;
    public string Description;
    public string ScriptName;
    public string DisplayName { get; } = string.Empty;

    public bool VisibleToGroup;
    public bool VisibleToOwner;

    public Reactor(Xml.Objects.Reactor reactor, MapObject map)
    {
        X = reactor.X;
        Y = reactor.Y;
        DisplayName = reactor.DisplayName;
        ScriptName = reactor.Script;
        Blocking = reactor.Blocking;
        CreatedAt = DateTime.Now;
        Description = reactor.Description;
        AllowDead = reactor.AllowDead;
        Map = map; 
        Init();
    }

    public Reactor(byte x, byte y, MapObject map, CastableReactor reactor, Creature caster = null,
        string description = null)
    {
        X = x;
        Y = y;
        Map = map;
        ScriptName = reactor.Script;
        Blocking = reactor.Blocking;
        CreatedAt = DateTime.Now;
        Expiration = CreatedAt.AddSeconds(reactor.Expiration);
        ExpirationSeconds = reactor.Expiration;
        Caster = caster?.GetSnapshot();
        Description = description;
        VisibleToGroup = reactor.DisplayGroup;
        VisibleToOwner = reactor.DisplayOwner;
        VisibleToCookies = reactor.DisplayCookie?.Split(" ").ToList() ?? new List<string>();
        VisibleToStatuses = reactor.DisplayStatus?.Split(" ").ToList() ?? new List<string>();
        DisplayName = reactor.DisplayName;
        Init();
    }

    public List<string> VisibleToCookies { get; set; } = new();
    public List<string> InvisibleToCookies { get; set; } = new();
    public List<string> VisibleToStatuses { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime Expiration { get; set; }
    private int ExpirationSeconds { get; set; }
    public VisibleObject Origin { get; set; }
    public Guid CreatedBy { get; set; }
    public bool OnDropCapable => Ready && !Expired && Script.HasFunction("OnDrop");
    public bool OnTakeCapable => Ready && !Expired && Script.HasFunction("OnTake");

    public bool Ready { get; set; } = false;

    public bool Expired => Uses != -1 && (Expiration < DateTime.Now || Uses == 0);

    public int Uses { get; set; } = -1;
    public List<DialogSequence> Pursuits { get; set; } = new();
    public Dictionary<string, string> Strings { get; set; } = new();
    public Dictionary<string, string> Responses { get; set; } = new();
    public List<DialogSequence> DialogSequences { get; set; } = new();
    public Dictionary<string, DialogSequence> SequenceIndex { get; set; } = new();

    public override void ShowTo(IVisible obj)
    {
        if (!VisibleTo(obj)) return;
        if (obj is not User user) return;

        // TODO: improve, this isn't sufficient to work with Say/Shout currently
        var p = new ServerPacket(0x07);
        p.WriteUInt16(1);
        p.WriteUInt16(X);
        p.WriteUInt16(Y);
        p.WriteUInt32(Id);
        if (Sprite != 0)
            p.WriteUInt16((ushort)(Sprite + 0x8000));
        else
            p.WriteUInt16(Sprite);

        p.WriteByte(0); // random 1                                                                                                                                                                                                
        p.WriteByte(0); // random 2                                                                                                                                                                                                
        p.WriteByte(0); // random 3                                                                                                                                                                                                
        p.WriteByte(0); // unknown a                                                                                                                                                                                               
        p.WriteByte((byte)Direction);
        p.WriteByte(0); // unknown b                                                                                                                                                                                               
        p.WriteByte(0);
        p.WriteByte(0); // unknown d                                                                                                                                                                                               
        p.WriteByte((byte)MonsterType.Reactor);
        p.WriteString8(string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName);
        user.Enqueue(p);
    }

    private void Init()
    {
        DialogSequences = new List<DialogSequence>();
        CreatedAt = DateTime.Now;
        Expiration = DateTime.MaxValue;
        Name = $"{Map.Name}@{X},{Y}";
        if (ExpirationSeconds <= 0) return;
        Expiration = CreatedAt.AddSeconds(ExpirationSeconds);
        Task.Run(OnExpiration);
    }

    public bool VisibleTo(IVisible obj)
    {
        if (Expired) return false;
        if (obj is not User user) return false;
        var casterObj = Caster?.GetUserObject();
        if (VisibleToCookies.Any(user.HasCookie)) return true;
        if (InvisibleToCookies.Any(user.HasCookie)) return false;
        if (user.CurrentStatuses.Values.Any(predicate: x => VisibleToStatuses.Contains(x.Name))) return true;
        if (casterObj == null) return false;
        if (VisibleToOwner && user.Name == Caster.Name) return true;
        if (VisibleToGroup && (casterObj.Group?.Contains(user) ?? false)) return true;
        return false;
    }

    public async Task OnExpiration()
    {
        while (!Expired)
            await Task.Delay(5000);
        Sprite = 0;
        Show();
        await Task.Delay(1000);
        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcode.RemoveReactor, Guid));
    }

    public void OnSpawn()
    {
        if (Expired) return;

        if (string.IsNullOrWhiteSpace(ScriptName))
        {
            Ready = true;
            return;
        }

        if (Game.World.ScriptProcessor.TryGetScript(ScriptName, out var myScript))
        {
            Script = myScript;
            World.ScriptProcessor.RegisterScriptAttachment(myScript, this);
        }
        else
        {
            GameLog.Error($"{Map}: reactor at {X},{Y}: reactor script {ScriptName} not found!");
            return;
        }

        if (Script.HasFunction("OnSpawn"))
        {
            DialogSequences.Clear();
            // Now run our actual OnSpawn function
            var ret = Script.ExecuteFunction("OnSpawn", ScriptEnvironment.Create(("origin", this), ("source", this)));
            if (ret.Result == ScriptResult.Success)
                Ready = true;
        }
        else
            Ready = true;
    }

    public ScriptEnvironment GetBaseEnvironment(VisibleObject obj) =>
        ScriptEnvironment.Create(("origin", this), ("source", this), ("caster", Caster), ("target", obj));

    public void OnEntry(VisibleObject obj)
    {
        if (Expired) return;
        if (obj is User user)
        {
            user.LastAssociate = this;
            if (!user.Condition.Alive && !AllowDead)
                return;
        }

        if (!Ready) return;
        var wef = Script.ExecuteFunction("OnEntry", GetBaseEnvironment(obj));
    }

    public override void AoiEntry(VisibleObject obj)
    {
        if (Expired) return;
        base.AoiEntry(obj);
        if (!Ready) return;
        if (obj is User user)
            ShowTo(user);
        Script.ExecuteFunction("AoiEntry", GetBaseEnvironment(obj));
    }

    public void OnLeave(VisibleObject obj)
    {
        if (Expired) return;
        if (Ready && Script.HasFunction("OnLeave"))
            Script.ExecuteFunction("OnLeave", GetBaseEnvironment(obj));
        if (obj is User user)
            user.LastAssociate = null;
    }

    public override void AoiDeparture(VisibleObject obj)
    {
        if (Expired) return;
        base.AoiDeparture(obj);
        if (!Ready) return;
        Script.ExecuteFunction("AoiDeparture", GetBaseEnvironment(obj));
        if (obj is User u)
        {
            var removePacket = new ServerPacket(0x0E);
            removePacket.WriteUInt32(Id);
            u.Enqueue(removePacket);
        }
    }

    public void OnDrop(VisibleObject obj, VisibleObject dropped)
    {
        if (Expired) return;
        if (!Ready) return;
        var env = GetBaseEnvironment(obj);
        env.Add("item", dropped);
        Script.ExecuteFunction("OnDrop", env);
    }

    public void OnMove(VisibleObject obj)
    {
        if (Expired) return;
        if (!Ready) return;
        Script.ExecuteFunction("OnMove", GetBaseEnvironment(obj));
    }

    public void OnTake(VisibleObject obj, VisibleObject taken)
    {
        if (Expired) return;
        if (!Ready) return;
        var env = GetBaseEnvironment(obj);
        env.Add("item", taken);
        Script.ExecuteFunction("OnTake", env);
    }
}