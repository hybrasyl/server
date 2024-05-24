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

using System.Text;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Hybrasyl.Servers;

public static class SampleStackExchangeRedisExtensions
{
    public static T Get<T>(this IDatabase cache, string key) => Deserialize<T>(cache.StringGet(key));

    public static object Get(this IDatabase cache, string key) => Deserialize<object>(cache.StringGet(key));

    public static void Set(this IDatabase cache, string key, object value)
    {
        cache.StringSet(key, Serialize(value));
    }

    private static byte[] Serialize(object o, ObjectCreationHandling handling = ObjectCreationHandling.Replace,
        PreserveReferencesHandling refHandling = PreserveReferencesHandling.All)
    {
        if (o == null) return null;
        var settings = new JsonSerializerSettings();
        settings.ObjectCreationHandling = handling;
        settings.PreserveReferencesHandling = refHandling;

        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(o, settings));
    }

    private static T Deserialize<T>(byte[] stream)
    {
        if (stream == null) return default;
        return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(stream));
    }
}