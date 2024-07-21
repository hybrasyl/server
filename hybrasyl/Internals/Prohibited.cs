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
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Internals;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class Prohibited : Attribute
{
    public Prohibited(params object[] prohibited)
    {
        Flags = new List<PlayerFlags>();
        Conditions = new List<CreatureCondition>();

        foreach (var parameter in prohibited)
        {
            if (parameter.GetType() == typeof(PlayerFlags))
                Flags.Add((PlayerFlags) parameter);
            if (parameter.GetType() == typeof(CreatureCondition))
                Conditions.Add((CreatureCondition) parameter);
        }
    }

    public List<PlayerFlags> Flags { get; set; }
    public List<CreatureCondition> Conditions { get; set; }

    public bool Check(ConditionInfo condition)
    {
        foreach (var flag in Flags)
            if (condition.Flags.HasFlag(flag))
                return false;

        foreach (var cond in Conditions)
            if (condition.Conditions.HasFlag(cond))
                return false;

        return true;
    }
}