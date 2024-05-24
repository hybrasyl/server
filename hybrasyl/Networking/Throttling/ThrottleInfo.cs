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

namespace Hybrasyl.Networking.Throttling;

// TODO: add other types of throttles

public class ThrottleInfo
{
    public DateTime LastAccepted;
    public DateTime LastReceived;

    public DateTime PreviousAccepted;
    public DateTime PreviousReceived;
    public long SquelchCount;
    public long TotalAccepted;

    public long TotalReceived;
    public long TotalSquelched;
    public long TotalThrottled;

    public ThrottleInfo()
    {
        PreviousReceived = DateTime.MinValue;
        LastReceived = DateTime.MinValue;
        PreviousAccepted = DateTime.MinValue;
        LastAccepted = DateTime.MinValue;
        TotalReceived = 0;
        TotalSquelched = 0;
        TotalThrottled = 0;
        SquelchCount = 0;
        Throttled = false;
        Squelched = false;
    }

    public string SquelchObject { get; set; }

    public bool Squelched { get; set; }
    public bool Throttled { get; set; }
}