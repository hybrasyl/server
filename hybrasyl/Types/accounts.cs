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
	public partial class accounts
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
		
		private string _nickname;
		public virtual string Nickname
		{
			get
			{
				return this._nickname;
			}
			set
			{
				this._nickname = value;
			}
		}
		
		private short _enabled;
		public virtual short Enabled
		{
			get
			{
				return this._enabled;
			}
			set
			{
				this._enabled = value;
			}
		}
		
		private string _email;
		public virtual string Email
		{
			get
			{
				return this._email;
			}
			set
			{
				this._email = value;
			}
		}
		
		private string _encrypted_password;
		public virtual string Encrypted_password
		{
			get
			{
				return this._encrypted_password;
			}
			set
			{
				this._encrypted_password = value;
			}
		}
		
		private string _reset_password_token;
		public virtual string Reset_password_token
		{
			get
			{
				return this._reset_password_token;
			}
			set
			{
				this._reset_password_token = value;
			}
		}
		
		private DateTime? _reset_password_sent_at;
		public virtual DateTime? Reset_password_sent_at
		{
			get
			{
				return this._reset_password_sent_at;
			}
			set
			{
				this._reset_password_sent_at = value;
			}
		}
		
		private DateTime? _remember_created_at;
		public virtual DateTime? Remember_created_at
		{
			get
			{
				return this._remember_created_at;
			}
			set
			{
				this._remember_created_at = value;
			}
		}
		
		private int? _sign_in_count;
		public virtual int? Sign_in_count
		{
			get
			{
				return this._sign_in_count;
			}
			set
			{
				this._sign_in_count = value;
			}
		}
		
		private DateTime? _current_sign_in_at;
		public virtual DateTime? Current_sign_in_at
		{
			get
			{
				return this._current_sign_in_at;
			}
			set
			{
				this._current_sign_in_at = value;
			}
		}
		
		private DateTime? _last_sign_in_at;
		public virtual DateTime? Last_sign_in_at
		{
			get
			{
				return this._last_sign_in_at;
			}
			set
			{
				this._last_sign_in_at = value;
			}
		}
		
		private string _current_sign_in_ip;
		public virtual string Current_sign_in_ip
		{
			get
			{
				return this._current_sign_in_ip;
			}
			set
			{
				this._current_sign_in_ip = value;
			}
		}
		
		private string _last_sign_in_ip;
		public virtual string Last_sign_in_ip
		{
			get
			{
				return this._last_sign_in_ip;
			}
			set
			{
				this._last_sign_in_ip = value;
			}
		}
		
		private string _confirmation_token;
		public virtual string Confirmation_token
		{
			get
			{
				return this._confirmation_token;
			}
			set
			{
				this._confirmation_token = value;
			}
		}
		
		private DateTime? _confirmed_at;
		public virtual DateTime? Confirmed_at
		{
			get
			{
				return this._confirmed_at;
			}
			set
			{
				this._confirmed_at = value;
			}
		}
		
		private DateTime? _confirmation_sent_at;
		public virtual DateTime? Confirmation_sent_at
		{
			get
			{
				return this._confirmation_sent_at;
			}
			set
			{
				this._confirmation_sent_at = value;
			}
		}
		
		private string _unconfirmed_email;
		public virtual string Unconfirmed_email
		{
			get
			{
				return this._unconfirmed_email;
			}
			set
			{
				this._unconfirmed_email = value;
			}
		}
		
		private int? _failed_attempts;
		public virtual int? Failed_attempts
		{
			get
			{
				return this._failed_attempts;
			}
			set
			{
				this._failed_attempts = value;
			}
		}
		
		private string _unlock_token;
		public virtual string Unlock_token
		{
			get
			{
				return this._unlock_token;
			}
			set
			{
				this._unlock_token = value;
			}
		}
		
		private DateTime? _locked_at;
		public virtual DateTime? Locked_at
		{
			get
			{
				return this._locked_at;
			}
			set
			{
				this._locked_at = value;
			}
		}
		
		private string _authentication_token;
		public virtual string Authentication_token
		{
			get
			{
				return this._authentication_token;
			}
			set
			{
				this._authentication_token = value;
			}
		}
		
		private DateTime? _created_at;
		public virtual DateTime? Created_at
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
		
		private DateTime? _updated_at;
		public virtual DateTime? Updated_at
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
		
		private int? _roles_mask;
		public virtual int? Roles_mask
		{
			get
			{
				return this._roles_mask;
			}
			set
			{
				this._roles_mask = value;
			}
		}
		
	}
}
#pragma warning restore 1591
