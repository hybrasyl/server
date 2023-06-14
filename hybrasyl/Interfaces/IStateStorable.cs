using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Interfaces
{
    // This exists to identify all usages of WorldStateData, to ensure we are not cross-mixing types between
    // WorldStateData and XMLManager / WorldStoreData, and to reduce / eliminate bugs from extracting all XML
    // type access out to the new XM library. These types of errors will be caught at the compiler level as
    // WorldStateData now has where clauses only allowing types implementing this interface to be used with it.
    //
    // It may be used for more in the future to make WorldStateData similar in patterns to XMLManager.
    public interface IStateStorable
    {
    }
}
