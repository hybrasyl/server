using System;
using System.Linq;
using Hybrasyl.Enums;
using Hybrasyl.Internals;
using Hybrasyl.Objects;

namespace Hybrasyl;

public partial class World : Server
{
    [HybrasylMessageHandler(ControlOpcode.MonolithControl)]
    private void ControlMessage_MonolithControl(HybrasylControlMessage message)
    {
        var monster = (Monster)message.Arguments[0];
        var map = (MapObject)message.Arguments[1];

        if (monster == null || map == null) return;
        // Don't handle control messages for dead/removed mobs, or mobs that cannot move or attack
        if (!monster.Condition.Alive || monster.DeathProcessed ||
            monster.Id == 0 || monster.Map == null ||
            monster.Condition.Asleep || monster.Condition.Frozen) return;

        monster.NextAction();
        monster.ProcessActions();
    }
}
