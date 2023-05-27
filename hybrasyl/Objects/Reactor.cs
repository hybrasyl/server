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
using System.Threading.Tasks;
using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Scripting;

namespace Hybrasyl.Objects;

public class Reactor : VisibleObject, IPursuitable
{
    private bool _ready;
    public bool Blocking;
    public string Description;
    public string ScriptName;

    public Reactor(Xml.Objects.Reactor reactor)
    {
        X = reactor.X;
        Y = reactor.Y;
        DialogSequences = new List<DialogSequence>();
    }

    public Reactor(byte x, byte y, MapObject map, string scriptName, int expiration = 0, string description = null,
        bool blocking = true)
    {
        X = x;
        Y = y;
        Map = map;
        Description = description;
        ScriptName = scriptName;
        Blocking = blocking;
        CreatedAt = DateTime.Now;
        if (expiration <= 0) return;
        Expiration = CreatedAt.AddSeconds(expiration);
        Task.Run(function: OnExpiration);
    }

    public DateTime CreatedAt { get; set; }
    public DateTime Expiration { get; set; }
    public VisibleObject Origin { get; set; }
    public Guid CreatedBy { get; set; }
    public bool OnDropCapable => Ready && !Expired && Script.HasFunction("OnDrop");
    public bool OnTakeCapable => Ready && !Expired && Script.HasFunction("OnTake");

    public bool Ready
    {
        get
        {
            if (!_ready)
                OnSpawn();
            return _ready;
        }
        set => _ready = value;
    }

    public bool Expired => Uses != -1 && (Expiration < DateTime.Now || Uses == 0);

    public int Uses { get; set; } = -1;
    public List<DialogSequence> Pursuits { get; set; } = new();
    public Dictionary<string, string> Strings { get; set; } = new();
    public Dictionary<string, string> Responses { get; set; } = new();
    public virtual List<DialogSequence> DialogSequences { get; set; } = new();
    public virtual Dictionary<string, DialogSequence> SequenceIndex { get; set; } = new();

    public override void ShowTo(IVisible obj)
    {
        if (Expired) return;
        if (obj is not User user) return;
        // TODO: improve, this isn't sufficient to work with Say/Shout currently
        var p = new ServerPacket(0x07);
        p.WriteUInt16(1);
        p.WriteUInt16(X);
        p.WriteUInt16(Y);
        p.WriteUInt32(Id);
        if (Sprite != 0)
            p.WriteUInt16((ushort) (Sprite + 0x8000));
        else
            p.WriteUInt16(Sprite);

        p.WriteByte(0); // random 1                                                                                                                                                                                                
        p.WriteByte(0); // random 2                                                                                                                                                                                                
        p.WriteByte(0); // random 3                                                                                                                                                                                                
        p.WriteByte(0); // unknown a                                                                                                                                                                                               
        p.WriteByte((byte) Direction);
        p.WriteByte(0); // unknown b                                                                                                                                                                                               
        p.WriteByte(0);
        p.WriteByte(0); // unknown d                                                                                                                                                                                               
        p.WriteByte((byte) MonsterType.Reactor);
        p.WriteString8(Name);
        user.Enqueue(p);
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
        if (Game.World.ScriptProcessor.TryGetScript(ScriptName, out var myScript))
        {
            Script = myScript;
            Script.AssociateScriptWithObject(this);
            _ready = Script.Run(false).Result == ScriptResult.Success;
        }
        else
        {
            GameLog.Error($"{Map}: reactor at {X},{Y}: reactor script {ScriptName} not found!");
        }

        // Now run our actual OnSpawn function
        if (_ready)
            Script.ExecuteFunction("OnSpawn");
    }

    public virtual void OnEntry(VisibleObject obj)
    {
        if (Expired) return;
        if (obj is User user)
        {
            user.LastAssociate = this;
            if (!user.Condition.Alive && !AllowDead)
                return;
        }

        if (!Ready) return;
        var wef = Script.ExecuteFunction("OnEntry",
            ScriptEnvironment.CreateWithOriginTargetAndSource(this, obj, obj));
    }

    public override void AoiEntry(VisibleObject obj)
    {
        if (Expired) return;
        base.AoiEntry(obj);
        if (!Ready) return;
        Script.ExecuteFunction("AoiEntry", ScriptEnvironment.CreateWithOriginTargetAndSource(this, obj, obj));
    }

    public virtual void OnLeave(VisibleObject obj)
    {
        if (Expired) return;
        if (Ready && Script.HasFunction("OnLeave"))
            Script.ExecuteFunction("OnLeave", ScriptEnvironment.CreateWithOriginTargetAndSource(this, obj, obj));
        if (obj is User user)
            user.LastAssociate = null;
    }

    public override void AoiDeparture(VisibleObject obj)
    {
        if (Expired) return;
        base.AoiDeparture(obj);
        if (!Ready) return;
        Script.ExecuteFunction("AoiDeparture", ScriptEnvironment.CreateWithOriginTargetAndSource(this, obj, obj));
    }

    public virtual void OnDrop(VisibleObject obj, VisibleObject dropped)
    {
        if (Expired) return;
        if (!Ready) return;
        var env = ScriptEnvironment.CreateWithOriginTargetAndSource(this, obj, dropped);
        env.Add("item", dropped);
        Script.ExecuteFunction("OnDrop", env);
    }

    public void OnMove(VisibleObject obj)
    {
        if (Expired) return;
        if (!Ready) return;
        Script.ExecuteFunction("OnMove", ScriptEnvironment.CreateWithOriginTargetAndSource(this, obj, obj));
    }

    public void OnTake(VisibleObject obj, VisibleObject taken)
    {
        if (Expired) return;
        if (!Ready) return;
        var env = ScriptEnvironment.CreateWithOriginTargetAndSource(this, obj, taken);
        env.Add("item", taken);
        Script.ExecuteFunction("OnTake", env);
    }
}