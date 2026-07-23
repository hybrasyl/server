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

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hybrasyl.Subsystems.Persistence;

/// <summary>
///     Serializes [Persistable] types that implement IEnumerable (Legend, the
///     MessageStore family) as objects. STJ's metadata model classifies them as
///     collections with no override, so the object shape is written by hand from the
///     same <see cref="WirePlan" /> the resolver uses.
/// </summary>
public class OptInEnumerableConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        PersistenceContractResolver.IsOptInEnumerable(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        // Non-null: the constructed converter type always has a parameterless ctor
        (JsonConverter)Activator.CreateInstance(
            typeof(OptInEnumerableConverter<>).MakeGenericType(typeToConvert))!;
}
