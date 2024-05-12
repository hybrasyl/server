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

using System;
using System.Collections.Generic;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;

namespace Hybrasyl;
// TODO: this is a first pass quick and dirty implementation

public enum SocialEventType
{
    Class = 1,
    Mass = 2,
    WorldEvent = 3
}

public class SocialEvent : IStateStorable
{
    public DateTime EndTime;
    public ushort MapId;
    public User Origin;
    public List<string> Speakers;
    public DateTime StartTime;
    public byte StartX;
    public byte StartY;
    public string Subtype;
    public SocialEventType Type;

    public SocialEvent(User origin, SocialEventType type, string subtype = null)
    {
        if (subtype == null)
            Subtype = "Unknown";
        else
            Subtype = subtype.ToLower();
        Origin = origin;
        MapId = origin.Map.Id;
        StartTime = DateTime.Now;
        EndTime = default;
        StartX = origin.X;
        StartY = origin.Y;
        Type = type;
        GameLog.UserActivityInfo($"Event beginning: {origin}, type {type} ({subtype}) at {origin.Map.Name}");
        // Lastly, we need to be able to talk
        Speakers = new List<string> { origin.Name };
    }

    public bool Active => EndTime != default && EndTime > StartTime;

    public void End()
    {
        EndTime = DateTime.Now;
        GameLog.UserActivityInfo(
            $"Event ending: {Origin}, type {Type} ({Subtype}), elapsed time {(DateTime.Now - StartTime).TotalSeconds}s");
    }
}