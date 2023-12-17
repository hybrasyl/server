using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoonSharp.Interpreter;

namespace Hybrasyl.Objects;

[MoonSharpUserData]
public record CreatureSnapshot
{
    public required StatInfo Stats { get; init; }
    public required string Name { get; init; }
    public Guid Parent { get; init; }
    public DateTime CreationDate { get; } = DateTime.Now;

    public User GetUserObject()
    {
        return Game.World.WorldState.TryGetWorldObject(Parent, out User user) ? user : null;
    }

    public bool IsPlayer => GetUserObject() != null;

}