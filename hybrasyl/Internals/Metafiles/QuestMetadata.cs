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

using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Internals.Metafiles;

[MoonSharpUserData]
public class QuestMetadata
{
    public SortedSet<Class> AllowedClasses;
    public int Circle = 0;
    public string Id;
    public string Prerequisite; // who knows
    public string Result;
    public string Reward;
    public string Summary;
    public string Title;

    // Client expects a string like "123", "12345" etc

    public QuestMetadata()
    {
        AllowedClasses = new SortedSet<Class> { Class.Monk, Class.Priest, Class.Wizard, Class.Rogue, Class.Warrior };
    }

    public string Classes => AllowedClasses.Aggregate(string.Empty, func: (current, c) => current + (byte) c);

    public void AddClass(Class c) => AllowedClasses.Add(c);
}