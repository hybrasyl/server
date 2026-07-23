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

using Hybrasyl.Subsystems.Players;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     Persists Inventory as a slot dictionary. The wire shape is load-bearing
///     contract; see the golden corpus.
/// </summary>
public class InventoryJsonConverter : JsonConverter<Inventory>
{
    public override void Write(Utf8JsonWriter writer, Inventory value, JsonSerializerOptions options) =>
        SlotConverter.Write(writer, value, options);

    public override Inventory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SlotConverter.Read(ref reader, new Inventory(Inventory.DefaultSize), options);
}
