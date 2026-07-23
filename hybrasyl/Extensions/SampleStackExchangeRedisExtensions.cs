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
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using Hybrasyl.Subsystems.Persistence;
using StackExchange.Redis;
using System.IO;
using System.Text.Json;

namespace Hybrasyl.Extensions;

public static class StackExchangeRedisExtensions
{
    public static T Get<T>(this IDatabase cache, string key)
    {
        try
        {
            return RedisJsonSerializer.Deserialize<T>(cache.StringGet(key));
        }
        catch (JsonException e)
        {
            // Corrupt data should fail loudly, but a bare JsonException is undiagnosable
            throw new InvalidDataException(
                $"Redis key {key}: stored {typeof(T).Name} is corrupt or unreadable: {e.Message}", e);
        }
    }

    public static object Get(this IDatabase cache, string key) => RedisJsonSerializer.Deserialize<object>(cache.StringGet(key));

    public static void Set(this IDatabase cache, string key, object value)
    {
        cache.StringSet(key, RedisJsonSerializer.Serialize(value));
    }
}