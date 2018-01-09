// ------------------------------------------------------------------------------
//  <auto-generated>
//    Generated by Xsd2Code++. Version 4.4.0.0
//    <NameSpace>Hybrasyl.Statuses</NameSpace><Collection>List</Collection><codeType>CSharp</codeType><EnableDataBinding>False</EnableDataBinding><GenerateCloneMethod>False</GenerateCloneMethod><GenerateDataContracts>False</GenerateDataContracts><DataMemberNameArg>OnlyIfDifferent</DataMemberNameArg><DataMemberOnXmlIgnore>False</DataMemberOnXmlIgnore><CodeBaseTag>Net20</CodeBaseTag><InitializeFields>AllExceptOptional</InitializeFields><GenerateUnusedComplexTypes>False</GenerateUnusedComplexTypes><GenerateUnusedSimpleTypes>False</GenerateUnusedSimpleTypes><GenerateXMLAttributes>True</GenerateXMLAttributes><OrderXMLAttrib>False</OrderXMLAttrib><EnableLazyLoading>False</EnableLazyLoading><VirtualProp>False</VirtualProp><PascalCase>False</PascalCase><AutomaticProperties>False</AutomaticProperties><PropNameSpecified>None</PropNameSpecified><PrivateFieldName>StartWithUnderscore</PrivateFieldName><PrivateFieldNamePrefix></PrivateFieldNamePrefix><EnableRestriction>False</EnableRestriction><RestrictionMaxLenght>False</RestrictionMaxLenght><RestrictionRegEx>False</RestrictionRegEx><RestrictionRange>False</RestrictionRange><ValidateProperty>False</ValidateProperty><ClassNamePrefix></ClassNamePrefix><ClassLevel>Public</ClassLevel><PartialClass>True</PartialClass><ClassesInSeparateFiles>False</ClassesInSeparateFiles><ClassesInSeparateFilesDir></ClassesInSeparateFilesDir><TrackingChangesEnable>False</TrackingChangesEnable><GenTrackingClasses>False</GenTrackingClasses><HidePrivateFieldInIDE>False</HidePrivateFieldInIDE><EnableSummaryComment>False</EnableSummaryComment><EnableAppInfoSettings>False</EnableAppInfoSettings><EnableExternalSchemasCache>False</EnableExternalSchemasCache><EnableDebug>False</EnableDebug><EnableWarn>False</EnableWarn><ExcludeImportedTypes>False</ExcludeImportedTypes><ExpandNesteadAttributeGroup>False</ExpandNesteadAttributeGroup><CleanupCode>False</CleanupCode><EnableXmlSerialization>False</EnableXmlSerialization><SerializeMethodName>Serialize</SerializeMethodName><DeserializeMethodName>Deserialize</DeserializeMethodName><SaveToFileMethodName>SaveToFile</SaveToFileMethodName><LoadFromFileMethodName>LoadFromFile</LoadFromFileMethodName><EnableEncoding>False</EnableEncoding><EnableXMLIndent>False</EnableXMLIndent><IndentChar>Indent1Space</IndentChar><NewLineAttr>False</NewLineAttr><OmitXML>False</OmitXML><Encoder>UTF8</Encoder><Serializer>XmlSerializer</Serializer><sspNullable>False</sspNullable><sspString>False</sspString><sspCollection>False</sspCollection><sspComplexType>False</sspComplexType><sspSimpleType>False</sspSimpleType><sspEnumType>False</sspEnumType><XmlSerializerEvent>False</XmlSerializerEvent><BaseClassName>EntityBase</BaseClassName><UseBaseClass>False</UseBaseClass><GenBaseClass>False</GenBaseClass><CustomUsings></CustomUsings><AttributesToExlude></AttributesToExlude>
//  </auto-generated>
// ------------------------------------------------------------------------------
#pragma warning disable
namespace Hybrasyl.Statuses
{
    using System;
    using System.Diagnostics;
    using System.Xml.Serialization;
    using System.Collections;
    using System.Xml.Schema;
    using System.ComponentModel;
    using System.Xml;
    using System.Collections.Generic;


    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.hybrasyl.com/XML/Status", IsNullable = false)]
    public partial class Status
    {

        #region Private fields
        private string _category;

        private CastRestriction _castRestriction;

        private Effects _effects;

        private string _prohibitedMessage;

        private string _script;

        private int _duration;

        private int _tick;

        private ushort _icon;

        private string _name;
        #endregion

        public Status()
        {
            this._effects = new Effects();
            this._castRestriction = new CastRestriction();
        }

