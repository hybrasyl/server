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
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

namespace Hybrasyl
{
    /*
    public class ItemTemplate
    {

        public int ID { get; set; }
        public string Name { get; set; }
        public ushort Sprite { get; set; }
        public ushort EquipSprite { get; set; }
        public ushort DisplaySprite { get; set; }
        public byte Color { get; set; }
        public ItemObjectType ItemObjectType { get; set; }
        public WeaponObjectType WeaponObjectType { get; set; }
        public byte EquipmentSlot { get; set; }
        public ushort Weight { get; set; }
        public byte MaximumStack { get; set; }
        public uint MaximumDurability { get; set; }
        public string InvokeName { get; set; }
        public byte BodyStyle { get; set; }

        public byte Level { get; set; }
        public byte Ability { get; set; }
        public Class Class { get; set; }
        public Sex Sex { get; set; }

        public int BonusHP { get; set; }
        public int BonusMP { get; set; }
        public sbyte BonusStr { get; set; }
        public sbyte BonusInt { get; set; }
        public sbyte BonusWis { get; set; }
        public sbyte BonusCon { get; set; }
        public sbyte BonusDex { get; set; }
        public sbyte BonusHit { get; set; }
        public sbyte BonusDmg { get; set; }
        public sbyte BonusAc { get; set; }
        public sbyte BonusMR { get; set; }
        public Element Element { get; set; }
        public ushort MinimumDamage { get; set; }
        public ushort MaximumDamage { get; set; }
    }
    */
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

    public class MonsterTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ushort Sprite { get; set; }
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
