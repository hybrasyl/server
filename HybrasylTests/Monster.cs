using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using Xunit;
using Creature = Hybrasyl.Xml.Creature;

namespace HybrasylTests;

[Collection("Hybrasyl")]
public class Monsters
{
    public HybrasylFixture Fixture { get; set; }

    public Monsters(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void MonsterRotation()
    {
        Assert.True(Game.World.WorldData.TryGetValue<Creature>("Gabbaghoul", out var monsterXml),
            "Gabbaghoul test monster not found");
        var monster = new Monster(monsterXml, SpawnFlags.AiDisabled, 99, 3);
    }
}
