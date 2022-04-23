using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    public class ScriptExecutionResult
    {
        public bool Success { get; set; }
        public DynValue Return { get; set; }
        public LuaException Exception { get; set; }
    }

    public class ScriptEnvironment
    {
        public Dictionary<string, DynValue> Variables;
        public string Function;
        public WorldObject Invoker = null;
        public WorldObject Source = null;
        public WorldObject Target = null;
    }

}
