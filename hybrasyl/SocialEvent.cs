using Hybrasyl.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hybrasyl
{
    // TODO: this is a first pass quick and dirty implementation

    public enum SocialEventType
    {
        Class = 1,
        Mass = 2,
        WorldEvent = 3
    }

    public class SocialEvent
    {
        public ushort MapId;
        public DateTime StartTime;
        public DateTime EndTime;
        public User Origin;
        public byte StartX;
        public byte StartY;
        public string Subtype;
        public SocialEventType Type;
        public List<string> Speakers;

        public bool Active => EndTime != default && EndTime > StartTime;

        public SocialEvent(User origin, SocialEventType type, string subtype=null)
        {
            if (subtype == null)
                Subtype = "Unknown";
            else
                Subtype = subtype.ToLower();
            Origin = origin;
            MapId = origin.Map.Id;
            StartTime = DateTime.Now;
            EndTime = default;
            StartX = origin.X;
            StartY = origin.Y;
            Type = type;
            GameLog.UserActivityInfo($"Event beginning: {origin}, type {type} ({subtype}) at {origin.Map.Name}");
            // Lastly, we need to be able to talk
            Speakers = new List<string>() { origin.Name };
        }

        public void End()
        {
            EndTime = DateTime.Now;
            GameLog.UserActivityInfo($"Event ending: {Origin}, type {Type} ({Subtype}), elapsed time {(DateTime.Now - StartTime).TotalSeconds}s");
        }
    }
}
