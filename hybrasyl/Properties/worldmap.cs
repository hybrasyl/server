//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Hybrasyl.Properties
{
    using System;
    using System.Collections.Generic;
    
    public partial class worldmap
    {
        public worldmap()
        {
            this.worldwarps = new HashSet<worldwarp>();
            this.worldmap_points = new HashSet<worldmap_points>();
        }
    
        public int id { get; set; }
        public string name { get; set; }
        public string client_map { get; set; }
        public System.DateTime created_at { get; set; }
        public System.DateTime updated_at { get; set; }
    
        public virtual ICollection<worldwarp> worldwarps { get; set; }
        public virtual ICollection<worldmap_points> worldmap_points { get; set; }
    }
}
