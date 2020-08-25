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
 * (C) 2020 ERISCO, LLC
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Text;

public static class EnumerableExtension
{
    public static T PickRandom<T>(this IEnumerable<T> source)
    {
        return source.PickRandom(1).Single();
    }

    public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count)
    {
        return source.Shuffle().Take(count);
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        return source.OrderBy(x => Guid.NewGuid());
    }
}

namespace Hybrasyl.Xml
{
    public partial class Item
    {
        public static SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider();
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
                var off = Properties.StatModifiers?.Element?.Offense ?? Element.None;
                var def = Properties.StatModifiers?.Element?.Defense ?? Element.None;
                if (Properties.Equipment?.Slot == EquipmentSlot.Necklace)
                    return off;
                return def;
            }
        }

        [XmlIgnore]
        public bool Usable => Properties.Use != null;

        [XmlIgnore]
        public Use Use => Properties.Use;

        [XmlIgnore]
        public int BonusHP => Properties.StatModifiers?.Base?.Hp ?? 0;
        [XmlIgnore]
        public int BonusMP => Properties.StatModifiers?.Base?.Mp ?? 0;

        [XmlIgnore]
        public Class Class => Properties.Restrictions?.Class ?? Class.Peasant;
        [XmlIgnore]
        public Gender Gender => Properties.Restrictions?.Gender ?? Gender.Neutral;

        [XmlIgnore]
        public int BonusHp => Properties.StatModifiers?.@Base?.Hp ?? 0;
        [XmlIgnore]
        public int BonusMp => Properties.StatModifiers?.@Base?.Mp ?? 0;
        [XmlIgnore]
        public sbyte BonusStr => Properties.StatModifiers?.@Base?.Str ?? 0;
        [XmlIgnore]
        public sbyte BonusInt => Properties.StatModifiers?.@Base?.@Int ?? 0;
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
        public Dictionary<string, List<Item>> Variants { get; set; }

        public string Id
        {
            get
            {
                var rawhash = $"{Name.Normalize()}:{Properties.Restrictions?.Gender.ToString().Normalize() ?? Gender.Neutral.ToString().Normalize()}";
                var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(rawhash));
                return string.Concat(hash.Select(b => b.ToString("x2"))).Substring(0, 8);
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

        public Item RandomVariant(string variant)
        {
            if (Variants.ContainsKey(variant))
            {
                return Variants[variant].PickRandom();
            }
            return null;
        }

    }

    public partial class VariantGroup
    {
        public Variant RandomVariant() => Variant.PickRandom();
    }
}

namespace Hybrasyl.Xml
{
    public partial class CastableHeal
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

    public partial class CastableDamage
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

        public uint UseCount { get; set; }

        public byte MasteryLevel { get; set; }

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

        public bool TryGetMotion(Class castClass, out CastableMotion motion)
        {
            motion = null;

            if (Effects?.Animations?.OnCast?.Player == null) return false;

            var m = Effects.Animations.OnCast.Player.Where(e => e.Class.Contains(castClass));
            if (m.Count() == 0)
                m = Effects.Animations.OnCast.Player.Where(e => e.Class.Count == 0);

            if (m.Count() == 0)
                return false;
            else
                motion = m.First();

            return true;
        }

        //public bool IntentTargets(IntentTarget type)
        //{
        //    foreach (var intent in Intents)
        //    {
        //        if (intent.Target.Contains(type))
        //            return true;
        //    }
        //    return false;
        //}

        
    }

    public partial class CastableIntent
    {
        public bool IsShapeless => Cross.Count == 0 && Line.Count == 0 && Square.Count == 0 && Tile.Count == 0 && Map == null;
    }
}

namespace Hybrasyl.Xml
{
    public partial class StatusHeal
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

    public partial class StatusDamage
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

namespace Hybrasyl.Xml
{

    public partial class Time
    {

        public HybrasylAge DefaultAge => new HybrasylAge() { Name = "Hybrasyl", StartYear = 1 };

        /// <summary>
        /// Try to find the previous age for a given age. Return false if there is no previous age 
        /// (in which case, a Hybrasyl date before the beginning of the age is simply a negative year)
        /// </summary>
        /// <param name="age"></param>
        /// <param name="previousAge"></param>
        /// <returns></returns>
        public bool TryGetPreviousAge(HybrasylAge age, out HybrasylAge previousAge)
        {
            previousAge = null;
            if (Ages.Count == 1)
                return false;
            // Find the age of the day before the start date. This assumes the 
            // user hasn't done something doltish like having non-contiguous ages
            var before = age.StartDate - new TimeSpan(1, 0, 0, 0);
            previousAge = Ages.FirstOrDefault(a => a.DateInAge(before));
            return previousAge != null;
        }

        /// <summary>
        /// Try to find the next age for a given age. Return false if there is no next age 
        /// (in which case, the Hybrasyl year simply increments without end)
        /// </summary>
        /// <param name="age"></param>
        /// <param name="nextAge"></param>
        /// <returns></returns>
        public bool TryGetNextAge(HybrasylAge age, out HybrasylAge nextAge)
        {
            nextAge = null;
            if (Ages.Count == 1)
                return false;
            // Find the age of the day after the start date. This (again) assumes the 
            // user hasn't done something doltish like having non-contiguous ages
            var after = age.StartDate + new TimeSpan(1, 0, 0, 0);
            nextAge = Ages.FirstOrDefault(a => a.DateInAge(after));
            return nextAge != null;
        }


        public HybrasylAge GetAgeFromTerranDatetime(DateTime datetime)
        {
            if (Ages.Count == 0)
                return DefaultAge;
            else if (Ages.Count == 1)
                return Ages.First();
            else
                return Ages.First(a => a.DateInAge(datetime));
        }
    }

    public partial class HybrasylAge
    {

        public bool DateInAge(DateTime datetime)
        {
            if (EndDate == default(DateTime)) return datetime.Ticks > StartDate.Ticks;
            var endDate = (DateTime)EndDate;
            return datetime.Ticks >= StartDate.Ticks && datetime.Ticks <= endDate.Ticks;
        }

    }

    public partial class NewPlayer
    {
        public StartMap GetStartMap()
        {
            StartMaps.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
            return StartMaps.First();
        }
    }
}
namespace Hybrasyl.Xml
{
      
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
            else if (_damage.Element.Count == 1 && _damage.Element[0] != Xml.Element.Random)
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
    public partial class SpawnMap
    {
        public DateTime LastSpawn { get; set; }
        public int Id { get; set; }
    }

    public partial class SpawnGroup
    {
        public string Filename { get; set; }
    }

    public partial class SpawnCastable
    {
        public DateTime LastCast { get; set; }
    }

}

namespace Hybrasyl.Xml
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

namespace Hybrasyl.Xml
{
    public partial class Access
    {
        private List<string> _privilegedUsers = new List<String>();

        public List<string> PrivilegedUsers
        {
            get
            {
                if (!string.IsNullOrEmpty(Privileged))
                  foreach (var p in Privileged.Trim().Split(' '))
                    _privilegedUsers.Add(p.Trim().ToLower());
                return _privilegedUsers;
            }
        }

        public bool IsPrivileged(string user)
        {
            if (PrivilegedUsers.Count > 0)
                return PrivilegedUsers.Contains(user.ToLower()) || PrivilegedUsers.Contains("*");
            return false;
        }

    }
}

