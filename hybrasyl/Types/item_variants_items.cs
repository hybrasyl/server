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
	public partial class item_variants_items
	{
		private int _item_id;
		public virtual int Item_id
		{
			get
			{
				return this._item_id;
			}
			set
			{
				this._item_id = value;
			}
		}
		
		private int _item_variant_id;
		public virtual int Item_variant_id
		{
			get
			{
				return this._item_variant_id;
			}
			set
			{
				this._item_variant_id = value;
			}
		}
		
	}
}
#pragma warning restore 1591
