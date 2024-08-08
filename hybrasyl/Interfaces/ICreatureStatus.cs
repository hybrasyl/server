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

using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Statuses;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Interfaces;

public interface ICreatureStatus
{
    string Name { get; }
    Guid Guid { get; }
    List<string> Category { get; }
    string ActionProhibitedMessage { get; }
    double Duration { get; }
    double Tick { get; }
    DateTime Start { get; }
    DateTime LastTick { get; }
    ushort Icon { get; }

    StatusSnapshot Snapshot { get; }
    Creature Target { get; }
    Creature Source { get; }
    bool Expired { get; }
    double Intensity { get; set; }
    double Elapsed { get; }
    double Remaining { get; }
    double ElapsedSinceTick { get; }
    List<string> UseCastRestrictions { get; }
    List<string> ReceiveCastRestrictions { get; }
    SimpleStatusEffect OnStartEffect { get; }
    SimpleStatusEffect OnTickEffect { get; }
    SimpleStatusEffect OnRemoveEffect { get; }
    SimpleStatusEffect OnExpireEffect { get; }

    void OnStart(bool displaySfx = true);
    void OnTick();
    void OnEnd();
    void OnExpire();
}