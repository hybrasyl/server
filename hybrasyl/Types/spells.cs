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
	public partial class spells
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
		
		private int _sprite;
		public virtual int Sprite
		{
			get
			{
				return this._sprite;
			}
			set
			{
				this._sprite = value;
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
