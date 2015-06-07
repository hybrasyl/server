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
     public class ShadeComponent : GameServerComponent
    {
        private GameServerTimer timer;
        private byte shade = 0;

        public ShadeComponent(World world)
            : base(world)
        {
            this.timer = new GameServerTimer(
                TimeSpan.FromSeconds(25f));
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
                    {
                        var x20 = new ServerPacket(0x20);
                        x20.WriteByte(0x01);
                        x20.WriteByte(this.shade);
                        user.Enqueue(x20);

                        if (shade == 15)
                            user.SendSystemMessage("Look's like it's getting dark soon.");
                    }
                }

                this.shade += 1;
                this.shade %= 18;
            }
        }
    }
}
