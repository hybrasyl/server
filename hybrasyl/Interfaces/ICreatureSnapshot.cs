using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Hybrasyl.Subsystems.Persistence;
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
            // Serialization round trip as a deep copy of the wire-visible stats
            var statInfo = RedisJsonSerializer.Deserialize<StatInfo>(RedisJsonSerializer.Serialize(Stats));
            var snapshot = new CreatureSnapshot
            {
                Name = Name,
                CreatureGuid = Guid,
                Stats = statInfo ?? new StatInfo()
            };
            World.WorldState.Set(snapshot.Guid, snapshot);
            return snapshot.Guid;
        }
    }
}
