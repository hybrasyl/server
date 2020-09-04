using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Hybrasyl
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UuidReference
    {
        private readonly object _lock = new object();

        private string _name;
        private string _userUuid;
        private string _accountUuid;
        private string _mailboxUuid;
        private string _parcelStoreUuid;
        private string _vaultUuid;

        
        public string UserUuid { get; set; }
        
        public string AccountUuid { get; set; }
        
        public string VaultUuid { get; set; }
        
        public string ParcelStoreUuid { get; set; }
        
        public string MailboxUuid { get; set; }
        

        //public string StorageKey => string.Concat(GetType(), ':', _name);

        public UuidReference() { }

        public UuidReference(string name)
        {
            _name = name;
        }

        
    }
}
