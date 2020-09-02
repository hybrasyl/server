using Hybrasyl.Objects;
using System;
using System.Linq;

namespace Hybrasyl
{
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

            // Don't handle control messages for dead/removed mobs
            if (!monster.Condition.Alive || monster.Id == 0 || monster.Map == null) return;
            if (monster.IsHostile)
            {
                if (map.Users.Count > 0)
                {
                    var entityTree = map.EntityTree.GetObjects(monster.GetViewport());
                    //get players on screen
                    var players = entityTree.OfType<User>();

                    Creature aggroTarget;
                    //get closest
                    if (monster.AggroTable.Count == 0)
                    {
                        //get 
                        aggroTarget = players.OrderBy(x => x.Distance(monster)).FirstOrDefault();
                    }
                    else
                    {
                        var aggroid = monster.AggroTable.OrderByDescending(x => x.Value).FirstOrDefault().Key;
                        aggroTarget = players.FirstOrDefault(x => x.Name == aggroid);
                    }

                    UserGroup targetGroup = null;

                    //get aggro target's group
                    if (aggroTarget is User)
                    {
                        targetGroup = ((User)aggroTarget).Group;
                    }

                    if (aggroTarget != null)
                    {

                        //pathfind or cast if far away

                        if (monster.Distance(aggroTarget) >= 2)
                        {
                            var nextAction = _random.Next(1, 7);

                            if (nextAction > 1)
                            {
                                //pathfind;
                                monster.PathFind((monster.Location.X, monster.Location.Y), (aggroTarget.Location.X, aggroTarget.Location.Y));
                            }
                            else
                            {
                                //cast
                                monster.Cast(aggroTarget, targetGroup);
                            }
                        }
                        else
                        {
                            //check facing and attack or cast

                            var nextAction = _random.Next(1, 6);
                            if (nextAction > 1)
                            {
                                var facing = monster.CheckFacing(monster.Direction, aggroTarget);
                                if (facing)
                                {
                                    monster.AssailAttack(monster.Direction, aggroTarget);
                                }
                            }
                            else
                            {
                                monster.Cast(aggroTarget, targetGroup);
                            }
                        }
                    }
                }
            }
            if (monster.ShouldWander)
            {
                var nextAction = _random.Next(0, 2);

                if (nextAction == 1)
                {
                    var nextMove = _random.Next(0, 4);
                    monster.Walk((Xml.Direction)nextMove);
                }
                else
                {
                    var nextMove = _random.Next(0, 4);
                    monster.Turn((Xml.Direction)nextMove);
                }
            }
        }
    }
}
