using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HybrasylTests;

[Collection("Hybrasyl")]
public class Monster
{
    public HybrasylFixture Fixture { get; set; }

    public Monster(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void MonsterRotation()
    {

    }
}
