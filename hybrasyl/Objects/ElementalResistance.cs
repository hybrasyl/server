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
using System.Linq;
using Hybrasyl.Xml.Objects;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

[JsonObject(MemberSerialization.OptIn)]
public class ElementalResistance
{
    [JsonProperty] private Dictionary<ElementType, double> Resistances = new();

    public ElementalResistance()
    {
        foreach (ElementType type in Enum.GetValues(typeof(ElementType)))
        {
            Resistances[type] = 0.0;
        }
    }

    public double GetResistance(ElementType element) =>
        Resistances.TryGetValue(element, out var value) ? value : 0.0;

    public void ModifyResistance(ElementType element, double mod)
    {
        if (Resistances.TryGetValue(element, out var resistance))
            Resistances[element] += mod;
        else
            Resistances[element] = mod;
    }

    public void Apply(List<Xml.Objects.ElementalResistance> er1)
    {
        foreach (var resistance in er1)
        {
            Resistances[resistance.Type] += resistance.Modifier;
            GameLog.Info($"{resistance.Type}: {resistance.Modifier}");
        }
    }

    public void Remove(List<Xml.Objects.ElementalResistance> er1)
    {
        foreach (var resistance in er1)
        {
            Resistances[resistance.Type] -= resistance.Modifier;
        }
    }

    public void Apply(ElementalResistance er1)
    {
        foreach (ElementType type in Enum.GetValues(typeof(ElementType)))
        {
            Resistances[type] += er1.Resistances[type];
            GameLog.Info($"{type}: {Resistances[type]}");
        }
    }

    public void Remove(ElementalResistance er1)
    {
        foreach (ElementType type in Enum.GetValues(typeof(ElementType)))
        {
            Resistances[type] -= er1.Resistances[type];
        }
    }

    public bool Empty =>  Enum.GetValues(typeof(ElementType)).Cast<ElementType>().All(type => Resistances[type] == 0);
}