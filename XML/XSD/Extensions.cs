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
using System.Linq;
using System.Xml.Serialization;
using Dotnet.ProjInfo;

namespace Hybrasyl.Xml
{
    public partial class CastableHeal
    {
        public bool IsSimple => string.IsNullOrEmpty(Formula);

        // temporary silliness due to xsd issues
        public bool IsEmpty => IsSimple && Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0;
    }

    public partial class CastableDamage
    {
        public bool IsSimple => string.IsNullOrEmpty(Formula);

        // temporary silliness due to xsd issues
        public bool IsEmpty => IsSimple && Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0;
    }

    // For some reason xsd2code doesn't add this and it breaks spawngroup parsing
    [XmlRootAttribute(Namespace = "http://www.hybrasyl.com/XML/Hybrasyl/2020-02")]
    public partial class SpawnGroup
    {
        public ushort MapId { get; set; }
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

    public partial class CastableIntent
    {
        public bool IsShapeless =>
            Cross.Count == 0 && Line.Count == 0 && Square.Count == 0 && Tile.Count == 0 && Map == null;
    }
}

namespace Hybrasyl.Xml
{
    public partial class StatusHeal
    {
        public bool IsSimple => string.IsNullOrEmpty(Formula);

        public bool IsEmpty => IsSimple && Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0;
    }

    public partial class StatusDamage
    {
        public bool IsSimple => string.IsNullOrEmpty(Formula);
        public bool IsEmpty => IsSimple && Simple.Value == 0 && Simple.Min == 0 && Simple.Max == 0;
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
        public HybrasylAge DefaultAge => new() { Name = "Hybrasyl", StartYear = 1 };

        /// <summary>
        ///     Try to find the previous age for a given age. Return false if there is no previous age
        ///     (in which case, a Hybrasyl date before the beginning of the age is simply a negative year)
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
            previousAge = Ages.FirstOrDefault(predicate: a => a.DateInAge(before));
            return previousAge != null;
        }

        /// <summary>
        ///     Try to find the next age for a given age. Return false if there is no next age
        ///     (in which case, the Hybrasyl year simply increments without end)
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
            nextAge = Ages.FirstOrDefault(predicate: a => a.DateInAge(after));
            return nextAge != null;
        }


        public HybrasylAge GetAgeFromTerranDatetime(DateTime datetime)
        {
            return Ages.Count switch
            {
                0 => DefaultAge,
                1 => Ages.First(),
                _ => Ages.First(predicate: a => a.DateInAge(datetime))
            };
        }
    }

    public partial class HybrasylAge
    {
        public bool DateInAge(DateTime datetime)
        {
            if (EndDate == default) return datetime.Ticks > StartDate.Ticks;
            var endDate = EndDate;
            return datetime.Ticks >= StartDate.Ticks && datetime.Ticks <= endDate.Ticks;
        }
    }

    public partial class NewPlayer
    {
        public StartMap GetStartMap()
        {
            StartMaps.OrderBy(keySelector: x => Guid.NewGuid()).FirstOrDefault();
            return StartMaps.First();
        }
    }
}

namespace Hybrasyl.Xml
{
    public partial class CreatureBehaviorSet
    {
        private List<string> skillCategories;
        private List<string> spellCategories;

        public List<string> LearnSkillCategories => string.IsNullOrEmpty(Castables?.SkillCategories)
            ? new List<string>()
            : Castables.SkillCategories.Trim().ToLower().Split(" ").ToList();

        public List<string> LearnSpellCategories => string.IsNullOrEmpty(Castables?.SpellCategories)
            ? new List<string>()
            : Castables.SpellCategories.Trim().ToLower().Split(" ").ToList();
    }
}

namespace Hybrasyl.Xml
{
    public partial class Spawn
    {
        public bool Disabled { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;

        public ElementType OffensiveElement
        {
            get
            {
                var ele = _damage.Elements.Count switch
                {
                    1 => _damage.Elements.First(),
                    > 1 => _damage.Elements.PickRandom(),
                    _ => ElementType.None
                };
                return ele switch
                {
                    ElementType.RandomExpanded => (ElementType) Random.Shared.Next(1, 10),
                    ElementType.RandomTemuair => (ElementType) Random.Shared.Next(1, 7),
                    _ => ele
                };
            }
        }

        public ElementType DefensiveElement
        {
            get
            {
                var ele = _defense.Elements.Count switch
                {
                    1 => _damage.Elements.First(),
                    > 1 => _damage.Elements.PickRandom(),
                    _ => ElementType.None
                };
                return ele switch
                {
                    ElementType.RandomExpanded => (ElementType) Random.Shared.Next(1, 10),
                    ElementType.RandomTemuair => (ElementType) Random.Shared.Next(1, 7),
                    _ => ele
                };
            }
        }


    }

    public partial class SpawnGroup
    {
        public string Filename { get; set; }
    }
}

namespace Hybrasyl.Xml
{
    public partial class Nation
    {
        public SpawnPoint RandomSpawnPoint =>
            SpawnPoints.Count > 0 ? SpawnPoints[Random.Shared.Next(0, SpawnPoints.Count)] : default;
    }
}

namespace Hybrasyl.Xml
{
    public partial class ServerConfig
    {
        // In case there is nothing defined in XML, we still need some associations for basic
        // functionality
        [XmlIgnoreAttribute] private static Dictionary<byte, (string key, string setting)> Default = new()
        {
            { 6, ("exchange", "Exchange") },
            { 2, ("group", "Allow Grouping") }
        };

        [XmlIgnoreAttribute] public Dictionary<byte, ClientSetting> SettingsNumberIndex { get; set; }

        [XmlIgnoreAttribute] public Dictionary<string, ClientSetting> SettingsKeyIndex { get; set; }

        public void InitializeClientSettings()
        {
            SettingsNumberIndex = new Dictionary<byte, ClientSetting>();
            SettingsKeyIndex = new Dictionary<string, ClientSetting>();

            for (byte x = 1; x <= 8; x++)
            {
                var newcs = new ClientSetting
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
                    var cs = ClientSettings.FirstOrDefault(predicate: val => val.Number == x);
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
        private List<string> _privilegedUsers = new();
        private List<string> _reservedNames = new();

        public bool AllPrivileged { get; set; }

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