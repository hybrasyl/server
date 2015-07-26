using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Objects
{

    public struct Spawnpoint
    {
        public string MapName { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
    }

    public class Nation
    {
        public string Name { get; set; }
        public byte Flag { get; set; }
        public List<Spawnpoint> SpawnPoints { get; set; }

        public Nation(string name, byte flag)
        {
            Name = name;
            Flag = flag;
            SpawnPoints = new List<Spawnpoint>();
        }
    }
}
