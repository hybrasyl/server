using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

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

    }
}
