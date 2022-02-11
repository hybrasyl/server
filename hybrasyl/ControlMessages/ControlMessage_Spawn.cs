using Hybrasyl.Objects;
using System;
using System.Linq;

namespace Hybrasyl;

public partial class World : Server
{
    private void ControlMessage_SpawnMonster(HybrasylControlMessage message)
    {
        var monster = (Monster)message.Arguments[0];
        var map = (Map)message.Arguments[1];
        GameLog.DebugFormat("monolith: spawning monster {0} on map {1}", monster.Name, map.Name);
        map.InsertCreature(monster);
    }

    private void ControlMessage_MonolithControl(HybrasylControlMessage message)
    {

        var monster = (Monster)message.Arguments[0];
        var map = (Map)message.Arguments[1];

        // Don't handle control messages for dead/removed mobs, or mobs that cannot move or attack
        if (!monster.Condition.Alive || monster.DeathProcessed || 
            monster.Id == 0 || monster.Map == null || 
            monster.Condition.Asleep || monster.Condition.Frozen) return;

        monster.NextAction();
            
    }
}