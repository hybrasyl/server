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

using C3;
using log4net;
using System.Drawing;

namespace Hybrasyl.Objects
{
    public class WorldObject : IQuadStorable
    {
        public static readonly ILog Logger =
               LogManager.GetLogger(
               System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The rectangle that defines the object's boundaries.
        /// </summary>
        public Rectangle Rect
        {
            get
            {
                return new Rectangle((int)(X), (int)(Y), 1, 1);
            }
        }

        public bool HasMoved { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
        public uint Id { get; set; }
        public World World { get; set; }
        public string Name { get; set; }

        public WorldObject()
        {
            Name = string.Empty;
        }

        public virtual void SendId()
        {
        }
    }
}