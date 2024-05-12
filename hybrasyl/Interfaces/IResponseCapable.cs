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

namespace Hybrasyl.Interfaces;

public interface IResponseCapable : IWorldObject
{
    public Dictionary<string, string> Strings { get; set; }
    public Dictionary<string, string> Responses { get; set; }

    public string DefaultGetLocalString(string key) =>
        Strings.ContainsKey(key) ? Strings[key] : World.GetLocalString(key);

    public string DefaultGetLocalString(string key, params (string Token, string Value)[] replacements)
    {
        var str = DefaultGetLocalString(key);

        return replacements.Aggregate(str, func: (current, repl) => current.Replace(repl.Token, repl.Value));
    }
}