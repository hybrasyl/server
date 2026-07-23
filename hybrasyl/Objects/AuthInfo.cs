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

using Hybrasyl.Extensions;
using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Attributes;
using Hybrasyl.Servers;
using System;
using System.Data;

namespace Hybrasyl.Objects;

public enum UserState : byte
{
    Disconnected = 0,
    Login = 1,
    Redirect = 2,
    InWorld = 3
}

[Persistable]
[RedisType]
public class AuthInfo : IStateStorable
{
    private AuthInfo() { }

    public AuthInfo(Guid guid)
    {
        UserGuid = guid;
    }

    [Persist] public Guid UserGuid { get; set; }

    public bool IsLoggedIn => CurrentState == UserState.InWorld;

    [Persist] public UserState CurrentState { get; set; }

    [Persist] public DateTime LastStateChange { get; set; }

    public double StateChangeDuration => (DateTime.Now - LastStateChange).TotalMilliseconds;

    [Persist] public DateTime LastLogin { get; set; }

    [Persist] public DateTime LastLogoff { get; set; }

    [Persist] public DateTime LastLoginFailure { get; set; }

    // Persisted, but absent from records predating the field / before a login has occurred.
    [Persist] public string? LastLoginFrom { get; set; }

    [Persist] public string? LastLoginFailureFrom { get; set; }

    [Persist] public long LoginFailureCount { get; set; }

    [Persist] public DateTime CreatedTime { get; set; }

    [Persist] public bool FirstLogin { get; set; }

    // Nullable: VerifyPassword explicitly guards against a null hash.
    [Persist] public string? PasswordHash { get; set; }

    [Persist] public DateTime LastPasswordChange { get; set; }

    [Persist] public string? LastPasswordChangeFrom { get; set; }

    public string StorageKey => $"{GetType()}:{UserGuid}";
    public bool IsSaving { get; set; }
    public bool IsGamemaster { get; set; }

    public string Username => Game.World.WorldState.GetNameByGuid(UserGuid);

    public bool IsPrivileged =>
        IsExempt || IsGamemaster || (Game.ActiveConfiguration.Access?.IsPrivileged(Username) ?? false);

    public bool IsExempt =>
        // This is hax, obvs, and so can you
        Username == "Kedian";

    public void Save()
    {
        if (IsSaving) return;
        IsSaving = true;
        var cache = World.DatastoreConnection.GetDatabase();
        cache.Set(StorageKey, this);
        Game.World.WorldState.SetWithIndex(UserGuid, this, Username);
        IsSaving = false;
    }

    public bool VerifyPassword(string password)
    {
        if (PasswordHash is null) throw new DataException("Password hash should never be null");
        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }
}