using Hybrasyl.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Objects
{

    [JsonObject(MemberSerialization.OptIn)]
    public class LocationInfo
    {
        private Map _map { get; set; }
        public Map Map
        {
            get { return _map; }
            set
            {
                _map = value;
                if (value != null)
                    _mapId = Map.Id;
            }
        }
        private ushort _mapId { get; set; }
        [JsonProperty]
        public ushort MapId
        {
            get { if (Map != null) return Map.Id; else return _mapId; }
            set
            {
                if (Game.World.WorldData.TryGetValue(value, out Map map))
                    Map = map;
                _mapId = value;
            }
        }
        [JsonProperty]
        public Direction Direction { get; set; }
        [JsonProperty]
        public byte X { get; set; }
        [JsonProperty]
        public byte Y { get; set; }
        [JsonProperty]
        public bool WorldMap { get; set; }
    }
}
