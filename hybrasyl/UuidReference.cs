using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Hybrasyl
{
    public class UuidReference
    {
        private readonly object _lock = new object();

        public string UserName { get; set; }
        
        public string UserUuid { get; set; }
        
        public string AccountUuid { get; set; }
        
        public string VaultUuid { get; set; }
        
        public string ParcelStoreUuid { get; set; }
        
        public string MailboxUuid { get; set; }

        public string AuthInfoUuid { get; set; }
        
        public UuidReference() { }

        public UuidReference(string name)
        {
            UserName = name;
        }

        
    }
}
