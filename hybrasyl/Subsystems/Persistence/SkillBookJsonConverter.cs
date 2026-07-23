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

using Hybrasyl.Casting;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     Persists SkillBook as a null-padded fixed-size array. The wire shape is
///     load-bearing contract; see the golden corpus.
/// </summary>
public class SkillBookJsonConverter : JsonConverter<SkillBook>
{
    public override void Write(Utf8JsonWriter writer, SkillBook value, JsonSerializerOptions options) =>
        BookSlotConverter.Write(writer, value);

    public override SkillBook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BookSlotConverter.Read(ref reader, new SkillBook());
}
