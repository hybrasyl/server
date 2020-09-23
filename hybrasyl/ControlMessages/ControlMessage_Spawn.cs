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

            // Don't handle control messages for dead/removed mobs, or mobs that cannot move or attack
            if (!monster.Condition.Alive || monster.DeathProcessed || 
                monster.Id == 0 || monster.Map == null || 
                monster.Condition.Asleep || monster.Condition.Frozen) return;
            if (monster.IsHostile)
            {
                if (map.Users.Count > 0)
                {
                    var entityTree = map.EntityTree.GetObjects(monster.GetViewport());
                    //get players on screen
                    var players = entityTree.OfType<User>();

                    if (players.Count() > 0)
                    {
                        //get closest
                        if (monster.ThreatInfo.ThreatTable.Count == 0)
                        {
                            //get 
                            monster.ThreatInfo.AddNewThreat(players.OrderBy(x => x.Distance(monster)).FirstOrDefault(), 1);
                        }
                    }

                    UserGroup targetGroup = null;

                    //get aggro target's group
                    if (monster.ThreatInfo.ThreatTarget != null && monster.ThreatInfo.ThreatTarget is User)
                    {
                        targetGroup = ((User)monster.ThreatInfo.ThreatTarget).Group;
                    }

                    if (monster.ThreatInfo.ThreatTarget != null)
                    {

                        //pathfind or cast if far away

                        if (monster.Distance(monster.ThreatInfo.ThreatTarget) >= 2)
                        {
                            var nextAction = _random.Next(1, 7);

                            if (nextAction > 1)
                            {
                                //pathfind, only if not paralyzed
                                if (!monster.Condition.Paralyzed)
                                    monster.PathFind((monster.Location.X, monster.Location.Y), (monster.ThreatInfo.ThreatTarget.Location.X, monster.ThreatInfo.ThreatTarget.Location.Y));
                            }
                            else
                            {
                                //cast
                                monster.Cast(monster.ThreatInfo.ThreatTarget, targetGroup);
                            }
                        }
                        else
                        {
                            //check facing and attack or cast
                            var nextAction = _random.Next(1, 6);
                            if (nextAction > 1)
                            {
                                var facing = monster.CheckFacing(monster.Direction, monster.ThreatInfo.ThreatTarget);
                                if (facing)
                                {
                                    monster.AssailAttack(monster.Direction, monster.ThreatInfo.ThreatTarget);
                                }
                            }
                            else
                            {
                                monster.Cast(monster.ThreatInfo.ThreatTarget, targetGroup);
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
