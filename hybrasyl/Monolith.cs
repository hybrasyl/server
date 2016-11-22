using Hybrasyl.Objects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hybrasyl.Creatures;
using Creature = Hybrasyl.Creatures.Creature;

namespace Hybrasyl
{
    //This class is defined to control the mob spawning thread.
    internal class Monolith
    {
        private static readonly ManualResetEvent AcceptDone = new ManualResetEvent(false);
        private static Random _random;

        public static readonly ILog Logger =
            LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Dictionary<int, SpawnGroup> _spawnGroups;
        private readonly Dictionary<string, Map> _maps;
        private readonly Dictionary<string, Creature> _creatures;
        

        internal Monolith()
        {
            _spawnGroups = Game.World.SpawnGroups;
            _maps = Game.World.MapCatalog;
            _creatures = Game.World.Creatures;
            _random = new Random();
        }

        public void Start()
        {
            try
            {
                foreach (var map in _spawnGroups.Values.SelectMany(spawnGroup => spawnGroup.Maps))
                {
                    //set extension properties on startup
                    map.Id = _maps.Values.Single(x => x.Name == map.Name).Id;
                    map.LastSpawn = DateTime.Now;
                }


                while (true)
                {
                    foreach (var spawnGroup in _spawnGroups.Values)
                    {
                        Spawn(spawnGroup);
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        public void Spawn(SpawnGroup spawnGroup)
        {
            foreach (var map in spawnGroup.Maps)
            {
                var spawnMap = Game.World.Maps[(ushort) map.Id];
                var monsterList = spawnMap.Objects.OfType<Monster>().ToList();
                var monsterCount = monsterList.Count;

                if (monsterCount > map.Limit) continue;
                if (!(map.LastSpawn.AddSeconds(map.Interval) < DateTime.Now)) continue;

                map.LastSpawn = DateTime.Now;

                
                var thisSpawn = _random.Next(map.MinSpawn, map.MaxSpawn);

                for (var i = 0; i < thisSpawn; i++)
                {
                    var idx = _random.Next(0, spawnGroup.Spawns.Count - 1);
                    var spawn = spawnGroup.Spawns[idx];
                    var creature = _creatures.Values.Single(x => x.Name == spawn.Base);

                    var baseMob = new Monster(creature, spawn, map.Id);
                    var mob = (Monster)baseMob.Clone();
                    
                    var xcoord = 0;
                    var ycoord = 0;
                    do
                    {
                        xcoord = _random.Next(0, spawnMap.X -1);
                        ycoord = _random.Next(0, spawnMap.Y -1);
                    } while (spawnMap.IsWall[xcoord, ycoord]);
                    mob.X = (byte) xcoord;
                    mob.Y = (byte) ycoord;
                    mob.Id = Convert.ToUInt32(_random.Next(0, int.MaxValue - 1));
                    SpawnMonster(mob, spawnMap);
                }
            }
        }
        private static void SpawnMonster(Monster monster, Map map)
        {
            World.MessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.MonolithSpawn, monster, map));
            //Game.World.Maps[mapId].InsertCreature(monster);
            //Logger.DebugFormat("Spawning monster: {0} at {1}, {2}", monster.Name, (int) monster.X, (int) monster.Y);
        }
    }
}
