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
 * (C) 2016 Project Hybrasyl (info@hybrasyl.com)
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;

namespace Hybrasyl.Items
{
    public partial class Item
    {
        [XmlIgnore]
        public bool IsVariant { get; set; }

        [XmlIgnore]
        public Item ParentItem { get; set; }

        #region Accessors to provide defaults 
        [XmlIgnore]
        public bool Stackable
        {
            get
            {
                if (Properties.Stackable != null)
                {
                    return Properties.Stackable.Max != 1;
                }
                return false;
            }
        } 

        [XmlIgnore]
        public int MaximumStack => Properties.Stackable?.Max ?? 0;

        [XmlIgnore]
        public byte Level => Properties.Restrictions?.Level?.Min ?? 1;

        [XmlIgnore]
        public byte Ability => Properties.Restrictions?.Ab?.Min ?? 0;

        [XmlIgnore]
        public Element Element
        {
            get
            {
                if (Properties.Equipment.WeaponType == WeaponType.None)
                    return Properties.StatEffects?.Element?.Defense ?? Element.None;
                return Properties.StatEffects?.Element?.Offense ?? Element.None;
            }
        }

        [XmlIgnore]
        public bool Usable => Properties.Use != null;

        [XmlIgnore]
        public Use Use => Properties.Use;

        [XmlIgnore]
        public int BonusHP => Properties.StatEffects.Base?.Hp ?? 0;
        [XmlIgnore]
        public int BonusMP => Properties.StatEffects.Base?.Mp ?? 0;

        [XmlIgnore]
        public Class Class => Properties.Restrictions?.@Class ?? Class.Peasant;
        [XmlIgnore]
        public Gender Gender => Properties.Restrictions?.Gender ?? Gender.Neutral;

        [XmlIgnore]
        public int BonusHp => Properties.StatEffects?.@Base?.Hp ?? 0;
        [XmlIgnore]
        public int BonusMp => Properties.StatEffects?.@Base?.Mp ?? 0;
        [XmlIgnore]
        public sbyte BonusStr => Properties.StatEffects?.@Base?.Str ?? 0;
        [XmlIgnore]
        public sbyte BonusInt => Properties.StatEffects?.@Base.@Int ?? 0;
        [XmlIgnore]
        public sbyte BonusWis => Properties.StatEffects?.@Base?.Wis ?? 0;
        [XmlIgnore]
        public sbyte BonusCon => Properties.StatEffects?.@Base?.Con ?? 0;
        [XmlIgnore]
        public sbyte BonusDex => Properties.StatEffects?.@Base?.Dex ?? 0;
        [XmlIgnore]
        public sbyte BonusDmg => Properties.StatEffects?.Combat?.Dmg ?? 0;
        [XmlIgnore]
        public sbyte BonusHit => Properties.StatEffects?.Combat?.Hit ?? 0;
        [XmlIgnore]
        public sbyte BonusAc => Properties.StatEffects?.Combat?.Ac ?? 0;
        [XmlIgnore]
        public sbyte BonusMr => Properties.StatEffects?.Combat?.Mr ?? 0;
        [XmlIgnore]
        public sbyte BonusRegen => Properties.StatEffects?.Combat?.Regen ?? 0;

        [XmlIgnore]
        public ushort MinLDamage => Properties.Damage?.Large.Min ?? 0;
        [XmlIgnore]
        public ushort MaxLDamage => Properties.Damage?.Large.Max ?? 0;
        [XmlIgnore]
        public ushort MinSDamage => Properties.Damage?.Small.Min ?? 0;
        [XmlIgnore]
        public ushort MaxSDamage => Properties.Damage?.Small.Max ?? 0;

        [XmlIgnore]
        public Variant CurrentVariant { get; set; }

        [XmlIgnore]
        public sbyte Regen => Properties.StatEffects?.Combat?.Regen ?? 0;
        #endregion

        [XmlIgnore]
        public Dictionary<int, Item> Variants { get; set; }

        public int Id
        {
            get
            {
                unchecked
                {
                    if (Properties.Appearance.DisplaySprite > 0)
                    {
                        return 31*Name.GetHashCode()*((Properties.Restrictions?.Gender.GetHashCode() ?? Gender.Neutral.GetHashCode()) + 1)*
                               Properties.Appearance.DisplaySprite.GetHashCode();
                    }
                    return 31*Name.GetHashCode()*((Properties.Restrictions?.Gender.GetHashCode() ?? Gender.Neutral.GetHashCode()) + 1);
                }
            }
        }

        public Item Clone()
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(ms, this);
            ms.Position = 0;
            object obj = bf.Deserialize(ms);
            ms.Close();
            return (Item)obj;
        }
    }
}

namespace Hybrasyl.Castables
{
    public partial class Castable
    {
        public int Id
        {
            get
            {
                unchecked
                {
                    return 31*(Name.GetHashCode() + 1);
                }
            }
        }

        public byte CastableLevel { get; set; }
    }
}

namespace Hybrasyl.Config
{
    public partial class HybrasylAge
    {

        public bool DateInAge(DateTime datetime)
        {
            if (EndDate == null) return datetime.Ticks > StartDate.Ticks;
            var endDate = (DateTime)EndDate;
            return datetime.Ticks >= StartDate.Ticks && datetime.Ticks <= endDate.Ticks;
        }
    }
}

namespace Hybrasyl.Creatures
{
    public partial class Map
    {
        public DateTime LastSpawn { get; set; }
        public int Id { get; set; }
    }

}

namespace Hybrasyl.Nations
{
    public partial class Nation
    {
        public SpawnPoint RandomSpawnPoint
        {
            get
            {
                var rand = new Random();
                if (SpawnPoints.Count > 0)
                    return SpawnPoints[rand.Next(0, SpawnPoints.Count)];
                else
                    return default(SpawnPoint);
            }
        }
    }
}
