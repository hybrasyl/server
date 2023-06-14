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
using MoonSharp.Interpreter;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

[JsonObject(MemberSerialization.OptIn)]
public class ElementalModifiers
{
    [JsonProperty] private Dictionary<ElementType, double> Resistances = new();
    [JsonProperty] private Dictionary<ElementType, double> Augments = new();

    public ElementalModifiers()
    {
        foreach (ElementType type in Enum.GetValues(typeof(ElementType)))
        {
            Resistances[type] = 0.0;
            Augments[type] = 0.0;
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

    public double GetAugment(ElementType element) =>
        Augments.TryGetValue(element, out var value) ? value : 0.0;

    public void ModifyAugment(ElementType element, double mod)
    {
        if (Augments.TryGetValue(element, out var resistance))
            Augments[element] += mod;
        else
            Augments[element] = mod;
    }

    public void Apply(List<ElementalModifier> elementalModifiers)
    {
        foreach (var modifier in elementalModifiers)
        {
            switch (modifier.Type)
            {
                case ElementalModifierType.Resistance:
                    Resistances[modifier.Element] += modifier.Modifier;
                    break;
                case ElementalModifierType.Augment:
                    Augments[modifier.Element] += modifier.Modifier;
                    break;
            }
        }
    }

    public void Remove(List<ElementalModifier> elementalModifiers)
    {
        foreach (var modifier in elementalModifiers)
        {
            switch (modifier.Type)
            {
                case ElementalModifierType.Resistance:
                    Resistances[modifier.Element] -= modifier.Modifier;
                    break;
                case ElementalModifierType.Augment:
                    Augments[modifier.Element] -= modifier.Modifier;
                    break;
            }
        }
    }

    public static ElementalModifiers operator +(ElementalModifiers em1, ElementalModifiers em2)
    {
        var ret = new ElementalModifiers();
        foreach (var element in Enum.GetValues<ElementType>())
        {
            ret.Augments[element] = em1.GetAugment(element) + em2.GetAugment(element);
            ret.Resistances[element] = em1.GetResistance(element) + em2.GetResistance(element);
        }
        return ret;
    }

    public static ElementalModifiers operator -(ElementalModifiers em1, ElementalModifiers em2)
    {
        var ret = new ElementalModifiers();
        foreach (var element in Enum.GetValues<ElementType>())
        {
            ret.Augments[element] = em1.GetAugment(element) - em2.GetAugment(element);
            ret.Resistances[element] = em1.GetResistance(element) - em2.GetResistance(element);
        }
        return ret;
    }

    public bool Empty =>  NoAugments && NoResistances;

    public bool NoAugments =>
        Enum.GetValues(typeof(ElementType)).Cast<ElementType>().All(type => Augments[type] == 0);

    public bool NoResistances =>
        Enum.GetValues(typeof(ElementType)).Cast<ElementType>().All(type => Resistances[type] == 0);

}