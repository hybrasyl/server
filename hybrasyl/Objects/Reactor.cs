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
using System.Threading.Tasks;
using Hybrasyl.Enums;

namespace Hybrasyl.Objects;

public class Reactor : VisibleObject
{
    public DateTime CreatedAt { get; set; }
    public DateTime Expiration { get; set; } = default;
    public VisibleObject Origin { get; set; } = null;
    public Guid CreatedBy { get; set; }

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
    public bool Blocking;
    public string Description;
    public string ScriptName;
    private bool _ready;

    public Reactor(Xml.Reactor reactor)
    {
        X = reactor.X;
        Y = reactor.Y;
    }

    public async Task OnExpiration()
    {
        while (!Expired)
            await Task.Delay(5000);
        Sprite = 0;
        Show();
        await Task.Delay(1000);
        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.RemoveReactor, Map.Id, X, Y, Guid));
    }

    public Reactor(byte x, byte y, Map map, string scriptName, int expiration = 0, string description = null, bool blocking = true) : base()
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
        Task.Run(async () => await OnExpiration());
    }

    public bool Expired => Uses != -1 && (Expiration < DateTime.Now || Uses == 0);

    public int Uses { get; set; } = -1;
        
    public void OnSpawn()
    {
        if (Expired) return;
        if (Game.World.ScriptProcessor.TryGetScript(ScriptName, out var myScript))
        {
            Script = myScript;
            Script.AssociateScriptWithObject(this);
            _ready = Script.Run(false);
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
        if (Ready)
            Script.ExecuteFunction("OnEntry", obj, this);
    }

    public override void AoiEntry(VisibleObject obj)
    {
        if (Expired) return;
        base.AoiEntry(obj);
        if (Ready)
            Script.ExecuteFunction("AoiEntry", obj, this);
    }

    public virtual void OnLeave(VisibleObject obj)
    {
        if (Expired) return;
        if (Ready && Script.HasFunction("OnLeave"))
            Script.ExecuteFunction("OnLeave", obj, this);
        if (obj is User user)
            user.LastAssociate = null;
    }

    public override void AoiDeparture(VisibleObject obj)
    {
        if (Expired) return;
        base.AoiDeparture(obj);
        if (Ready)
            Script.ExecuteFunction("AoiDeparture", obj, this);
    }

    public virtual void OnDrop(VisibleObject obj, VisibleObject dropped)
    {
        if (Expired) return;
        if (Ready)
            Script.ExecuteFunction("OnDrop", obj, this,
                dropped);
    }


    public void OnMove(VisibleObject obj)
    {
        if (Expired) return;
        if (Ready)
            Script.ExecuteFunction("OnMove", obj, this);
    }

    public void OnTake(VisibleObject obj, VisibleObject taken)
    {
        if (Expired) return;
        if (Ready)
            Script.ExecuteFunction("OnTake", obj, this, taken);
    }

    public override void ShowTo(VisibleObject obj)
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
            p.WriteUInt16((ushort) Sprite);

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
}