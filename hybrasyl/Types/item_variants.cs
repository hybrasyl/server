#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the ClassGenerator.ttinclude code generation file.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Data.Common;
using System.Collections.Generic;
using Telerik.OpenAccess;
using Telerik.OpenAccess.Metadata;
using Telerik.OpenAccess.Data.Common;
using Telerik.OpenAccess.Metadata.Fluent;
using Telerik.OpenAccess.Metadata.Fluent.Advanced;

namespace Hybrasyl	
{
	public partial class item_variants
	{
		private int _id;
		public virtual int Id
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
		
		private string _name;
		public virtual string Name
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
		
		private string _modifier;
		public virtual string Modifier
		{
			get
			{
				return this._modifier;
			}
			set
			{
				this._modifier = value;
			}
		}
		
		private string _effect_script_name;
		public virtual string Effect_script_name
		{
			get
			{
				return this._effect_script_name;
			}
			set
			{
				this._effect_script_name = value;
			}
		}
		
		private string _weight;
		public virtual string Weight
		{
			get
			{
				return this._weight;
			}
			set
			{
				this._weight = value;
			}
		}
		
		private string _max_stack;
		public virtual string Max_stack
		{
			get
			{
				return this._max_stack;
			}
			set
			{
				this._max_stack = value;
			}
		}
		
		private string _max_durability;
		public virtual string Max_durability
		{
			get
			{
				return this._max_durability;
			}
			set
			{
				this._max_durability = value;
			}
		}
		
		private string _hp;
		public virtual string Hp
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
		
		private string _mp;
		public virtual string Mp
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
		
		private string _str;
		public virtual string Str
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
		
		private string _int;
		public virtual string Int
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
		
		private string _wis;
		public virtual string Wis
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
		
		private string _con;
		public virtual string Con
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
		
		private string _dex;
		public virtual string Dex
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
		
		private string _hit;
		public virtual string Hit
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
		
		private string _ac;
		public virtual string Ac
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
		
		private string _dmg;
		public virtual string Dmg
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
		
		private string _mr;
		public virtual string Mr
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
		
		private string _max_s_dmg;
		public virtual string Max_s_dmg
		{
			get
			{
				return this._max_s_dmg;
			}
			set
			{
				this._max_s_dmg = value;
			}
		}
		
		private string _min_s_dmg;
		public virtual string Min_s_dmg
		{
			get
			{
				return this._min_s_dmg;
			}
			set
			{
				this._min_s_dmg = value;
			}
		}
		
		private string _max_l_dmg;
		public virtual string Max_l_dmg
		{
			get
			{
				return this._max_l_dmg;
			}
			set
			{
				this._max_l_dmg = value;
			}
		}
		
		private string _min_l_dmg;
		public virtual string Min_l_dmg
		{
			get
			{
				return this._min_l_dmg;
			}
			set
			{
				this._min_l_dmg = value;
			}
		}
		
		private string _value;
		public virtual string Value
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
		
		private string _regen;
		public virtual string Regen
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
		
		private int? _level;
		public virtual int? Level
		{
			get
			{
				return this._level;
			}
			set
			{
				this._level = value;
			}
		}
		
		private int? _ab;
		public virtual int? Ab
		{
			get
			{
				return this._ab;
			}
			set
			{
				this._ab = value;
			}
		}
		
		private int? _element;
		public virtual int? Element
		{
			get
			{
				return this._element;
			}
			set
			{
				this._element = value;
			}
		}
		
		private int _bodystyle;
		public virtual int Bodystyle
		{
			get
			{
				return this._bodystyle;
			}
			set
			{
				this._bodystyle = value;
			}
		}
		
		private int? _color;
		public virtual int? Color
		{
			get
			{
				return this._color;
			}
			set
			{
				this._color = value;
			}
		}
		
		private short? _enchantable;
		public virtual short? Enchantable
		{
			get
			{
				return this._enchantable;
			}
			set
			{
				this._enchantable = value;
			}
		}
		
		private short? _depositable;
		public virtual short? Depositable
		{
			get
			{
				return this._depositable;
			}
			set
			{
				this._depositable = value;
			}
		}
		
		private short? _bound;
		public virtual short? Bound
		{
			get
			{
				return this._bound;
			}
			set
			{
				this._bound = value;
			}
		}
		
		private short? _vendorable;
		public virtual short? Vendorable
		{
			get
			{
				return this._vendorable;
			}
			set
			{
				this._vendorable = value;
			}
		}
		
		private short? _tailorable;
		public virtual short? Tailorable
		{
			get
			{
				return this._tailorable;
			}
			set
			{
				this._tailorable = value;
			}
		}
		
		private short? _smithable;
		public virtual short? Smithable
		{
			get
			{
				return this._smithable;
			}
			set
			{
				this._smithable = value;
			}
		}
		
		private short? _consecratable;
		public virtual short? Consecratable
		{
			get
			{
				return this._consecratable;
			}
			set
			{
				this._consecratable = value;
			}
		}
		
		private short? _perishable;
		public virtual short? Perishable
		{
			get
			{
				return this._perishable;
			}
			set
			{
				this._perishable = value;
			}
		}
		
		private short? _exchangeable;
		public virtual short? Exchangeable
		{
			get
			{
				return this._exchangeable;
			}
			set
			{
				this._exchangeable = value;
			}
		}
		
		private short? _consecratable_variant;
		public virtual short? Consecratable_variant
		{
			get
			{
				return this._consecratable_variant;
			}
			set
			{
				this._consecratable_variant = value;
			}
		}
		
		private short? _tailorable_variant;
		public virtual short? Tailorable_variant
		{
			get
			{
				return this._tailorable_variant;
			}
			set
			{
				this._tailorable_variant = value;
			}
		}
		
		private short? _smithable_variant;
		public virtual short? Smithable_variant
		{
			get
			{
				return this._smithable_variant;
			}
			set
			{
				this._smithable_variant = value;
			}
		}
		
		private short? _enchantable_variant;
		public virtual short? Enchantable_variant
		{
			get
			{
				return this._enchantable_variant;
			}
			set
			{
				this._enchantable_variant = value;
			}
		}
		
		private short? _elemental_variant;
		public virtual short? Elemental_variant
		{
			get
			{
				return this._elemental_variant;
			}
			set
			{
				this._elemental_variant = value;
			}
		}
		
		private DateTime _created_at;
		public virtual DateTime Created_at
		{
			get
			{
				return this._created_at;
			}
			set
			{
				this._created_at = value;
			}
		}
		
		private DateTime _updated_at;
		public virtual DateTime Updated_at
		{
			get
			{
				return this._updated_at;
			}
			set
			{
				this._updated_at = value;
			}
		}
		
	}
}
#pragma warning restore 1591
