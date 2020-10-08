using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl
{
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
        public string UserUuid { get; set; }

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
        public Int64 LoginFailureCount { get; set; }
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
        public string StorageKey => string.Concat(GetType(), ':', UserUuid);
        public bool IsSaving { get; set; }
        public bool IsGamemaster { get; set; } = false;

        public AuthInfo(string uuid)
        {
            UserUuid = uuid;
        }

        public string Username => Game.World.WorldData.GetNameByUuid(UserUuid);

        public void Save()
        {
            if (IsSaving) return;
            IsSaving = true;
            var cache = World.DatastoreConnection.GetDatabase();
            cache.Set(StorageKey, this);
            Game.World.WorldData.Set<AuthInfo>(UserUuid, this);
            IsSaving = false;
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
        }

        public bool IsPrivileged
        {
            get
            {
                return IsExempt || IsGamemaster || (Game.Config.Access?.IsPrivileged(Username) ?? false);
            }
        }

        public bool IsExempt
        {
            get
            {
                // This is hax, obvs, and so can you
                return Username == "Kedian"; // ||(Account != null && Account.email == "baughj@discordians.net");
            }
        }

    }
}
