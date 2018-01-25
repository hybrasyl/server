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
using System.Linq;
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
                    return Properties.StatModifiers?.Element?.Defense ?? Element.None;
                return Properties.StatModifiers?.Element?.Offense ?? Element.None;
            }
        }

        [XmlIgnore]
        public bool Usable => Properties.Use != null;

        [XmlIgnore]
        public Use Use => Properties.Use;

        [XmlIgnore]
        public int BonusHP => Properties.StatModifiers.Base?.Hp ?? 0;
        [XmlIgnore]
        public int BonusMP => Properties.StatModifiers.Base?.Mp ?? 0;

        [XmlIgnore]
        public Class Class => Properties.Restrictions?.@Class ?? Class.Peasant;
        [XmlIgnore]
        public Gender Gender => Properties.Restrictions?.Gender ?? Gender.Neutral;

        [XmlIgnore]
        public int BonusHp => Properties.StatModifiers?.@Base?.Hp ?? 0;
        [XmlIgnore]
        public int BonusMp => Properties.StatModifiers?.@Base?.Mp ?? 0;
        [XmlIgnore]
        public sbyte BonusStr => Properties.StatModifiers?.@Base?.Str ?? 0;
        [XmlIgnore]
        public sbyte BonusInt => Properties.StatModifiers?.@Base.@Int ?? 0;
        [XmlIgnore]
        public sbyte BonusWis => Properties.StatModifiers?.@Base?.Wis ?? 0;
        [XmlIgnore]
        public sbyte BonusCon => Properties.StatModifiers?.@Base?.Con ?? 0;
        [XmlIgnore]
        public sbyte BonusDex => Properties.StatModifiers?.@Base?.Dex ?? 0;
        [XmlIgnore]
        public sbyte BonusDmg => Properties.StatModifiers?.Combat?.Dmg ?? 0;
        [XmlIgnore]
        public sbyte BonusHit => Properties.StatModifiers?.Combat?.Hit ?? 0;
        [XmlIgnore]
        public sbyte BonusAc => Properties.StatModifiers?.Combat?.Ac ?? 0;
        [XmlIgnore]
        public sbyte BonusMr => Properties.StatModifiers?.Combat?.Mr ?? 0;
        [XmlIgnore]
        public sbyte BonusRegen => Properties.StatModifiers?.Combat?.Regen ?? 0;

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
        public sbyte Regen => Properties.StatModifiers?.Combat?.Regen ?? 0;
        #endregion

        [XmlIgnore]
        public Dictionary<int, Item> Variants { get; set; }

        public int Id
        {
            get
            {
                unchecked
                {
                    return 31 * Name.GetHashCode() * ((Properties.Restrictions?.Gender.GetHashCode() ?? Gender.Neutral.GetHashCode()) + 1);
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
    public partial class Heal
    {
        public bool IsSimple
        {
            get { return string.IsNullOrEmpty(Formula); }
        }
        
        // temporary silliness due to xsd issues
        public bool IsEmpty
        {
            get
            {
                return IsSimple && (string.IsNullOrEmpty(Simple.Value) && Simple.Min == "0" && Simple.Max == "0");
            }
        }
    }

    public partial class Damage
    {
        public bool IsSimple
        {
            get { return string.IsNullOrEmpty(Formula); }
        }
        // temporary silliness due to xsd issues
        public bool IsEmpty
        {
            get
            {
                return IsSimple && (string.IsNullOrEmpty(Simple.Value) && Simple.Min == "0" && Simple.Max == "0");
            }

        }
    }

    public partial class Castable
    {
        public int Id
        {
            get
            {
                unchecked
                {
                    return 31 * (Name.GetHashCode() + 1);
                }
            }
        }

        public byte CastableLevel { get; set; }

        public DateTime LastCast { get; set; }

        public bool OnCooldown
        {
            get
            {
                return Cooldown > 0 ? (DateTime.Now - LastCast).Seconds < Cooldown : false;
            }
        }

        public byte GetMaxLevelByClass(Class castableClass)
        {
            var maxLevelProperty = MaxLevel.GetType().GetProperty(castableClass.ToString());
            return (byte)(maxLevelProperty != null ? maxLevelProperty.GetValue(MaxLevel, null) : 0);
        }

        public bool TryGetMotion(Class castClass, out Motion motion)
        {
            motion = null;
            try
            {
                motion = Effects.Animations.OnCast.Player.SingleOrDefault(x => x.Class.Contains(castClass));
            }
            catch (InvalidOperationException)
            {
                motion = Effects.Animations.OnCast.Player.FirstOrDefault(x => x.Class.Contains(castClass));
            }
            catch (NullReferenceException)
            {
                return false;
            }           
            if (motion != null) return true;
            return false;
        }
    }

}

namespace Hybrasyl.Statuses
{
    public partial class Heal
    {
        public bool IsSimple
        {
            get { return string.IsNullOrEmpty(Formula); }
        }
        public bool IsEmpty
        {
            get
            {
                return IsSimple && (string.IsNullOrEmpty(Simple.Value) && Simple.Min == "0" && Simple.Max == "0");
            }

        }

    }

    public partial class Damage
    {
        public bool IsSimple
        {
            get { return string.IsNullOrEmpty(Formula); }
        }
        public bool IsEmpty
        {
            get
            {
                return IsSimple && (string.IsNullOrEmpty(Simple.Value) && Simple.Min == "0" && Simple.Max == "0");
            }

        }

    }

    public partial class Status
    {
        public int Id
        {
            get
            {
                unchecked
                {
                    return 31 * (Name.GetHashCode() + 1);
                }
            }
        }
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
    public partial class LootTable
    {
        public static explicit operator LootTable(Loot.LootTable table)
        {
            var ret = new LootTable();
            ret.Items = new LootTableItemList();
            ret.Items.Items = new List<LootItem>();

            ret.Chance = table.Chance;
            ret.Gold.Min = table.Gold.Min;
            ret.Gold.Max = table.Gold.Max;
            ret.Items.Chance = table.Items.Chance;

            foreach (var items in table.Items.Items)
            {
                var newLootItem = new LootItem();
                newLootItem.Always = items.Always;
                newLootItem.Max = items.Max;
                newLootItem.Min = items.Min;
                newLootItem.Unique = items.Unique;
                newLootItem.Value = items.Value;
                newLootItem.Variants = items.Variants;

                ret.Items.Items.Add(newLootItem);
            }

            ret.Items.Rolls = table.Items.Rolls;
            ret.Rolls = table.Rolls;

            return ret;
        }
    }
    
    
    public partial class Spawn
    {
        protected Random Rng = new Random();

        /// <summary>
        /// Calculate a specific offensive element for a spawn from its list of elements.
        /// </summary>
        /// <returns>Element enum</returns>
        public Element GetOffensiveElement()
        {
            if (_damage.Element.Count > 1)
                return _damage.Element[Rng.Next(_damage.Element.Count)];
            else if (_damage.Element.Count == 1 && _damage.Element[0] != Element.Random)
                return _damage.Element[0];

            // Only deal with "base" elements for right now
            return (Element)Rng.Next(1, 4);
        }

        /// <summary>
        /// Calculate a specific defensive element for a spawn from its list of elements.
        /// </summary>
        /// <returns>Element enum</returns>
        public Element GetDefensiveElement()
        {
            if (_defense.Element.Count > 1)
                return _defense.Element[Rng.Next(_defense.Element.Count)];
            else if (_defense.Element.Count == 1 && _defense.Element[0] != Element.Random)
                return _damage.Element[0];

            // Only deal with "base" elements for right now
            return (Element)Rng.Next(1, 4);
        }
    }
    public partial class Map
    {
        public DateTime LastSpawn { get; set; }
        public int Id { get; set; }
        public bool Disabled { get; set; }
    }

    public partial class SpawnGroup
    {
        public bool Disabled { get; set; }
        public string Filename { get; set; }
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

