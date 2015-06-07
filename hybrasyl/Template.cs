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

using Hybrasyl.Objects;
namespace Hybrasyl
{
    public class SkillTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ushort Sprite { get; set; }
    }

    public class SpellTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ushort Sprite { get; set; }
    }

    public class MonsterTemplate : Monster
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ushort Sprite { get; set; }

        public double Speed { get; set; }
    }

    public class MerchantTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ushort Sprite { get; set; }
    }

    public class ReactorTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
