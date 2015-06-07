/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using Hybraysl;
using System;

namespace Hybrasyl.Components
{
    public class MessageComponent : GameServerComponent
    {
        private GameServerTimer timer;
        private byte shade = 0;

        public MessageComponent(World world)
            : base(world)
        {
            this.timer = new GameServerTimer(
                TimeSpan.FromSeconds(20.0f));
        }

        public override void Update(TimeSpan elapsedTime)
        {
            this.timer.Update(elapsedTime);

            if (this.timer.Elapsed)
            {
                this.timer.Reset();

                foreach (var user in world.Users.Values)
                {
                    if (user != null)
                        user.SendSystemMessage("\0");
                }
            }
        }
    }
}
