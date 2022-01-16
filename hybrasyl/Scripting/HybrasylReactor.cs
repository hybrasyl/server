using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    [MoonSharpUserData]

    public class HybrasylReactor
    {
        internal Reactor Reactor { get; set; }
        public HybrasylUser Origin => Reactor.Origin is User u ? new HybrasylUser(u) : null;
        public byte X => Reactor.X;
        public byte Y => Reactor.Y;
        public bool Blocking => Reactor.Blocking;
        public int Uses => Reactor.Uses;
        public long Expiration => ((DateTimeOffset)Reactor.Expiration).ToUnixTimeSeconds();

        public HybrasylReactor(Reactor obj)
        {
            Reactor = obj;
        }

    }
}
