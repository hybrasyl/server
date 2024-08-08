using Hybrasyl.Objects;
using Hybrasyl.Servers;
using Newtonsoft.Json;
using System;

namespace Hybrasyl.Interfaces;

public interface ICreatureSnapshotRequester
{
    public World World { get; }
    public Guid Guid { get; } 
    public Guid OriginSnapshotId { get;  set; }

    public CreatureSnapshot OriginSnapshot =>
        World.WorldState.TryGetValue($"{Guid}:{OriginSnapshotId}",
            out CreatureSnapshot snapshot)
            ? snapshot
            : null;

    public void RequestSnapshot(ICreatureSnapshotProvider snapshotProvider)
    {
        OriginSnapshotId = snapshotProvider.ProvideSnapshot(Guid);
    }
}

public interface ICreatureSnapshotProvider
{
    public World World { get; }
    public string Name { get; }
    public Guid Guid { get; set; }
    public StatInfo Stats { get; set; }

    public Guid ProvideSnapshot(Guid requester)
    {
        var stats = JsonConvert.SerializeObject(Stats);
        var statInfo = JsonConvert.DeserializeObject<StatInfo>(stats);
        var snapshot = new CreatureSnapshot { Name = Name, Parent = Guid, Stats = statInfo };
        World.WorldState.Set($"{requester}:{Guid}", snapshot);
        return snapshot.Id;
    }
}
    