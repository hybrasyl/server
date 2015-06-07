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

using System;

namespace Hybrasyl
{
    public class GameServerTimer
    {
        public TimeSpan Timer { get; set; }
        public TimeSpan Delay { get; set; }

        public bool Elapsed
        {
            get { return (this.Timer >= this.Delay); }
        }

        public GameServerTimer(TimeSpan delay)
        {
            this.Timer = TimeSpan.Zero;
            this.Delay = delay;
        }

        public void Reset()
        {
            this.Timer = TimeSpan.Zero;
        }
        public void Update(TimeSpan elapsedTime)
        {
            this.Timer += elapsedTime;
        }
    }
}