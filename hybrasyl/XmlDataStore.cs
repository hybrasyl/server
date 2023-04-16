using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl
{
    public class XmlDataStore
    {
        private XmlDataStore() {}
        
        private Object Lock { get; set; }
        private XmlDataStore Instance { get; set; }

        public XmlDataStore Store
        {
            get
            {
                lock (Lock)
                {
                    if (Instance == null)
                        Instance = new XmlDataStore();
                    return Instance;
                }
            }
        }
    }
}
