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

#nullable enable

using Hybrasyl.Subsystems.Players;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     Persists Equipment as a slot dictionary. The wire shape is load-bearing
///     contract; see the golden corpus.
/// </summary>
public class EquipmentJsonConverter : JsonConverter<Equipment>
{
    public override void Write(Utf8JsonWriter writer, Equipment value, JsonSerializerOptions options) =>
        SlotConverter.Write(writer, value, options);

    public override Equipment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SlotConverter.Read(ref reader, new Equipment(Equipment.DefaultSize), options);
}
