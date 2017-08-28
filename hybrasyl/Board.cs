using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace Hybrasyl
{
     
    public class MessageStoreLocked : Exception
    {

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MessageStore : IEnumerable<Message>
    {
        [JsonProperty] public string Name;   
        [JsonProperty] public string DisplayName;
        [JsonProperty] public List<Message> Messages;
        [JsonProperty] public string Guid;
        [JsonProperty] public short CurrentId;
        public int Id;

        public bool Full => Messages.Count == short.MaxValue;
        private int _lock; 
        public bool IsSaving;
        public bool IsLocked => _lock == 1;

        public MessageStore(string name, string displayName="")
        {
            Name = name;
            IsSaving = false;
            Guid = System.Guid.NewGuid().ToString();
            CurrentId = 0;
            _lock = 0;
            Messages = new List<Message>();
            DisplayName = displayName != "" ? displayName : Name;
        }

        public string StorageKey => string.Concat(GetType(), ':', Name.ToLower());

        public void Save()
        {
            if (IsSaving) return;
            IsSaving = true;
            var cache = World.DatastoreConnection.GetDatabase();
            cache.Set(StorageKey, JsonConvert.SerializeObject(this));
            IsSaving = false;
        }

        public void Lock()
        {
            if (_lock == 0)
                Interlocked.Exchange(ref _lock, 1);
            else
                throw new MessageStoreLocked();          
        }

        public void Unlock()
        {
            if (_lock == 1)
                Interlocked.Exchange(ref _lock, 0);
        }

        public virtual bool ReceiveMessage(Message newMessage)
        {
            if (IsLocked || Full == true)
            {
                return false;
            }
            CurrentId++;
            newMessage.Id = CurrentId;
            Messages.Add(newMessage);
            Save();
            return true;
        }

        public virtual void DeleteMessage(int id)
        {
            Messages[id].Deleted = true;
        }

        public IEnumerator<Message> GetEnumerator()
        {
            return Messages.Take(Constants.MESSAGE_RETURN_SIZE).Where(message => !message.Deleted).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
 
        public void Cleanup()
        {
            // Lock the mailbox during this process.
            Lock();
            // Try to remove deleted messages first.
            Messages.RemoveAll(m => m.Deleted == true);
            // If we still are within 100 messages of the maximum, now start deleting older messages
            if (Messages.Count > short.MaxValue - 100)
            {
                // Delete up to 10% of the mailbox, consisting of the oldest messages
                Messages.RemoveRange(0, Messages.Count/10);
            }
            // Renumber mailbox.
            // This sucks, but I'm not sure how to make it better given the client restrictions.
            CurrentId = 0;
            foreach (var message in Messages)
            {
                message.Id = CurrentId;
                CurrentId++;
            }
            // Unlock and save.
            Unlock();
            Save();
        }

        public ServerPacket RenderToPacket(bool isClick = false)
        {
            var response = new ServerPacket(0x31);
            if (this is Mailbox)
            {
                response.WriteByte(0x04); // 0x02 - public, 0x04 - mail
                response.WriteByte(0x01); // ??? - needs to be odd number unless board in world has been clicked
                response.WriteUInt16(0); // board ID;
                response.WriteString8("Mail");
                response.WriteByte(Math.Min((byte)this.Count(),(byte)Constants.MESSAGE_RETURN_SIZE));
                foreach (var message in this)
                {
                    response.WriteBoolean(!message.Read);
                    response.WriteInt16((short)message.Id);
                    response.WriteString8(message.Sender);
                    response.WriteByte((byte)message.Created.Month);
                    response.WriteByte((byte)message.Created.Day);
                    response.WriteString8(message.Subject);
                }

            }
            else if (this is Board)
            {
                // boardId 0 - get mail messages
                response.WriteByte(0x02); // 0x02 - public, 0x04 - mail
                response.WriteByte((byte)(isClick == true ? 0x02 : 0x01));
                // ??? - needs to be odd number unless board in world has been clicked
                response.WriteUInt16((ushort) Id); // board ID;
                response.WriteString8(DisplayName);
                response.WriteByte(Math.Min((byte) this.Count(),
                    (byte) Constants.MESSAGE_RETURN_SIZE));
                foreach (var message in this)
                {
                    response.WriteBoolean(message.Highlighted);
                    response.WriteInt16((short) message.Id);
                    response.WriteString8(message.Sender);
                    response.WriteByte((byte) message.Created.Month);
                    response.WriteByte((byte) message.Created.Day);
                    response.WriteString8(message.Subject);
                }
            }
            return response;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Mailbox : MessageStore
    {
        public Mailbox(string name) : base(name) { }

        public bool HasUnreadMessages
        {
            get { return Messages.Where(m => m.Read == false).Count() > 0; }
        }
    }

    public enum BoardAccessLevel
    {
        Read,
        Write,       // N.B. Write implies read
        Moderate,    // Moderator implies r/w access
    };

    public class Board : MessageStore
    {
        [JsonProperty] public bool Global;
        [JsonProperty] public HashSet<string> ModeratorList { get; private set; }
        [JsonProperty] public HashSet<string> ReaderList { get; private set; }
        [JsonProperty] public HashSet<string> WriterList { get; private set; }
        [JsonProperty] public HashSet<string> BlockList { get; private set; }

        private void InitializeStorage()
        {
            ModeratorList = new HashSet<string>();
            ReaderList = new HashSet<string>();
            WriterList = new HashSet<string>();
            BlockList = new HashSet<string>();
        }

        public Board(string name) : base(name)
        {
            Global = false;
            InitializeStorage();
        }
       
        public override bool ReceiveMessage(Message newMessage)
        {
            if (CheckAccessLevel(newMessage.Sender, BoardAccessLevel.Write))
            {
                return base.ReceiveMessage(newMessage);
            }
            return false;
        }

        public bool CheckAccessLevel(string charName, BoardAccessLevel level)
        {
            var checkname = charName.ToLower();

            if (ModeratorList.Contains(checkname))
                return true;

            if (BlockList.Contains(checkname))
                return false;

            switch (level)
            {
                case BoardAccessLevel.Read:
                    return ReaderList.Count == 0 || ReaderList.Contains(checkname) || WriterList.Contains(checkname);
                case BoardAccessLevel.Write:
                    return WriterList.Count == 0 || WriterList.Contains(checkname);
                case BoardAccessLevel.Moderate:
                    return ModeratorList.Contains(checkname);
            }
            return false;
        }

        public void SetAccessLevel(string charName, BoardAccessLevel level)
        {
            if (level == BoardAccessLevel.Read)
                ReaderList.Add(charName.ToLower());
            if (level == BoardAccessLevel.Moderate)
                ModeratorList.Add(charName.ToLower());
            if (level == BoardAccessLevel.Write)
                WriterList.Add(charName.ToLower());
        }


    }

    public class Message
    {
        [JsonProperty] public string Subject;
        [JsonProperty] public string Body;
        [JsonProperty] public string Sender;
        [JsonProperty] public string Recipient;
        [JsonProperty] public DateTime Created;
        [JsonProperty] public bool Highlighted;
        [JsonProperty] public bool Deleted;
        private bool _read;

        [JsonProperty]
        public bool Read
        {
            get { return _read; }
            set
            {
                _read = value;
                if (value == true)
                    ReadTime = DateTime.Now;
            }
        }

        [JsonProperty] public DateTime ReadTime;
        [JsonProperty] public string Guid;
        [JsonProperty] public int Id;

        public Message(string recipient, string sender, string subject, string body)
        {
            Created = DateTime.Now;
            Recipient = recipient;
            Sender = sender;
            Subject = subject;
            Body = body;
            Deleted = false;
            Highlighted = false;
            Guid = System.Guid.NewGuid().ToString();
            Read = false;
        }

        public ServerPacket RenderToPacket(bool Mailbox = true)
        {
            var response = new ServerPacket(0x31);
            // Functionality unknown but necessary
            if (Mailbox)
            {
                response.WriteByte(0x05);  
                response.WriteByte(0x03);
            }
            else
            {
                response.WriteByte(0x03);
                response.WriteByte(0x00);
            }
            response.WriteBoolean(Mailbox || Highlighted); // Mailbox messages are always "read"
            response.WriteUInt16((ushort)Id);
            response.WriteString8(Sender);
            response.WriteByte((byte)Created.Month);
            response.WriteByte((byte)Created.Day);
            response.WriteString8(Subject);
            response.WriteString16(Body);

            return response;
        }

    }
}
