// This file is part of Project Hybrasyl.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
//
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
//
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
//
// (C) 2020-2026 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Casting;
using Hybrasyl.Subsystems.Persistence;
using Hybrasyl.Subsystems.Players;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using PlayerInventory = Hybrasyl.Subsystems.Players.Inventory;

namespace Hybrasyl.Tests;

/// <summary>
///     The resolver and enumerable-converter paths honor IJsonOnDeserialized; the
///     hand-written slot converters must too, or a future implementor silently loses
///     its post-load hook.
/// </summary>
[Collection("Hybrasyl")]
public class ConverterCallbackTests
{
    [Fact]
    public void SlotConverterReadFiresDeserializeCallback()
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("{}"));
        reader.Read();
        var target = SlotConverter.Read(ref reader, new CallbackInventory(), RedisJsonSerializer.Options);
        Assert.True(target.CallbackFired);
    }

    [Fact]
    public void BookSlotConverterReadFiresDeserializeCallback()
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("[]"));
        reader.Read();
        var target = BookSlotConverter.Read(ref reader, new CallbackBook());
        Assert.True(target.CallbackFired);
    }

    private sealed class CallbackInventory() : PlayerInventory(1), IJsonOnDeserialized
    {
        public bool CallbackFired { get; private set; }
        void IJsonOnDeserialized.OnDeserialized() => CallbackFired = true;
    }

    private sealed class CallbackBook : Book, IJsonOnDeserialized
    {
        public bool CallbackFired { get; private set; }
        void IJsonOnDeserialized.OnDeserialized() => CallbackFired = true;
    }
}
