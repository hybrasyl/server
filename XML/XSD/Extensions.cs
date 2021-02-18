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
using System.Xml;


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
                return IsSimple && (Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0);
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
                return IsSimple && (Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0);
            }

        }
    }

    public partial class SpawnGroup
    {
        public ushort MapId { get; set; } = 0;

    }

    public partial class Spawn
    {
        public DateTime LastSpawn { get; set; } = default;
    }

    public partial class LootSet
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

        public bool IsSkill => Book == Book.PrimarySkill || Book == Book.SecondarySkill || Book == Book.UtilitySkill;
        public bool IsSpell => Book == Book.PrimarySpell || Book == Book.SecondarySpell || Book == Book.UtilitySpell;

        public bool OnCooldown
        {
            get
            {
                return Cooldown > 0 ? (DateTime.Now - LastCast).TotalSeconds < Cooldown : false;
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

        public List<string> CategoryList
        {
            get
            {
                if (Categories.Count > 0)
                    return new List<string>();
                else
                    return Categories.Select(x => x.Value).ToList();
            }    
        }

    }

    public class CastableComparer : IEqualityComparer<Castable>
    {
        public bool Equals(Castable c1, Castable c2)
        {
            if (c1 == null && c2 == null) return true;
            if (c1 == null || c2 == null) return false;

            if (c1.Name.Trim().ToLower() == c2.Name.Trim().ToLower() &&
                c1.Book == c2.Book)
                return true;

            return false;
        }

        public int GetHashCode(Castable c)
        {
            string hCode = $"{c.Name.Trim().ToLower()}-{c.Book}";
            return hCode.GetHashCode();
        }
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
                return IsSimple && (Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0);
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
                return IsSimple && (Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0);
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
    public partial class CreatureBehaviorSet
    {
        private List<string> skillCategories = null;
        private List<string> spellCategories = null;

        public List<string> LearnSkillCategories
        {
            get
            {
                if (string.IsNullOrEmpty(Castables?.SkillCategories))
                    skillCategories = new List<string>();
                if (skillCategories == null)
                {
                    skillCategories = Castables.SkillCategories.Trim().ToLower().Split(" ").ToList();
                }
                return skillCategories;
            }
        }

        public List<string> LearnSpellCategories
        {
            get
            {
                if (string.IsNullOrEmpty(Castables?.SpellCategories))
                    spellCategories = new List<string>();
                if (spellCategories == null)
                {
                    spellCategories = Castables.SpellCategories.Trim().ToLower().Split(" ").ToList();
                }
                return spellCategories;
            }
        }

        public List<CreatureCastable> OffensiveCastables => Behavior?.Casting?.Offense?.Castable == null ? new List<CreatureCastable>() : Behavior.Casting.Offense.Castable;
        public List<CreatureCastable> DefensiveCastables => Behavior?.Casting?.Defense?.Castable == null ? new List<CreatureCastable>() : Behavior.Casting.Defense.Castable;
        public List<CreatureCastable> OnDeathCastables => Behavior?.Casting?.OnDeath?.Castable == null ? new List<CreatureCastable>() : Behavior.Casting.OnDeath.Castable;
        public List<CreatureCastable> NearDeathCastables => Behavior?.Casting?.NearDeath?.Castable == null ? new List<CreatureCastable>() : Behavior.Casting.NearDeath.Castable;
        public bool CanCast => OffensiveCastables.Count > 0 || DefensiveCastables.Count > 0 || OnDeathCastables.Count > 0 || NearDeathCastables.Count > 0;
    }
}

namespace Hybrasyl.Xml
{
      
    public partial class Spawn
    {
        private static readonly Random Rng = new Random();
        public bool Disabled { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Calculate a specific offensive element for a spawn from its list of elements.
        /// </summary>
        /// <returns>Element enum</returns>
        public ElementType GetOffensiveElement()
        {
            if (_damage.Element == ElementType.Random)
                return (ElementType)Rng.Next(1, 9);
            else if (_damage.Element == ElementType.RandomFour)
                return (ElementType)Rng.Next(1, 4);
            else if (_damage.Element == ElementType.RandomEight)
                return (ElementType)Rng.Next(5, 8);
            return _damage.Element;
        }

        /// <summary>
        /// Calculate a specific defensive element for a spawn from its list of elements.
        /// </summary>
        /// <returns>Element enum</returns>
        public ElementType GetDefensiveElement()
        {
            if (_defense.Element == ElementType.Random)
                return (ElementType)Rng.Next(1, 9);
            else if (_defense.Element == ElementType.RandomFour)
                return (ElementType)Rng.Next(1, 4);
            else if (_defense.Element == ElementType.RandomEight)
                return (ElementType)Rng.Next(5, 8);
            return _defense.Element;
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

    public partial class SpawnCastableOffense
    {
        public DateTime LastCast { get; set; }
    }

    public partial class SpawnCastableDefense
    {
        public DateTime LastCast { get; set; }
    }
    public partial class SpawnCastableNearDeath
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

    public partial class ServerConfig
    {

        // In case there is nothing defined in XML, we still need some associations for basic
        // functionality
        [XmlIgnoreAttribute]
        static Dictionary<byte, (string key, string setting)> Default = new Dictionary<byte, (string key, string setting)>()
        {
            { 6, ("exchange", "Exchange") },
            { 2, ("group", "Allow Grouping") }
        };

        [XmlIgnoreAttribute]
        public Dictionary<byte, ClientSetting> SettingsNumberIndex { get; set; }

        [XmlIgnoreAttribute]
        public Dictionary<string, ClientSetting> SettingsKeyIndex { get; set; }

        public void InitializeClientSettings()
        {
            SettingsNumberIndex = new Dictionary<byte, ClientSetting>();
            SettingsKeyIndex = new Dictionary<string, ClientSetting>();

            for (byte x = 1; x <= 8; x++)
            {
                var newcs = new ClientSetting()
                {
                    Default = true,
                    Number = x,
                    Key = Default.ContainsKey(x) ? Default[x].key : $"setting{x}",
                    Value = Default.ContainsKey(x) ? Default[x].setting : $"Setting {x}"
                };

                if (ClientSettings == null) // No settings at all in xml
                {
                    SettingsNumberIndex.Add(x, newcs);
                    SettingsKeyIndex.Add(newcs.Key, newcs);
                }
                else
                {
                    var cs = ClientSettings.Where(val => val.Number == x).FirstOrDefault();
                    if (cs == default(ClientSetting))
                    {
                        // No specified setting for this number
                        SettingsNumberIndex.Add(x, newcs);
                        SettingsKeyIndex.Add(newcs.Key, newcs);
                    }
                    else
                    {
                        // We have a defined setting in xml, use it
                        SettingsNumberIndex.Add(x, cs);
                        SettingsKeyIndex.Add(cs.Key.ToLower(), cs);
                    }
                }
            }
        }

        public string GetSettingLabel(byte number) => SettingsNumberIndex[number].Value;
        public byte GetSettingNumber(string key) => SettingsKeyIndex[key.ToLower()].Number;
    }

    public partial class Access
    {
        private List<string> _privilegedUsers = new List<String>();
        private List<string> _reservedNames = new List<String>();

        public bool AllPrivileged { get; set; } = false;

        public List<string> PrivilegedUsers
        {
            get
            {
                if (!string.IsNullOrEmpty(Privileged) && _privilegedUsers.Count == 0)
                    foreach (var p in Privileged.Trim().Split(' '))
                    {
                        _privilegedUsers.Add(p.Trim().ToLower());
                        if (p.Trim().ToLower() == "*")
                            AllPrivileged = true;
                    }
                return _privilegedUsers;
            }
        }

        public List<string> ReservedNames
        {
            get
            {
                if (!string.IsNullOrEmpty(Reserved) && _reservedNames.Count == 0)
                    foreach (var p in Reserved.Trim().Split(' '))
                        _reservedNames.Add(p.Trim().ToLower());
                return _reservedNames;
            }
        }

        public bool IsPrivileged(string user)
        {
            if (PrivilegedUsers.Count > 0)
                return PrivilegedUsers.Contains(user.ToLower()) || PrivilegedUsers.Contains("*");
            return false;
        }

        public bool IsReservedName(string user)
        {
            if (ReservedNames.Count > 0)
                return ReservedNames.Contains(user.ToLower());
            return false;
        }
    }
}

