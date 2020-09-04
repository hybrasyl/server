using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Hybrasyl
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UuidReference :INotifyPropertyChanged
    {
        private readonly object _lock = new object();

        private string _name;
        private string _userUuid;
        private string _accountUuid;
        private string _mailboxUuid;
        private string _parcelStoreUuid;
        private string _vaultUuid;

        [JsonProperty]
        public string UserUuid
        { 
            get 
            {
                return _userUuid;
            } 
            set
            {
                _userUuid = value ?? "";
                OnPropertyChanged("UserUuid");
            } 
        }
        [JsonProperty]
        public string AccountUuid
        {
            get
            {
                return _accountUuid;
            }
            set
            {
                _accountUuid = value ?? "";
                OnPropertyChanged("AccountUuid");
            }
        }
        [JsonProperty]
        public string VaultUuid
        {
            get
            {
                return _vaultUuid;
            }
            set
            {
                _vaultUuid = value ?? "";
                OnPropertyChanged("VaultUuid");
            }
        }
        [JsonProperty]
        public string ParcelStoreUuid
        {
            get
            {
                return _parcelStoreUuid;
            }
            set
            {
                _parcelStoreUuid = value ?? "";
                OnPropertyChanged("ParcelStoreUuid");
            }
        }
        [JsonProperty]
        public string MailboxUuid
        {
            get
            {
                return _mailboxUuid;
            }
            set
            {
                _mailboxUuid = value ?? "";
                OnPropertyChanged("MailboxUuid");
            }
        }

        public string StorageKey => string.Concat(GetType(), ':', _name);

        public bool IsSaving;

        public event PropertyChangedEventHandler PropertyChanged;

        public UuidReference() { }

        public UuidReference(string name)
        {
            _name = name;
            //Save();
        }

        public void Save()
        {
            if (IsSaving) return;
            lock (_lock)
            {
                IsSaving = true;
                var cache = World.DatastoreConnection.GetDatabase();
                cache.Set(StorageKey, this);
                Game.World.WorldData.Set<UuidReference>(_name, this);
                IsSaving = false;
            }
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }
        protected void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            //Save();
        }
    }
}
