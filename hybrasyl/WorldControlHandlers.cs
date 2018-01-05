using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl
{
    public partial class World
    {
        #region Control Message Handlers

        public void SetControlMessageHandlers()
        {
            ControlMessageHandlers[ControlOpcodes.CleanupUser] = ControlMessage_CleanupUser;
            ControlMessageHandlers[ControlOpcodes.SaveUser] = ControlMessage_SaveUser;
            ControlMessageHandlers[ControlOpcodes.ShutdownServer] = ControlMessage_ShutdownServer;
            ControlMessageHandlers[ControlOpcodes.RegenUser] = ControlMessage_RegenerateUser;
            ControlMessageHandlers[ControlOpcodes.LogoffUser] = ControlMessage_LogoffUser;
            ControlMessageHandlers[ControlOpcodes.MailNotifyUser] = ControlMessage_MailNotifyUser;
            ControlMessageHandlers[ControlOpcodes.StatusTick] = ControlMessage_StatusTick;
            ControlMessageHandlers[ControlOpcodes.MonolithSpawn] = ControlMessage_SpawnMonster;
            ControlMessageHandlers[ControlOpcodes.MonolithControl] = ControlMessage_MonolithControl;
        }

        private void ControlMessage_CleanupUser(HybrasylControlMessage message)
        {
            // clean up after a broken connection
            var connectionId = (long)message.Arguments[0];
            User user;
            if (ActiveUsers.TryRemove(connectionId, out user))
            {
                Logger.InfoFormat("cid {0}: closed, player {1} removed", connectionId, user.Name);
                if (user.ActiveExchange != null)
                    user.ActiveExchange.CancelExchange(user);
                ((IDictionary)ActiveUsersByName).Remove(user.Name);
                user.Save();
                user.UpdateLogoffTime();
                user.Map?.Remove(user);
                user.Group?.Remove(user);
                Remove(user);
                Logger.DebugFormat("cid {0}: {1} cleaned up successfully", user.Name);
                DeleteUser(user.Name);
            }
        }

        private void ControlMessage_RegenerateUser(HybrasylControlMessage message)
        {
            // regenerate a user
            // USDA Formula for HP: MAXHP * (0.1 + (CON - Lv) * 0.01) <20% MAXHP
            // USDA Formula for MP: MAXMP * (0.1 + (WIS - Lv) * 0.01) <20% MAXMP
            // Regen = regen * 0.0015 (so 100 regen = 15%)
            User user;
            var connectionId = (long)message.Arguments[0];
            if (ActiveUsers.TryGetValue(connectionId, out user))
            {
                uint hpRegen = 0;
                uint mpRegen = 0;
                double fixedRegenBuff = Math.Min(user.Regen * 0.0015, 0.15);
                fixedRegenBuff = Math.Max(fixedRegenBuff, 0.125);
                if (user.Hp != user.MaximumHp)
                {
                    hpRegen = (uint)Math.Min(user.MaximumHp * (0.1 * Math.Max(user.Con, (user.Con - user.Level)) * 0.01),
                        user.MaximumHp * 0.20);
                    hpRegen = hpRegen + (uint)(fixedRegenBuff * user.MaximumHp);
                }
                if (user.Mp != user.MaximumMp)
                {
                    mpRegen = (uint)Math.Min(user.MaximumMp * (0.1 * Math.Max(user.Int, (user.Int - user.Level)) * 0.01),
                        user.MaximumMp * 0.20);
                    mpRegen = mpRegen + (uint)(fixedRegenBuff * user.MaximumMp);
                }
                Logger.DebugFormat("User {0}: regen HP {1}, MP {2}", user.Name,
                    hpRegen, mpRegen);
                user.Hp = Math.Min(user.Hp + hpRegen, user.MaximumHp);
                user.Mp = Math.Min(user.Mp + mpRegen, user.MaximumMp);
                user.UpdateAttributes(StatUpdateFlags.Current);
            }
        }

        private void ControlMessage_SaveUser(HybrasylControlMessage message)
        {
            // save a user
            User user;
            var connectionId = (long)message.Arguments[0];
            if (ActiveUsers.TryGetValue(connectionId, out user))
            {
                Logger.DebugFormat("Saving user {0}", user.Name);
                user.Save();
            }
            else
            {
                Logger.WarnFormat("Tried to save user associated with connection ID {0} but user doesn't exist",
                    connectionId);
            }
        }

        private void ControlMessage_ShutdownServer(HybrasylControlMessage message)
        {
            // Initiate an orderly shutdown
            var userName = (string)message.Arguments[0];
            Logger.WarnFormat("Server shutdown request initiated by {0}", userName);
            // Chaos is Rising Up, yo.
            foreach (var connection in ActiveUsers)
            {
                var user = connection.Value;
                user.SendMessage("Chaos is rising up. Please re-enter in a few minutes.",
                    Hybrasyl.MessageTypes.SYSTEM_WITH_OVERHEAD);
            }

            // Actually shut down the server. This terminates the listener loop in Game.
            if (Game.IsActive())
                Game.ToggleActive();

            Logger.WarnFormat("Server has begun shutdown");
        }

        private void ControlMessage_LogoffUser(HybrasylControlMessage message)
        {
            // Log off the specified user
            var userName = (string)message.Arguments[0];
            Logger.WarnFormat("{0}: forcing logoff", userName);
            User user;
            if (WorldData.TryGetValue(userName, out user))
            {
                user.Logoff();
            }
        }

        private void ControlMessage_MailNotifyUser(HybrasylControlMessage message)
        {
            // Set unread mail flag and if the user is online, send them an UpdateAttributes packet
            var userName = (string)message.Arguments[0];
            Logger.DebugFormat("mail: attempting to notify {0} of new mail", userName);
            User user;
            if (WorldData.TryGetValue(userName, out user))
            {
                user.UpdateAttributes(StatUpdateFlags.Secondary);
                Logger.DebugFormat("mail: notification to {0} sent", userName);
            }
            else
            {
                Logger.DebugFormat("mail: notification to {0} failed, not logged in?", userName);
            }
        }

        private void ControlMessage_StatusTick(HybrasylControlMessage message)
        {
            var userName = (string)message.Arguments[0];
            Logger.DebugFormat("statustick: processing tick for {0}", userName);
            User user;
            if (WorldData.TryGetValue(userName, out user))
            {
                user.ProcessStatusTicks();
            }
            else
            {
                Logger.DebugFormat("tick: Cannot process tick for {0}, not logged in?", userName);
            }
        }

        private void ControlMessage_SpawnMonster(HybrasylControlMessage message)
        {
            var monster = (Monster)message.Arguments[0];
            var map = (Map)message.Arguments[1];
            Logger.DebugFormat("monolith: spawning monster {0} on map {1}", monster.Name, map.Name);
            map.InsertCreature(monster);
        }

        private void ControlMessage_MonolithControl(HybrasylControlMessage message)
        {

            var monster = (Monster)message.Arguments[0];
            var map = (Map)message.Arguments[1];

            if (monster.IsHostile)
            {
                var entityTree = map.EntityTree.GetObjects(monster.GetViewport());
                var hasPlayer = entityTree.Any(x => x is User);

                if (hasPlayer)
                {
                    //get players
                    var players = entityTree.OfType<User>();

                    //get closest
                    var closest =
                        players.OrderBy(x => Math.Sqrt((Math.Pow(monster.X - x.X, 2) + Math.Pow(monster.Y - x.Y, 2))))
                            .FirstOrDefault();

                    if (closest != null)
                    {

                        //pathfind or cast if far away
                        var distanceX = (int)Math.Sqrt(Math.Pow(monster.X - closest.X, 2));
                        var distanceY = (int)Math.Sqrt(Math.Pow(monster.Y - closest.Y, 2));
                        if (distanceX >= 1 && distanceY >= 1)
                        {
                            var nextAction = _random.Next(1, 6);

                            if (nextAction > 1)
                            {
                                //pathfind;
                                if (distanceX > distanceY)
                                {
                                    monster.Walk(monster.X > closest.X ? Direction.West : Direction.East);
                                }
                                else
                                {
                                    //movey
                                    monster.Walk(monster.Y > closest.Y ? Direction.North : Direction.South);
                                }

                                if (distanceX == distanceY)
                                {
                                    var next = _random.Next(0, 2);

                                    if (next == 0)
                                    {
                                        monster.Walk(monster.X > closest.X ? Direction.West : Direction.East);
                                    }
                                    else
                                    {
                                        monster.Walk(monster.Y > closest.Y ? Direction.North : Direction.South);
                                    }
                                }
                            }
                            else
                            {
                                //cast
                                if (monster.CanCast)
                                {
                                    monster.Cast(closest);
                                }
                            }
                        }
                        else
                        {
                            //check facing and attack or cast

                            var nextAction = _random.Next(1, 6);
                            if (nextAction > 1)
                            {
                                var facing = monster.CheckFacing(monster.Direction, closest);
                                if (facing)
                                {
                                    monster.AssailAttack(monster.Direction, closest);
                                }
                            }
                            else
                            {
                                if (monster.CanCast)
                                {
                                    monster.Cast(closest);
                                }
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
                    monster.Walk((Direction)nextMove);
                }
                else
                {
                    var nextMove = _random.Next(0, 4);
                    monster.Turn((Direction)nextMove);
                }
            }
        }

        #endregion Control Message Handlers
    }
}
