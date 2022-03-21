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
using C3;
using Hybrasyl.Dialogs;
using Hybrasyl.Scripting;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

[JsonObject(MemberSerialization.OptIn)]
public class WorldObject : IQuadStorable
{
    /// <summary>
    /// The rectangle that defines the object's boundaries.
    /// </summary>
    public Rectangle Rect => new(X, Y, 1, 1);

    public DateTime CreationTime { get; set; }

    public bool HasMoved { get; set; }

    public virtual byte X { get; set; }
    public virtual byte Y { get; set; }
    public uint Id { get; set; }
    [JsonProperty(Order = 0)] public Guid Guid { get; set; } = Guid.NewGuid();

    [JsonProperty(Order = 0)]
    public virtual string Name { get; set; }

    public Script Script { get; set; }
    public Guid ServerGuid { get; set; }
    public World World => Game.GetServerByGuid<World>(ServerGuid);
    public ushort DialogSprite { get; set; }

    private Dictionary<string, dynamic> _ephemeralStore { get; set; }
    private readonly object _storeLock = new object();

    public void SetEphemeral(string key, dynamic value)
    {
        lock(_storeLock)
            _ephemeralStore[key] = value;
    }

    public virtual void OnInsert() {}

    public List<Tuple<string, dynamic>> GetEphemeralValues()
    {
        var ret = new List<Tuple<string, dynamic>>();
        lock (_storeLock)
        {
            foreach (var entry in _ephemeralStore)
                ret.Add(new Tuple<string,dynamic>(entry.Key, entry.Value));
        }
        return ret;
    }

    public dynamic GetEphemeral(string key)
    {
        lock (_storeLock)
            return _ephemeralStore.ContainsKey(key) ? _ephemeralStore[key] : null;
    }

    public bool ClearEphemeral(string key)
    {
        lock (_storeLock)
            return _ephemeralStore.ContainsKey(key) && _ephemeralStore.Remove(key);
    }

    public bool TryGetEphemeral(string key, out dynamic value)
    {
        lock (_storeLock)
            return _ephemeralStore.TryGetValue(key, out value);
    }

    public WorldObject()
    {
        Name = string.Empty;
        ResetPursuits();
        _ephemeralStore = new Dictionary<string, dynamic>();
        CreationTime = DateTime.Now;
    }

    public virtual void SendId()
    {
    }

    public virtual int Distance(WorldObject obj)
    {
        return Point.Distance(obj.X, obj.Y, X, Y);
    }

    public void ResetPursuits()
    {
        Pursuits = new List<DialogSequence>();
        DialogSequences = new List<DialogSequence>();
        SequenceCatalog = new Dictionary<string, DialogSequence>();
    }

    public virtual void AddPursuit(DialogSequence pursuit)
    {
        if (pursuit.Id == null)
        {
            // This is a local sequence, so assign it into the pursuit range and
            // assign an ID
            pursuit.Id = (uint)(Constants.DIALOG_SEQUENCE_SHARED + Pursuits.Count);
            Pursuits.Add(pursuit);
        }
        else
        {
            // This is a shared sequence
            Pursuits.Add(pursuit);
        }

        if (SequenceCatalog.ContainsKey(pursuit.Name))
        {
            GameLog.WarningFormat("Pursuit {0} is being overwritten", pursuit.Name);
            SequenceCatalog.Remove(pursuit.Name);

        }

        SequenceCatalog.Add(pursuit.Name, pursuit);

        if (pursuit.Id > Constants.DIALOG_SEQUENCE_SHARED)
        {
            pursuit.AssociateSequence(this);
        }
    }

    public virtual void RegisterDialogSequence(DialogSequence sequence)
    {
        sequence.Id = (uint)(Constants.DIALOG_SEQUENCE_PURSUITS + DialogSequences.Count);
        sequence.AssociateSequence(this);
        DialogSequences.Add(sequence);

        if (SequenceCatalog.ContainsKey(sequence.Name))
        {
            GameLog.WarningFormat("Dialog sequence {0} is being overwritten", sequence.Name);
            SequenceCatalog.Remove(sequence.Name);

        }
        SequenceCatalog.Add(sequence.Name, sequence);
            
    }

    public List<DialogSequence> Pursuits;
    public List<DialogSequence> DialogSequences;
    public Dictionary<string, DialogSequence> SequenceCatalog;
}