        public string Category
        {
            get
            {
                return this._category;
            }
            set
            {
                this._category = value;
            }
        }

        public CastRestriction CastRestriction
        {
            get
            {
                return this._castRestriction;
            }
            set
            {
                this._castRestriction = value;
            }
        }

        public Effects Effects
        {
            get
            {
                return this._effects;
            }
            set
            {
                this._effects = value;
            }
        }

        public string ProhibitedMessage
        {
            get
            {
                return this._prohibitedMessage;
            }
            set
            {
                this._prohibitedMessage = value;
            }
        }

        public string Script
        {
            get
            {
                return this._script;
            }
            set
            {
                this._script = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int Duration
        {
            get
            {
                return this._duration;
            }
            set
            {
                this._duration = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int Tick
        {
            get
            {
                return this._tick;
            }
            set
            {
                this._tick = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public ushort Icon
        {
            get
            {
                return this._icon;
            }
            set
            {
                this._icon = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string Name
        {
            get
            {
                return this._name;
            }
            set
            {
                this._name = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class CastRestriction
    {

        #region Private fields
        private string _use;

        private string _receive;
        #endregion

        [System.Xml.Serialization.XmlAttributeAttribute(DataType = "token")]
        public string Use
        {
            get
            {
                return this._use;
            }
            set
            {
                this._use = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute(DataType = "token")]
        public string Receive
        {
            get
            {
                return this._receive;
            }
            set
            {
                this._receive = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class Conditions
    {

        #region Private fields
        private CreatureCondition _set;

        private CreatureCondition _unset;
        #endregion

        public CreatureCondition Set
        {
            get
            {
                return this._set;
            }
            set
            {
                this._set = value;
            }
        }

        public CreatureCondition Unset
        {
            get
            {
                return this._unset;
            }
            set
            {
                this._unset = value;
            }
        }
    }

    [System.FlagsAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.hybrasyl.com/XML/HybrasylCommon")]
    public enum CreatureCondition
    {

        /// <remarks/>
        Freeze = 1,

        /// <remarks/>
        Sleep = 2,

        /// <remarks/>
        Paralyze = 4,

        /// <remarks/>
        Blind = 8,

        /// <remarks/>
        Coma = 16,

        /// <remarks/>
        Poison = 32,

        /// <remarks/>
        Mist = 64,

        /// <remarks/>
        Regen = 128,

        /// <remarks/>
        Sight = 256,

        /// <remarks/>
        Invisible = 512,

        /// <remarks/>
        Mute = 1024,

        /// <remarks/>
        ReflectPhysical = 2048,

        /// <remarks/>
        ReflectMagical = 4096,

        /// <remarks/>
        Invulnerable = 8192,

        /// <remarks/>
        Charm = 16384,

        /// <remarks/>
        IncreaseDamage = 32768,

        /// <remarks/>
        ReduceDamage = 65536,

        /// <remarks/>
        AbsorbSpell = 131072,

        /// <remarks/>
        ProhibitItemUse = 262144,

        /// <remarks/>
        ProhibitEquipChange = 524288,

        /// <remarks/>
        ProhibitSpeech = 1048576,

        /// <remarks/>
        ProhibitWhisper = 2097152,

        /// <remarks/>
        ProhibitShout = 4194304,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class StatModifiers
    {

        #region Private fields
        private sbyte _str;

        private sbyte _int;

        private sbyte _wis;

        private sbyte _con;

        private sbyte _dex;

        private int _hp;

        private int _mp;

        private sbyte _hit;

        private sbyte _dmg;

        private sbyte _ac;

        private sbyte _regen;

        private sbyte _mr;

        private Element _offensiveElement;

        private Element _defensiveElement;

        private float _damageModifier;

        private float _healModifier;

        private DamageType _damageType;

        private float _reflectChance;

        private float _reflectIntensity;
        #endregion

        public StatModifiers()
        {
            this._str = ((sbyte)(0));
            this._int = ((sbyte)(0));
            this._wis = ((sbyte)(0));
            this._con = ((sbyte)(0));
            this._dex = ((sbyte)(0));
            this._hp = 0;
            this._mp = 0;
            this._hit = ((sbyte)(0));
            this._dmg = ((sbyte)(0));
            this._ac = ((sbyte)(0));
            this._regen = ((sbyte)(0));
            this._mr = ((sbyte)(0));
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Str
        {
            get
            {
                return this._str;
            }
            set
            {
                this._str = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Int
        {
            get
            {
                return this._int;
            }
            set
            {
                this._int = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Wis
        {
            get
            {
                return this._wis;
            }
            set
            {
                this._wis = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Con
        {
            get
            {
                return this._con;
            }
            set
            {
                this._con = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Dex
        {
            get
            {
                return this._dex;
            }
            set
            {
                this._dex = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(0)]
        public int Hp
        {
            get
            {
                return this._hp;
            }
            set
            {
                this._hp = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(0)]
        public int Mp
        {
            get
            {
                return this._mp;
            }
            set
            {
                this._mp = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Hit
        {
            get
            {
                return this._hit;
            }
            set
            {
                this._hit = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Dmg
        {
            get
            {
                return this._dmg;
            }
            set
            {
                this._dmg = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Ac
        {
            get
            {
                return this._ac;
            }
            set
            {
                this._ac = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Regen
        {
            get
            {
                return this._regen;
            }
            set
            {
                this._regen = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(sbyte), "0")]
        public sbyte Mr
        {
            get
            {
                return this._mr;
            }
            set
            {
                this._mr = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public Element OffensiveElement
        {
            get
            {
                return this._offensiveElement;
            }
            set
            {
                this._offensiveElement = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public Element DefensiveElement
        {
            get
            {
                return this._defensiveElement;
            }
            set
            {
                this._defensiveElement = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public float DamageModifier
        {
            get
            {
                return this._damageModifier;
            }
            set
            {
                this._damageModifier = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public float HealModifier
        {
            get
            {
                return this._healModifier;
            }
            set
            {
                this._healModifier = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public DamageType DamageType
        {
            get
            {
                return this._damageType;
            }
            set
            {
                this._damageType = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public float ReflectChance
        {
            get
            {
                return this._reflectChance;
            }
            set
            {
                this._reflectChance = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public float ReflectIntensity
        {
            get
            {
                return this._reflectIntensity;
            }
            set
            {
                this._reflectIntensity = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/HybrasylCommon")]
    public enum Element
    {

        /// <remarks/>
        None,

        /// <remarks/>
        Fire,

        /// <remarks/>
        Water,

        /// <remarks/>
        Wind,

        /// <remarks/>
        Earth,

        /// <remarks/>
        Light,

        /// <remarks/>
        Dark,

        /// <remarks/>
        Wood,

        /// <remarks/>
        Metal,

        /// <remarks/>
        Undead,

        /// <remarks/>
        Random,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/HybrasylCommon")]
    public enum DamageType
    {

        /// <remarks/>
        Direct,

        /// <remarks/>
        Physical,

        /// <remarks/>
        Magical,

        /// <remarks/>
        Elemental,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class Damage
    {

        #region Private fields
        private DamageFlags _flags;

        private SimpleQuantity _simple;

        private string _formula;

        private DamageType _type;
        #endregion

        public Damage()
        {
            this._simple = new SimpleQuantity();
        }

        public DamageFlags Flags
        {
            get
            {
                return this._flags;
            }
            set
            {
                this._flags = value;
            }
        }

        public SimpleQuantity Simple
        {
            get
            {
                return this._simple;
            }
            set
            {
                this._simple = value;
            }
        }

        public string Formula
        {
            get
            {
                return this._formula;
            }
            set
            {
                this._formula = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public DamageType Type
        {
            get
            {
                return this._type;
            }
            set
            {
                this._type = value;
            }
        }
    }

    [System.FlagsAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.hybrasyl.com/XML/Status")]
    public enum DamageFlags
    {

        /// <remarks/>
        None = 1,

        /// <remarks/>
        Scaled = 2,

        /// <remarks/>
        Resistance = 4,

        /// <remarks/>
        Threat = 8,

        /// <remarks/>
        Nonlethal = 16,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class SimpleQuantity
    {

        #region Private fields
        private string _min;

        private string _max;

        private string _value;
        #endregion

        public SimpleQuantity()
        {
            this._min = "0";
            this._max = "0";
        }

        [System.Xml.Serialization.XmlAttributeAttribute(DataType = "nonNegativeInteger")]
        [System.ComponentModel.DefaultValueAttribute("0")]
        public string Min
        {
            get
            {
                return this._min;
            }
            set
            {
                this._min = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute(DataType = "nonNegativeInteger")]
        [System.ComponentModel.DefaultValueAttribute("0")]
        public string Max
        {
            get
            {
                return this._max;
            }
            set
            {
                this._max = value;
            }
        }

        [System.Xml.Serialization.XmlTextAttribute(DataType = "nonNegativeInteger")]
        public string Value
        {
            get
            {
                return this._value;
            }
            set
            {
                this._value = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class Heal
    {

        #region Private fields
        private SimpleQuantity _simple;

        private string _formula;
        #endregion

        public Heal()
        {
            this._simple = new SimpleQuantity();
        }

        public SimpleQuantity Simple
        {
            get
            {
                return this._simple;
            }
            set
            {
                this._simple = value;
            }
        }

        public string Formula
        {
            get
            {
                return this._formula;
            }
            set
            {
                this._formula = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class Messages
    {

        #region Private fields
        private string _target;

        private string _source;

        private string _group;

        private string _say;

        private string _shout;
        #endregion

        public string Target
        {
            get
            {
                return this._target;
            }
            set
            {
                this._target = value;
            }
        }

        public string Source
        {
            get
            {
                return this._source;
            }
            set
            {
                this._source = value;
            }
        }

        public string Group
        {
            get
            {
                return this._group;
            }
            set
            {
                this._group = value;
            }
        }

        public string Say
        {
            get
            {
                return this._say;
            }
            set
            {
                this._say = value;
            }
        }

        public string Shout
        {
            get
            {
                return this._shout;
            }
            set
            {
                this._shout = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class Animation
    {

        #region Private fields
        private ushort _id;

        private short _speed;
        #endregion

        public Animation()
        {
            this._speed = ((short)(100));
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public ushort Id
        {
            get
            {
                return this._id;
            }
            set
            {
                this._id = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(typeof(short), "100")]
        public short Speed
        {
            get
            {
                return this._speed;
            }
            set
            {
                this._speed = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class StatusAnimations
    {

        #region Private fields
        private Animation _target;

        private Animation _spellEffect;
        #endregion

        public StatusAnimations()
        {
            this._spellEffect = new Animation();
            this._target = new Animation();
        }

        public Animation Target
        {
            get
            {
                return this._target;
            }
            set
            {
                this._target = value;
            }
        }

        public Animation SpellEffect
        {
            get
            {
                return this._spellEffect;
            }
            set
            {
                this._spellEffect = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class ModifierEffect
    {

        #region Private fields
        private StatusAnimations _animations;

        private ModifierEffectSound _sound;

        private Messages _messages;

        private Heal _heal;

        private Damage _damage;

        private StatModifiers _statModifiers;

        private Conditions _conditions;
        #endregion

        public ModifierEffect()
        {
            this._conditions = new Conditions();
            this._statModifiers = new StatModifiers();
            this._damage = new Damage();
            this._heal = new Heal();
            this._messages = new Messages();
            this._sound = new ModifierEffectSound();
            this._animations = new StatusAnimations();
        }

        public StatusAnimations Animations
        {
            get
            {
                return this._animations;
            }
            set
            {
                this._animations = value;
            }
        }

        public ModifierEffectSound Sound
        {
            get
            {
                return this._sound;
            }
            set
            {
                this._sound = value;
            }
        }

        public Messages Messages
        {
            get
            {
                return this._messages;
            }
            set
            {
                this._messages = value;
            }
        }

        public Heal Heal
        {
            get
            {
                return this._heal;
            }
            set
            {
                this._heal = value;
            }
        }

        public Damage Damage
        {
            get
            {
                return this._damage;
            }
            set
            {
                this._damage = value;
            }
        }

        public StatModifiers StatModifiers
        {
            get
            {
                return this._statModifiers;
            }
            set
            {
                this._statModifiers = value;
            }
        }

        public Conditions Conditions
        {
            get
            {
                return this._conditions;
            }
            set
            {
                this._conditions = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class ModifierEffectSound
    {

        #region Private fields
        private byte _id;
        #endregion

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte Id
        {
            get
            {
                return this._id;
            }
            set
            {
                this._id = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.7.2556.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.hybrasyl.com/XML/Status")]
    public partial class Effects
    {

        #region Private fields
        private ModifierEffect _onApply;

        private ModifierEffect _onTick;

        private ModifierEffect _onRemove;
        #endregion

        public Effects()
        {
            this._onRemove = new ModifierEffect();
            this._onTick = new ModifierEffect();
            this._onApply = new ModifierEffect();
        }

        public ModifierEffect OnApply
        {
            get
            {
                return this._onApply;
            }
            set
            {
                this._onApply = value;
            }
        }

        public ModifierEffect OnTick
        {
            get
            {
                return this._onTick;
            }
            set
            {
                this._onTick = value;
            }
        }

        public ModifierEffect OnRemove
        {
            get
            {
                return this._onRemove;
            }
            set
            {
                this._onRemove = value;
            }
        }
    }
}
#pragma warning restore
