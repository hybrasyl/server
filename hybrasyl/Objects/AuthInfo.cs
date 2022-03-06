using Newtonsoft.Json;
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

[JsonObject(MemberSerialization.OptIn)]
[RedisType]
public class AuthInfo
{
    [JsonProperty]
    public Guid UserGuid { get; set; }

    public bool IsLoggedIn => CurrentState == UserState.InWorld;

    [JsonProperty]
    public UserState CurrentState { get; set; }

    [JsonProperty]
    public DateTime LastStateChange { get; set; }

    public double StateChangeDuration => (DateTime.Now - LastStateChange).TotalMilliseconds;

    [JsonProperty]
    public DateTime LastLogin { get; set; }
    [JsonProperty]
    public DateTime LastLogoff { get; set; }
    [JsonProperty]
    public DateTime LastLoginFailure { get; set; }
    [JsonProperty]
    public string LastLoginFrom { get; set; }
    [JsonProperty]
    public string LastLoginFailureFrom { get; set; }
    [JsonProperty]
    public long LoginFailureCount { get; set; }
    [JsonProperty]
    public DateTime CreatedTime { get; set; }
    [JsonProperty]
    public bool FirstLogin { get; set; }
    [JsonProperty]
    public string PasswordHash { get; set; }
    [JsonProperty]
    public DateTime LastPasswordChange { get; set; }
    [JsonProperty]
    public string LastPasswordChangeFrom { get; set; }
    public string StorageKey => $"{GetType()}:{UserGuid}";
    public bool IsSaving { get; set; }
    public bool IsGamemaster { get; set; }

    public AuthInfo(Guid guid)
    {
        UserGuid = guid;
    }

    public string Username => Game.World.WorldData.GetNameByGuid(UserGuid);

    public void Save()
    {
        if (IsSaving) return;
        IsSaving = true;
        var cache = World.DatastoreConnection.GetDatabase();
        cache.Set(StorageKey, this);
        Game.World.WorldData.SetWithIndex(UserGuid, this, Username);
        IsSaving = false;
    }

    public bool VerifyPassword(string password)
    {
        if (PasswordHash is null) throw new DataException("Password hash should never be null");
        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }

    public bool IsPrivileged => IsExempt || IsGamemaster || (Game.Config.Access?.IsPrivileged(Username) ?? false);

    public bool IsExempt =>
        // This is hax, obvs, and so can you
        Username == "Kedian";
}