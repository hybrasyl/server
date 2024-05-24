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
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Subsystems.Scripting;

[MoonSharpUserData]
public class HybrasylReactor : HybrasylWorldObject
{
    public HybrasylReactor(Reactor obj) : base(obj) { }
    internal Reactor Reactor => WorldObject as Reactor;
    public HybrasylUser Origin => Reactor.Origin is User u ? new HybrasylUser(u) : null;

    public bool Blocking => Reactor.Blocking;

    public int Uses
    {
        get => Reactor.Uses;
        set => Reactor.Uses = value;
    }

    public bool Expired => Reactor.Expired;
    public long Expiration => ((DateTimeOffset) Reactor.Expiration).ToUnixTimeSeconds();

    /// <summary>
    ///     Make a reactor visible to a player if they have a specified cookie.
    /// </summary>
    /// <param name="cookieName">Name of the cookie to add to the list.</param>
    /// <param name="remove">If true, remove the cookie from the list.</param>
    public void VisibleToCookie(string cookieName, bool remove = false)
    {
        if (!remove)
            Reactor.VisibleToCookies.Add(cookieName);
        else
            Reactor.VisibleToCookies.Remove(cookieName);
    }

    /// <summary>
    ///     Make a reactor invisible to a player if they have a specified cookie.
    /// </summary>
    /// <param name="cookieName">Name of the cookie to add to the list.</param>
    /// <param name="remove">If true, remove the cookie from the list.</param>
    public void InvisibleToCookie(string cookieName, bool remove = false)
    {
        if (!remove)
            Reactor.InvisibleToCookies.Add(cookieName);
        else
            Reactor.InvisibleToCookies.Remove(cookieName);
    }
}