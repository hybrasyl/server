using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Newtonsoft.Json;
using System;

namespace Hybrasyl.Interfaces
{
    public interface IStatSnapshotProvider
    {
        public World World { get; }
        public string Name { get; }
        public Guid Guid { get; }
        public StatInfo Stats { get; }

        public Guid CreateStatSnapshot()
        {
            var stats = JsonConvert.SerializeObject(Stats);
            var statInfo = JsonConvert.DeserializeObject<StatInfo>(stats);
            var snapshot = new CreatureSnapshot
            {
                Name = Name,
                CreatureGuid = Guid,
                Stats = statInfo ?? new StatInfo()
            };
            World.WorldState.Set(Guid, snapshot);
            return snapshot.Guid;
        }
    }
}
