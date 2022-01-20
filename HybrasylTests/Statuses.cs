using System;
using Hybrasyl;
using Hybrasyl.Xml;
using Hybrasyl.Objects;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HybrasylTests;


[Collection("Hybrasyl")]
public class Status
{
    private static HybrasylFixture Fixture;

    [Fact]
    public void ApplyStatus()
    {
        // Apply a status, verify that status exists

    }

    [Fact]
    public void ApplyMuteStatus()
    {
        // Apply mute status, verify that user/creature cannot cast
    }
}