using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

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
        public ElementType Element
        {
            get
            {
                var off = Properties.StatModifiers?.Element?.Offense ?? ElementType.None;
                var def = Properties.StatModifiers?.Element?.Defense ?? ElementType.None;
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
}
