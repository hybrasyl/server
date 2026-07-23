
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
            // WirePlan member copy of the wire-visible stats: runs on every status
            // application, so no JSON round trip. Plan order matters - the Hp/Mp
            // setters clamp against maxima assigned by earlier members.
            var statInfo = new StatInfo();
            foreach (var member in WirePlan.For(typeof(StatInfo)).Members)
                member.Set?.Invoke(statInfo, member.Get(Stats));
            var snapshot = new CreatureSnapshot
            {
                Name = Name,
                CreatureGuid = Guid,
                Stats = statInfo
            };
            World.WorldState.Set(snapshot.Guid, snapshot);
            return snapshot.Guid;
        }
    }
}
