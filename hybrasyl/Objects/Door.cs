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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015-2016 Project Hybrasyl (info@hybrasyl.com)
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */
 
 using log4net;

namespace Hybrasyl.Objects
{
    public class Door : VisibleObject
    {
        public new static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool Closed { get; set; }
        public bool IsLeftRight { get; set; }
        public bool UpdateCollision { get; set; }

        public Door(byte x, byte y, bool closed = false, bool isLeftRight = false, bool updateCollision = true)
        {
            X = x;
            Y = y;
            Closed = closed;
            IsLeftRight = isLeftRight;
            UpdateCollision = updateCollision;
        }

        public override void OnClick(User invoker)
        {
            invoker.Map.ToggleDoors(X, Y);
        }

        public override void AoiEntry(VisibleObject obj)
        {
            ShowTo(obj);
        }

        public override void ShowTo(VisibleObject obj)
        {
            if (!(obj is User)) return;
            var user = (User)obj;
            user.SendDoorUpdate(X, Y, Closed,
                IsLeftRight);
        }
    }


}