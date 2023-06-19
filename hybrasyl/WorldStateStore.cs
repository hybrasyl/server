/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Interfaces;
using Hybrasyl.Messaging;
using Hybrasyl.Objects;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Hybrasyl;


public enum CastableFilter
{
    SkillsOnly,
    SpellsOnly,
    SkillsAndSpells
}


public class WorldStateStore
{
    public static SHA256 sha = SHA256.Create();

    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, dynamic>> _dataStore;
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<dynamic, dynamic>> _index;
    private readonly ConcurrentDictionary<Guid, WorldObject> _indexByGuid = new();

    private readonly ConcurrentDictionary<string, QuestMetadata> _questDataStore = new();

    /// <summary>
    ///     Constructor, takes no arguments.
    /// </summary>
    public WorldStateStore()
    {
        _dataStore = new ConcurrentDictionary<Type, ConcurrentDictionary<string, dynamic>>();
        _index = new ConcurrentDictionary<Type, ConcurrentDictionary<dynamic, dynamic>>();
        RedisTypes = new HashSet<Type>();
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var type in assembly.GetTypes())
            if (type.GetCustomAttributes(typeof(RedisType), false).Length > 0)
                RedisTypes.Add(type);
    }

    public IDatabase Redis => World.DatastoreConnection.GetDatabase();

    private HashSet<Type> RedisTypes { get; }

    /// <summary>
    ///     Normalize keys by converting to lowercase and removing whitespace (this means that
    ///     MiLeTh InN RoOm 1 => milethinnroom1. Collisions are possible here if you are mixing case in
    ///     keys, in which case, I suggest you ask yourself why you're doing that.
    /// </summary>
    /// <param name="key">Dynamic key object, which must provide a ToString</param>
    /// <returns>A normalized string</returns>
    private static string Sanitize(dynamic key) => key.ToString().Normalize().ToLower();

    /// <summary>
    ///     Get a substore for a given type T.
    /// </summary>
    /// <typeparam name="T">The type to fetch</typeparam>
    /// <returns></returns>
    private ConcurrentDictionary<string, dynamic> GetSubStore<T>() where T : IStateStorable
    {
        if (_dataStore.ContainsKey(typeof(T))) return _dataStore[typeof(T)];
        _dataStore.TryAdd(typeof(T), new ConcurrentDictionary<string, dynamic>());
        return _dataStore[typeof(T)];
    }

    /// <summary>
    ///     Get a subindex for the given type T.
    /// </summary>
    /// <typeparam name="T">The type to fetch</typeparam>
    /// <returns></returns>
    private ConcurrentDictionary<dynamic, dynamic> GetSubIndex<T>() where T : IStateStorable
    {
        if (_index.ContainsKey(typeof(T))) return _index[typeof(T)];
        _index.TryAdd(typeof(T), new ConcurrentDictionary<dynamic, dynamic>());
        return _index[typeof(T)];
    }

    /// <summary>
    ///     Given a type and a key, return the typed object matching the key, or a default value.
    /// </summary>
    /// <typeparam name="T">The type to be returned</typeparam>
    /// <param name="key">The key for the object</param>
    /// <returns></returns>
    public T Get<T>(dynamic key) where T : IStateStorable
    {
        if (_dataStore.ContainsKey(typeof(T))) return (T)_dataStore[typeof(T)][Sanitize(key)];
        return default;
    }

    public T GetWorldObject<T>(Guid guid) where T : WorldObject, IStateStorable =>
        _indexByGuid.ContainsKey(guid) ? (T)_indexByGuid[guid] : null;

    public bool TryGetWorldObject<T>(Guid guid, out T obj) where T : WorldObject, IStateStorable
    {
        obj = null;
        if (!_indexByGuid.ContainsKey(guid)) return false;
        obj = (T)_indexByGuid[guid];
        return true;
    }

    public bool RemoveWorldObject<T>(Guid guid) where T : WorldObject, IStateStorable => _indexByGuid.Remove(guid, out _);
    public bool SetWorldObject<T>(Guid guid, T obj) where T : WorldObject, IStateStorable => _indexByGuid.TryAdd(guid, obj);

    /// <summary>
    ///     Return the first of any known type (e.g. first map, first NPC, etc)
    /// </summary>
    /// <typeparam name="T">The type of the object desired</typeparam>
    /// <returns></returns>
    public T First<T>() where T : IStateStorable => (T)_dataStore[typeof(T)].First().Value;

    /// <summary>
    ///     Given a type and a key, return the typed object matching the key in the subindex,
    ///     or a default value.
    /// </summary>
    /// <typeparam name="T">The type to be returned</typeparam>
    /// <param name="key">The index key for the object</param>
    /// <returns>Found object</returns>
    public T GetByIndex<T>(dynamic key) where T : IStateStorable
    {
        if (_index.ContainsKey(typeof(T))) return (T)_index[typeof(T)][Sanitize(key)];
        return default;
    }

    /// <summary>
    ///     Try to find a typed value in the store given a key.
    /// </summary>
    /// <typeparam name="T">The type to be returned</typeparam>
    /// <param name="key">The key</param>
    /// <param name="tresult">The out parameter which will contain the object, if found</param>
    /// <returns>True or false depending on whether or not item was found</returns>
    public bool TryGetValue<T>(dynamic key, out T tresult) where T : IStateStorable
    {
        tresult = default;
        var sub = GetSubStore<T>();
        if (!sub.ContainsKey(Sanitize(key))) return false;
        tresult = (T)sub[Sanitize(key)];
        return true;
    }

    /// <summary>
    ///     Try to find a typed value in the store given an index key.
    /// </summary>
    /// <typeparam name="T">The type to be returned</typeparam>
    /// <param name="key">The index key</param>
    /// <param name="tresult">The out parameter which will contain the object, if found</param>
    /// <returns>True or false depending on whether or not item was found</returns>
    public bool TryGetValueByIndex<T>(dynamic key, out T tresult) where T : IStateStorable
    {
        tresult = default;
        var sub = GetSubIndex<T>();
        if (!sub.ContainsKey(Sanitize(key)))
            //GameLog.Error($"TryGetValueByIndex: type {typeof(T)}: key {key.ToString().Normalize()} not found");
            return false;
        tresult = (T)sub[Sanitize(key)];
        return true;
    }

    /// <summary>
    ///     Store an object in the datastore with the given key.
    /// </summary>
    /// <typeparam name="T">The type to be stored</typeparam>
    /// <param name="key">The key to be used for the object</param>
    /// <param name="value">The actual object to be stored</param>
    /// <returns>Boolean indicating success</returns>
    public bool Set<T>(dynamic key, T value) where T : IStateStorable => GetSubStore<T>().TryAdd(Sanitize(key), value);

    /// <summary>
    ///     Store an object in the datastore with the given key and index key.
    /// </summary>
    /// <typeparam name="T">The type to be stored</typeparam>
    /// <param name="key">The key for the object</param>
    /// <param name="value">The actual object to be stored</param>
    /// <param name="index">The index key for the object</param>
    /// <returns>Boolean indicating success</returns>
    public bool SetWithIndex<T>(dynamic key, T value, dynamic index) where T : IStateStorable => GetSubStore<T>().TryAdd(Sanitize(key), value) &&
                                                                        GetSubIndex<T>().TryAdd(Sanitize(index), value);

    /// <summary>
    ///     Returns all the objects contained in the datastore of the specified type's substore.
    /// </summary>
    /// <typeparam name="T">The type to be returned.</typeparam>
    /// <returns></returns>
    public IEnumerable<T> Values<T>() where T : IStateStorable => GetSubStore<T>().Values.Cast<T>();

    /// <summary>
    ///     Returns all the keys contained in the datastore for the specified type's substore.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IEnumerable<string> Keys<T>() where T : IStateStorable => GetSubStore<T>().Keys;

    /// <summary>
    ///     Checks to see whether a key exists in the datastore for a given type.
    /// </summary>
    /// <typeparam name="T">The type to check</typeparam>
    /// <param name="key">The key to check</param>
    /// <returns>Boolean indicating whether or not the key exists</returns>
    public bool ContainsKey<T>(dynamic key) where T : IStateStorable => GetSubStore<T>().ContainsKey(Sanitize(key));

    /// <summary>
    ///     Return a count of typed objects in the datastore.
    /// </summary>
    /// <typeparam name="T">The type for which to produce a count</typeparam>
    /// <returns>Integer number of objects</returns>
    public int Count<T>() where T : IStateStorable => GetSubStore<T>().Count;

    /// <summary>
    ///     Get an IDictionary which will only contain values of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to return</typeparam>
    /// <returns>IDictionary of objects of the specified type.</returns>
    public IDictionary<string, T> GetDictionary<T>() where T : IStateStorable => (IDictionary<string, T>)_dataStore[typeof(T)];

    /// <summary>
    ///     Remove an object from the datastore.
    /// </summary>
    /// <typeparam name="T">The type of the object to remove</typeparam>
    /// <param name="key">The key corresponding to the object to be removed</param>
    /// <returns></returns>
    public bool Remove<T>(dynamic key) where T : IStateStorable => GetSubStore<T>().TryRemove(Sanitize(key), out dynamic _);

    public bool RemoveIndex<T>(dynamic index) where T : IStateStorable => GetSubIndex<T>().TryRemove(Sanitize(index), out dynamic _);

    // Convenience finder functions below for various non-generic types.
    // This can probably be further genericized, moving forward.

    /// <summary>
    ///     Return a username for a given guid
    /// </summary>
    /// <param name="guid">The guid to look up</param>
    /// <returns>The username or string.empty if not found</returns>
    public string GetNameByGuid(Guid guid)
    {
        if (TryGetValue(guid, out GuidReference reference))
            return reference.UserName;
        return string.Empty;
    }

    public Guid GetGuidByName(string name)
    {
        if (TryGetValueByIndex(name, out GuidReference reference))
            return reference.UserGuid;
        // Does user exist?
        return TryGetUser(name, out var user) ? user.Guid : Guid.Empty;
    }

    public bool TryGetSocialEvent(User name, out SocialEvent socialEvent)
    {
        socialEvent = null;
        if (TryGetValue(name, out socialEvent))
            return true;
        if (TryGetValueByIndex(name.Map.Id, out socialEvent))
            return true;
        return false;
    }

    public bool TryGetAuthInfo(string name, out AuthInfo info)
    {
        info = null;
        var guid = GetGuidByName(name);

        if (guid == Guid.Empty) return false;
        if (TryGetValue(guid, out info)) return true;
        if (TryGetValueByIndex(name, out info)) return true;
        // Fall back to loading from Redis
        info = GetOrCreateByGuid<AuthInfo>(guid, name);
        if (info == null) return false;
        if (!info.IsLoggedIn) return true;
        // If we loaded from Redis, the user (should not) be logged in, so reset state
        info.CurrentState = UserState.Disconnected;
        info.Save();
        return true;
    }

    public T GetOrCreate<T>(GuidReference reference) where T : IStateStorable => GetOrCreateByGuid<T>(reference.UserGuid, reference.UserName);

    public T GetOrCreateByGuid<T>(Guid guid, string index = "") where T : IStateStorable
    {
        var type = typeof(T);
        if (!RedisTypes.Contains(type))
            throw new ArgumentException($"Type {type} is not a Guid referenced Redis type");

        // Check for existence of object locally first
        if (TryGetValue(guid, out T obj))
            return obj;

        // Check for existence of object in Redis
        var storageKey = $"{type.FullName}:{guid}";
        if (Redis.KeyExists(storageKey))
            obj = Redis.Get<T>(storageKey);

        // Fall back to creating it if needed
        if (obj == null)
        {
            obj = (T)Activator.CreateInstance(typeof(T), guid);
            Redis.Set(storageKey, obj);
        }

        // Now that we have the object, update the store
        if (string.IsNullOrEmpty(index))
            Set(guid, obj);
        else
            SetWithIndex(guid, obj, index);

        return obj;
    }

    public Board GetBoard(string name)
    {
        Board newBoard;
        if (ContainsKey<Board>(name)) return Get<Board>(name);
        var newBoardId = Values<Board>().Count() + 1;
        // Check redis first, then fall back to creation
        if (Redis.KeyExists(Board.GetStorageKey(name)))
        {
            newBoard = Redis.Get<Board>(Board.GetStorageKey(name));
            GameLog.DataLogInfo($"Board: loaded {name}");
            newBoard.Id = newBoardId;
        }
        else
        {
            newBoard = new Board(name) { Id = newBoardId };
            newBoard.Save();
            GameLog.DataLogInfo($"Board: Creating {name}");
        }

        SetWithIndex(name, newBoard, newBoard.Id);
        return Get<Board>(name);
    }

    public GuidReference GetGuidReference(string name)
    {
        if (TryGetValueByIndex(name, out GuidReference reference))
            return reference;
        if (TryGetUser(name, out var userobj))
            return GetGuidReference(userobj);
        return null;
    }

    public GuidReference GetGuidReference(User userObj)
    {
        if (TryGetValue(userObj.Guid, out GuidReference reference))
            return reference;

        var guidRef = new GuidReference(userObj.Name)
        {
            AccountGuid = userObj.AccountGuid,
            UserGuid = userObj.Guid
        };

        SetWithIndex(userObj.Guid, guidRef, userObj.Name);
        return Get<GuidReference>(userObj.Guid);
    }

    public bool TryGetUser(string name, out User userobj)
    {
        userobj = null;
        try
        {
            userobj = Redis.Get<User>(User.GetStorageKey(name));
            if (userobj != null)
            {
                // Ensure our guid reference is created when we deserialize a user (if it doesn't already exist)
                GetGuidReference(userobj);
                return true;
            }
        }
        catch (Exception e)
        {
            GameLog.Fatal("{name}: DESERIALIZATION ERROR, bug or corrupt user data: {e}", name, e);
            return false;
        }

        return false;
    }

    public bool RegisterQuest(QuestMetadata data) => _questDataStore.TryAdd(data.Id, data);

    public List<QuestMetadata> QuestMetadata => _questDataStore.Values.ToList();

}