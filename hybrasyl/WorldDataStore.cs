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

using Hybrasyl.Messaging;
using Hybrasyl.Objects;
using Hybrasyl.Xml;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Hybrasyl
{

    //    public interface IWorldDataStorable
    //    {
    //        public string DataStoreKey { get; }
    //        public string Index { get; }
    //        public List<string> Tags { get; }
    //    }

    //    public interface IWorldDataStore<T>
    //    {
    //        public T Get(string key);
    //        public T First();
    //        public T GetByIndex(string key);
    //        public bool TryGetValue(string key, out T tresult);
    //        public bool TryGetValueByIndex(string key, out T tresult);
    //        public bool Set(string key, T value);
    //        public bool SetWithIndex(string key, T value, string index);
    //        public bool SetWithIndexAndTags(string key, T value, string index, IEnumerable<string> tags);
    //        public IEnumerable<T> Values();
    //        public IEnumerable<string> Keys();
    //        public IEnumerable<string> Tags();
    //        public bool ContainsKey(string key);
    //        public bool ContainsTag(string tag);
    //        public int Count();
    //        public IDictionary<string, T> GetDictionary();
    //        public bool Remove(string  key);
    //        public bool RemoveIndex(string index);
    //        public bool RemoveByTag(string tag);
    //        public List<T> Find(string name);
    ////        public bool ImportAll(XmlLoadResponse<T> response) where T : IHybrasylLoadable<T>;
    //    }
    //    // WorldDataStore[Xml.Item].Get()

    //    public class WorldDataStore<T> : IWorldDataStore<T>
    //    {

    //        private ConcurrentDictionary<string, T> DataStore;
    //        private ConcurrentDictionary<string, T> Index;
    //        private ConcurrentDictionary<string, HashSet<T>> ByTags;

    //        private HashSet<string> Tags;
    //        public static SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider();

    //        static string Sanitize(dynamic key) => key.ToString().Normalize().ToLower();

    //        /// <summary>
    //        /// Given a type and a key, return the typed object matching the key, or a default value.
    //        /// </summary>
    //        /// <typeparam name="T">The type to be returned</typeparam>
    //        /// <param name="key">The key for the object</param>
    //        /// <returns></returns>
    //        public T Get(string key)
    //        {
    //            var s = Sanitize(key);
    //            if (DataStore.ContainsKey(s))
    //            { 
    //                return DataStore[s];
    //            }
    //            return default;
    //        }

    //        /// <summary>
    //        /// Return the first of any known type (e.g. first map, first NPC, etc)
    //        /// </summary>
    //        /// <returns>First </returns>
    //        public T First() => DataStore.First().Value;

    //        /// <summary>
    //        /// Given a type and a key, return the typed object matching the key in the subindex,
    //        /// or a default value.
    //        /// </summary>
    //        /// <typeparam name="T">The type to be returned</typeparam>
    //        /// <param name="key">The index key for the object</param>
    //        /// <returns>Found object</returns>

    //        public T GetByIndex(string key)
    //        {
    //            var s = Sanitize(key);

    //            if (Index.ContainsKey(s))
    //            {
    //                return Index[s];
    //            }
    //            return default;
    //        }

    //        /// <summary>
    //        /// Try to find a typed value in the store given a key.
    //        /// </summary>
    //        /// <typeparam name="T">The type to be returned</typeparam>
    //        /// <param name="key">The key</param>
    //        /// <param name="tresult">The out parameter which will contain the object, if found</param>
    //        /// <returns>True or false depending on whether or not item was found</returns>
    //        public bool TryGetValue(string key, out T tresult)
    //        {
    //            tresult = default;
    //            var s = Sanitize(key);
    //            if (!DataStore.ContainsKey(s)) return false;
    //            tresult = DataStore[s];
    //            return true;
    //        }

    //        /// <summary>
    //        /// Try to find a typed value in the store given an index key.
    //        /// </summary>
    //        /// <typeparam name="T">The type to be returned</typeparam>
    //        /// <param name="key">The index key</param>
    //        /// <param name="tresult">The out parameter which will contain the object, if found</param>
    //        /// <returns>True or false depending on whether or not item was found</returns>
    //        public bool TryGetValueByIndex(string key, out T tresult)
    //        {
    //            tresult = default(T);
    //            var s = Sanitize(key);
    //            if (!Index.ContainsKey(s))
    //            {
    //                //GameLog.Error($"TryGetValueByIndex: type {typeof(T)}: key {key.ToString().Normalize()} not found");
    //                return false;
    //            }
    //            tresult = Index[s];
    //            return true;
    //        }

    //        public IEnumerable<T> GetByTag(string tag)
    //        {
    //            var s = Sanitize(tag);
    //            if (!ByTags.ContainsKey(s))
    //                return new HashSet<T>();
    //            return ByTags[s];
    //        }

    //        /// <summary>
    //        /// Store an object in the datastore with the given key.
    //        /// </summary>
    //        /// <typeparam name="T">The type to be stored</typeparam>
    //        /// <param name="key">The key to be used for the object</param>
    //        /// <param name="value">The actual object to be stored</param>
    //        /// <returns>Boolean indicating success</returns>
    //        public bool Set(string key, T value) => DataStore.TryAdd(Sanitize(key), value);

    //        /// <summary>
    //        /// Store an object in the datastore with the given key and index key.
    //        /// </summary>
    //        /// <typeparam name="T">The type to be stored</typeparam>
    //        /// <param name="key">The key for the object</param>
    //        /// <param name="value">The actual object to be stored</param>
    //        /// <param name="index">The index key for the object</param>
    //        /// <returns>Boolean indicating success</returns>
    //        public bool SetWithIndex(dynamic key, T value, dynamic index) => DataStore.TryAdd(Sanitize(key), value) &&
    //           Index.TryAdd(Sanitize(index), value);

    //        /// <summary>
    //        /// Returns all the objects contained in the datastore of the specified type's substore.
    //        /// </summary>
    //        /// <typeparam name="T">The type to be returned.</typeparam>
    //        /// <returns></returns>      
    //        public IEnumerable<T> Values() => DataStore.Values.Cast<T>();

    //        /// <summary>
    //        /// Returns all the keys contained in the datastore for the specified type's substore.
    //        /// </summary>
    //        /// <typeparam name="T"></typeparam>
    //        /// <returns></returns>
    //        public IEnumerable<string> Keys() => DataStore.Keys;

    //        /// <summary>
    //        /// Checks to see whether a key exists in the datastore for a given type.
    //        /// </summary>
    //        /// <typeparam name="T">The type to check</typeparam>
    //        /// <param name="key">The key to check</param>
    //        /// <returns>Boolean indicating whether or not the key exists</returns>
    //        public bool ContainsKey(string key) => DataStore.ContainsKey(Sanitize(key));

    //        /// <summary>
    //        /// Return a count of typed objects in the datastore.
    //        /// </summary>
    //        /// <typeparam name="T">The type for which to produce a count</typeparam>
    //        /// <returns>Integer number of objects</returns>
    //        public int Count() => DataStore.Count;

    //        /// <summary>
    //        /// Get an IDictionary which will only contain values of the specified type.
    //        /// </summary>
    //        /// <typeparam name="T">The type to return</typeparam>
    //        /// <returns>IDictionary of objects of the specified type.</returns>
    //        public IDictionary<string, T> GetDictionary() => DataStore;

    //        /// <summary>
    //        /// Remove an object from the datastore.
    //        /// </summary>
    //        /// <typeparam name="T">The type of the object to remove</typeparam>
    //        /// <param name="key">The key corresponding to the object to be removed</param>
    //        /// <returns></returns>
    //        public bool Remove(string key) 
    //        {
    //            if (DataStore.TryRemove(Sanitize(key), out T tresult))
    //            {
    //                if (tresult is IWorldDataStorable wds) {
    //                    // Also try to remove item from tag sets and indexes
    //                    Index.TryRemove(wds.Index, out T _);
    //                    foreach (var tag in wds.Tags)
    //                    {
    //                        if (ByTags.ContainsKey(tag))
    //                            ByTags[tag].Remove(tresult);
    //                    }
    //                }
    //                return true;
    //            }
    //            return false;
    //        }

    //        public bool RemoveIndex(string index)
    //        {
    //            return false;
    //        }
    //    }

    //    public class WorldCache<T> : WorldData<T>, IWorldDataStore<T>
    //    {


    //    }

    //    public class DataStoreType  : System.Attribute
    //    {
    //        public Type StoreType;

    //    }

    //    public class UuidReferenceDataStore : WorldData<UuidReference>, IWorldDataStore<UuidReference>
    //    { }

    public enum CastableFilter
    {
        SkillsOnly,
        SpellsOnly,
        SkillsAndSpells
    };

    //public class DataStorable : Attribute
    //{
    //    public Type SupportedType;
    //}

    //public static class WorldDataStoreNew
    //{
    //    private static HashSet<Type> SupportedTypes = new HashSet<Type>();

    //    public static IEnumerable<Type> GetStorableTypes()
    //    {
    //        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
    //        {
    //            if (type.GetCustomAttributes(typeof(DataStorable), true).Length > 0)
    //            {
    //                yield return type;
    //            }
    //        }
    //    }

    //    private static ConcurrentDictionary<Type, dynamic> DataStores = new ConcurrentDictionary<Type, dynamic>();

    //    static WorldDataStoreNew()
    //    {
    //        // Find and register data stores
    //        foreach (Type type in GetStorableTypes())
    //        {
    //            SupportedTypes.Add(type);                
    //        }
    //        foreach (Type type in SupportedTypes)
    //        {
    //            var storeType = typeof(WorldDataStore<>).MakeGenericType(type.GetType());
    //            var storeInstance = Activator.CreateInstance(storeType);
    //            DataStores.TryAdd(type, storeInstance);
    //        }
    //    }

    //    public static WorldDataStore<T> GetDataStore<T>()
    //    {
    //        if (SupportedTypes.Contains(typeof(T)))
    //        {
    //            return DataStores[typeof(T)] as WorldDataStore<T>;
    //        }
    //        else
    //            throw new ArgumentException("Unsupported data type");     
    //    }

    //}

    public partial class WorldDataStore
    {

        /// <summary>
        /// Normalize keys by converting to lowercase and removing whitespace (this means that 
        /// MiLeTh InN RoOm 1 => milethinnroom1. Collisions are possible here if you are mixing case in
        /// keys, in which case, I suggest you ask yourself why you're doing that.
        /// </summary>
        /// <param name="key">Dynamic key object, which must provide a ToString</param>
        /// <returns>A normalized string</returns>
        static string Sanitize(dynamic key) => key.ToString().Normalize().ToLower();

        private ConcurrentDictionary<Type, ConcurrentDictionary<string, dynamic>> _dataStore;
        private ConcurrentDictionary<Type, ConcurrentDictionary<dynamic, dynamic>> _index;
        public static SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider();

        // TODO: refactor WDS to support multiple indexes for stores. For now we need 
        // a way to easily retrieve a castable or list of castables based on category
        private ConcurrentDictionary<string, HashSet<Castable>> CastableIndex;

        // TODO: now we really need to do the above
        public ConcurrentDictionary<string, HashSet<Item>> ItemByCategory;

        public IDatabase Redis => World.DatastoreConnection.GetDatabase();

        private HashSet<Type> RedisTypes { get; set; } 

        /// <summary>
        /// Constructor, takes no arguments.
        /// </summary>
        public WorldDataStore()
        {
            _dataStore = new ConcurrentDictionary<Type, ConcurrentDictionary<string, dynamic>>();
            _index = new ConcurrentDictionary<Type, ConcurrentDictionary<dynamic, dynamic>>();
            RedisTypes = new HashSet<Type>();
            CastableIndex = new ConcurrentDictionary<string, HashSet<Castable>>();
            ItemByCategory = new ConcurrentDictionary<string, HashSet<Item>>();
            var assembly = Assembly.GetExecutingAssembly();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(RedisType), false).Length > 0)
                    RedisTypes.Add(type);
            }
        }

        /// <summary>
        /// Get a substore for a given type T.
        /// </summary>
        /// <typeparam name="T">The type to fetch</typeparam>
        /// <returns></returns>
        private ConcurrentDictionary<string, dynamic> GetSubStore<T>()
        {
            if (_dataStore.ContainsKey(typeof(T)))
            {
                return _dataStore[typeof(T)];
            }
            _dataStore.TryAdd(typeof(T), new ConcurrentDictionary<string, dynamic>());
            return _dataStore[typeof(T)];
        }

        /// <summary>
        /// Get a subindex for the given type T.
        /// </summary>
        /// <typeparam name="T">The type to fetch</typeparam>
        /// <returns></returns>
        private ConcurrentDictionary<dynamic, dynamic> GetSubIndex<T>()
        {
            if (_index.ContainsKey(typeof(T)))
            {
                return _index[typeof(T)];
            }
            _index.TryAdd(typeof(T), new ConcurrentDictionary<dynamic, dynamic>());
            return _index[typeof(T)];
        }

        /// <summary>
        /// Given a type and a key, return the typed object matching the key, or a default value.
        /// </summary>
        /// <typeparam name="T">The type to be returned</typeparam>
        /// <param name="key">The key for the object</param>
        /// <returns></returns>
        public T Get<T>(dynamic key)
        {
            if (_dataStore.ContainsKey(typeof(T)))
            {
                return (T)_dataStore[typeof(T)][Sanitize(key)];
            }
            return default(T);
        }

        /// <summary>
        /// Return the first of any known type (e.g. first map, first NPC, etc)
        /// </summary>
        /// <typeparam name="T">The type of the object desired</typeparam>
        /// <returns></returns>

        public T First<T>() => (T)_dataStore[typeof(T)].First().Value;

        /// <summary>
        /// Given a type and a key, return the typed object matching the key in the subindex,
        /// or a default value.
        /// </summary>
        /// <typeparam name="T">The type to be returned</typeparam>
        /// <param name="key">The index key for the object</param>
        /// <returns>Found object</returns>

        public T GetByIndex<T>(dynamic key)
        {
            if (_index.ContainsKey(typeof(T)))
            {
                return (T)_index[typeof(T)][Sanitize(key)];
            }
            return default(T);
        }

        /// <summary>
        /// Try to find a typed value in the store given a key.
        /// </summary>
        /// <typeparam name="T">The type to be returned</typeparam>
        /// <param name="key">The key</param>
        /// <param name="tresult">The out parameter which will contain the object, if found</param>
        /// <returns>True or false depending on whether or not item was found</returns>
        public bool TryGetValue<T>(dynamic key, out T tresult)
        {
            tresult = default(T);
            var sub = GetSubStore<T>();
            if (!sub.ContainsKey(Sanitize(key))) return false;
            tresult = (T)sub[Sanitize(key)];
            return true;
        }

        /// <summary>
        /// Try to find a typed value in the store given an index key.
        /// </summary>
        /// <typeparam name="T">The type to be returned</typeparam>
        /// <param name="key">The index key</param>
        /// <param name="tresult">The out parameter which will contain the object, if found</param>
        /// <returns>True or false depending on whether or not item was found</returns>
        public bool TryGetValueByIndex<T>(dynamic key, out T tresult)
        {
            tresult = default(T);
            var sub = GetSubIndex<T>();
            if (!sub.ContainsKey(Sanitize(key)))
            {
                //GameLog.Error($"TryGetValueByIndex: type {typeof(T)}: key {key.ToString().Normalize()} not found");
                return false;
            }
            tresult = (T)sub[Sanitize(key)];
            return true;
        }

        /// <summary>
        /// Store an object in the datastore with the given key.
        /// </summary>
        /// <typeparam name="T">The type to be stored</typeparam>
        /// <param name="key">The key to be used for the object</param>
        /// <param name="value">The actual object to be stored</param>
        /// <returns>Boolean indicating success</returns>
        public bool Set<T>(dynamic key, T value) => GetSubStore<T>().TryAdd(Sanitize(key), value);

        /// <summary>
        /// Store an object in the datastore with the given key and index key.
        /// </summary>
        /// <typeparam name="T">The type to be stored</typeparam>
        /// <param name="key">The key for the object</param>
        /// <param name="value">The actual object to be stored</param>
        /// <param name="index">The index key for the object</param>
        /// <returns>Boolean indicating success</returns>
        public bool SetWithIndex<T>(dynamic key, T value, dynamic index) => GetSubStore<T>().TryAdd(Sanitize(key), value) &&
            GetSubIndex<T>().TryAdd(Sanitize(index), value);

        /// <summary>
        /// Returns all the objects contained in the datastore of the specified type's substore.
        /// </summary>
        /// <typeparam name="T">The type to be returned.</typeparam>
        /// <returns></returns>      
        public IEnumerable<T> Values<T>() => GetSubStore<T>().Values.Cast<T>();

        /// <summary>
        /// Returns all the keys contained in the datastore for the specified type's substore.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<string> Keys<T>() => GetSubStore<T>().Keys;

        /// <summary>
        /// Checks to see whether a key exists in the datastore for a given type.
        /// </summary>
        /// <typeparam name="T">The type to check</typeparam>
        /// <param name="key">The key to check</param>
        /// <returns>Boolean indicating whether or not the key exists</returns>
        public bool ContainsKey<T>(dynamic key) => GetSubStore<T>().ContainsKey(Sanitize(key));

        /// <summary>
        /// Return a count of typed objects in the datastore.
        /// </summary>
        /// <typeparam name="T">The type for which to produce a count</typeparam>
        /// <returns>Integer number of objects</returns>
        public int Count<T>() => GetSubStore<T>().Count;

        /// <summary>
        /// Get an IDictionary which will only contain values of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to return</typeparam>
        /// <returns>IDictionary of objects of the specified type.</returns>
        public IDictionary<string, T> GetDictionary<T>() => (IDictionary<string, T>)_dataStore[typeof(T)];

        /// <summary>
        /// Remove an object from the datastore.
        /// </summary>
        /// <typeparam name="T">The type of the object to remove</typeparam>
        /// <param name="key">The key corresponding to the object to be removed</param>
        /// <returns></returns>
        public bool Remove<T>(dynamic key)
        {
            return GetSubStore<T>().TryRemove(Sanitize(key), out dynamic _);
        }

        public bool RemoveIndex<T>(dynamic index)
        {
            return GetSubIndex<T>().TryRemove(Sanitize(index), out dynamic _);
        }


        // Convenience finder functions below for various non-generic types.
        // This can probably be further genericized, moving forward.

        /// <summary>
        /// Find all iterations (genders) of a given item name, if it exists.
        /// </summary>
        /// <param name="name">A string name or SHA id of an item</param>
        /// <returns></returns>
        public List<Xml.Item> FindItem(string name)
        {
            // Check for an exact result first
            var ret = new List<Xml.Item>();
            Xml.Item target;
            if (TryGetValue(name, out target) || TryGetValueByIndex(name, out target))
                ret.Add(target);
            else
            {
                foreach (var gender in Enum.GetValues(typeof(Xml.Gender)))
                {
                    var rawhash = $"{name.Normalize()}:{gender.ToString().Normalize()}";
                    var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(rawhash));
                    if (TryGetValue(string.Concat(hash.Select(b => b.ToString("x2"))).Substring(0, 8), out Xml.Item result))
                        ret.Add(result);
                }
            }
            return ret;
        }

        /// <summary>
        /// Return a username for a given uuid
        /// </summary>
        /// <param name="uuid">The uuid to look up</param>
        /// <returns>The username or string.empty if not found</returns>
        public string GetNameByUuid(string uuid)
        {
            if (TryGetValue(uuid, out UuidReference reference))
                return reference.UserName;            
            return string.Empty;
        }

        public string GetUuidByName(string name)
        {
            if (TryGetValueByIndex(name, out UuidReference reference))
                return reference.UserUuid;
            // Does user exist?
            if (TryGetUser(name, out User user))
                return user.Uuid;
            return string.Empty;
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
            var uuid = GetUuidByName(name);
            
            if (!string.IsNullOrEmpty(uuid))
            {
                if (TryGetValue(uuid, out info))
                    return true;
                if (TryGetValueByIndex(name, out info))
                    return true;
                // Fall back to loading from Redis
                info = GetOrCreateByUuid<AuthInfo>(uuid, name);
                // If we loaded from Redis, the user (should not) be logged in, so reset state
                if (info.IsLoggedIn)
                {
                    info.CurrentState = UserState.Disconnected;
                    info.Save();
                }
                if (info != null)
                    return true;
            }          
            return false;
        }

        public T GetOrCreate<T>(UuidReference reference) => GetOrCreateByUuid<T>(reference.UserUuid, reference.UserName);

        public T GetOrCreateByUuid<T>(string uuid, string index = "")
        {
            var type = typeof(T);
            if (!RedisTypes.Contains(type))
                throw new ArgumentException($"Type {type} is not a uuid referenced Redis type");

            // Check for existence of object locally first
            if (TryGetValue(uuid, out T obj))
                return obj;

            // Check for existence of object in Redis
            var storageKey = $"{type.FullName}:{uuid}";
            if (Redis.KeyExists(storageKey))
                obj = Redis.Get<T>(storageKey);

            // Fall back to creating it if needed
            if (obj == null)
            {
                obj = (T)Activator.CreateInstance(typeof(T), new object[1] { uuid });
                Redis.Set(storageKey, obj);
            }

            // Now that we have the object, update the store
            if (string.IsNullOrEmpty(index))
                Set(uuid, obj);
            else
                SetWithIndex(uuid, obj, index);

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
                GameLog.Info("Board: loaded {boardname}", name);
                newBoard.Id = newBoardId;
            }
            else
            {
                newBoard = new Board(name) { Id = newBoardId };
                newBoard.Save();
                GameLog.Info("Board: Creating {boardname}", name);
            }
            SetWithIndex(name, newBoard, newBoard.Id);
            return Get<Board>(name);
        }

        public UuidReference GetUuidReference(string name)
        {
            if (TryGetValueByIndex(name, out UuidReference reference))
                return reference;
            if (TryGetUser(name, out User userobj))
                return GetUuidReference(userobj);
            return null;
        }

        public UuidReference GetUuidReference(User userObj)
        {

            if (TryGetValue(userObj.Uuid, out UuidReference reference))
                return reference;

            var uuidRef = new UuidReference(userObj.Name)
            {
                AccountUuid = userObj.AccountUuid,
                UserUuid = userObj.Uuid               
            };

            SetWithIndex(userObj.Uuid, uuidRef, userObj.Name);
            return Get<UuidReference>(userObj.Uuid);
        }

        public bool TryGetUser(string name, out User userobj)
        {
            userobj = null;
            try
            {
                userobj = Redis.Get<User>(User.GetStorageKey(name));
                if (userobj != null)
                {
                    // Ensure our UUID reference is created when we deserialize a user (if it doesn't already exist)
                    GetUuidReference(userobj);
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

        /// <summary>
        /// Register a castable in the world data store, which will create a usable index by the categories 
        /// identified in the castable.
        /// </summary>
        /// <param name="castable"></param>
        public void RegisterCastable(Xml.Castable castable)
        {
            foreach (var category in castable.Categories)
            {
                var sanitized = Sanitize(category);
                if (!CastableIndex.ContainsKey(sanitized))
                    CastableIndex[sanitized] = new HashSet<Xml.Castable>(new Xml.CastableComparer());
                CastableIndex[sanitized].Add(castable);
            }

        }

        public void RegisterItem(Item item)
        {
            foreach (var category in item.Categories)
            {
                var sanitized = Sanitize(category);
                if (!ItemByCategory.ContainsKey(sanitized))
                    ItemByCategory[sanitized] = new HashSet<Item>();
            }
        }

        public bool ImportAll<T>(XmlLoadResponse<T> response) where T : IHybrasylLoadable<T>
        {
            return false;
        }


        /// <summary>
        /// Convenience method to retrieve a HashSet of castables, filtered to only skills.
        /// </summary>
        /// <param name="Str">Attribute requirement for the castables (str)</param>
        /// <param name="Int">Attribute requirement for the castables (int)</param>
        /// <param name="Wis">Attribute requirement for the castables (wis)</param>
        /// <param name="Con">Attribute requirement for the castables (con)</param>
        /// <param name="Dex">Attribute requirement for the castables (dex)</param>
        /// <param name="category">A category to narrow the search</param>
        /// <returns>HashSet of castables satisfying the given requirements</returns>
        /// <returns></returns>
        public HashSet<Xml.Castable> GetSkills(long Str = 0, long Int = 0, long Wis = 0,
            long Con = 0, long Dex = 0, string category = null) =>
            GetCastables(Str, Int, Wis, Con, Dex, category, CastableFilter.SkillsOnly);

        /// <summary>
        /// Convenience method to retrieve a HashSet of castables, filtered to only spells.
        /// </summary>
        /// <param name="Str">Attribute requirement for the castables (str)</param>
        /// <param name="Int">Attribute requirement for the castables (int)</param>
        /// <param name="Wis">Attribute requirement for the castables (wis)</param>
        /// <param name="Con">Attribute requirement for the castables (con)</param>
        /// <param name="Dex">Attribute requirement for the castables (dex)</param>
        /// <param name="category">A category to narrow the search</param>
        /// <returns>HashSet of castables satisfying the given requirements</returns>
        /// <returns></returns>
        public HashSet<Xml.Castable> GetSpells(long Str = 0, long Int = 0, long Wis = 0,
            long Con = 0, long Dex = 0, string category = null) =>
            GetCastables(Str, Int, Wis, Con, Dex, category, CastableFilter.SpellsOnly);

        /// <summary>
        /// Return a HashSet of castables, possibly narrowed by filters.
        /// </summary>
        /// <param name="category">A category to narrow the search</param>
        /// <param name="Str">Attribute requirement for the castable (str)</param>
        /// <param name="Int">Attribute requirement for the castable (int)</param>
        /// <param name="Wis">Attribute requirement for the castable (wis)</param>
        /// <param name="Con">Attribute requirement for the castable (con)</param>
        /// <param name="Dex">Attribute requirement for the castable (dex)</param>
        /// <returns>HashSet of castables satisfying the given requirements</returns>
        public HashSet<Xml.Castable> GetCastables(long Str = 0, long Int = 0, long Wis = 0, 
            long Con = 0, long Dex = 0, string category = null, 
            CastableFilter filter = CastableFilter.SkillsAndSpells)
        {
            HashSet<Xml.Castable> ret;
            if (!string.IsNullOrEmpty(category))
            {
                var sanitized = Sanitize(category);
                if (CastableIndex.ContainsKey(sanitized))
                    ret = CastableIndex[sanitized];
                else
                    ret = new HashSet<Castable>();
            }
            else
                ret = Values<Xml.Castable>().ToHashSet(new CastableComparer());

            if (Str > 0 || Int > 0 || Wis > 0 || Con > 0 || Dex > 0)
            {
                // TODO: perhaps cache this information
                foreach (var castable in ret)
                {
                    if (castable.Requirements.Count == 0)
                    {
                        continue;
                    }
                    var physreq = castable.Requirements.Where(x => x.Physical != null);
                    if (physreq.Count() == 0)
                    {
                        continue;
                    }
                    foreach (var req in physreq)
                    {
                        if (Str >= req.Physical.Str && Int >= req.Physical.Int && Wis >= req.Physical.Wis && Con >= req.Physical.Con &&
                            Dex >= req.Physical.Dex)
                            continue;
                        else
                            ret.Remove(castable);
                    }
                }
            }
            if (filter == CastableFilter.SkillsOnly)
                ret = ret.Where(c => c.IsSkill).ToHashSet();
            if (filter == CastableFilter.SpellsOnly)
                ret = ret.Where(c => c.IsSpell).ToHashSet();
            return ret;
        }        
    }
}

