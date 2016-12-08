using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Markup;
using Microsoft.Scripting.Ast;

namespace Hybrasyl
{
    public class WorldDataStore
    {
        private ConcurrentDictionary<Type, ConcurrentDictionary<string, dynamic>> _dataStore;
        private ConcurrentDictionary<Type, ConcurrentDictionary<dynamic, dynamic>> _index;

        public WorldDataStore()
        {
            _dataStore = new ConcurrentDictionary<Type, ConcurrentDictionary<string, dynamic>>();
            _index = new ConcurrentDictionary<Type, ConcurrentDictionary<dynamic, dynamic>>();
        }

        /// <summary>
        /// Get a substore for a given type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
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
                return (T) _dataStore[typeof(T)][key];
            }
            return default(T);
        }

        public T GetByIndex<T>(dynamic key)
        {
            if (_index.ContainsKey(typeof(T)))
            {
                return (T) _index[typeof(T)][key];
            }
            return default(T);
        }

        public bool TryGetValue<T>(dynamic key, out T tresult)
        {
            tresult = default(T);
            var sub = GetSubStore<T>();
            if (!sub.ContainsKey(key.ToString())) return false;
            tresult = (T) sub[key.ToString()];
            return true;
        }

        public bool TryGetValueByIndex<T>(dynamic key, out T tresult)
        {
            tresult = default(T);
            var sub = GetSubIndex<T>();
            if (!sub.ContainsKey(key)) return false;
            tresult = (T)sub[key];
            return true;
        }

        /// <summary>
        /// Given a key and a value, set the 
        /// </summary>
        /// <typeparam name="T">The type we want to store.</typeparam>
        /// <param name="key">The key to be used for the object.</param>
        /// <param name="value">The actual object to be stored.</param>
        /// <returns></returns>
        public bool Set<T>(dynamic key, T value) => GetSubStore<T>().TryAdd(key.ToString(), value);

        public bool SetWithIndex<T>(dynamic key, T value, dynamic index) => GetSubStore<T>().TryAdd(key.ToString(), value) &&
            GetSubIndex<T>().TryAdd(index, value);

    

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
        public bool ContainsKey<T>(dynamic key) => GetSubStore<T>().ContainsKey(key.ToString());
        public int Count<T>() => GetSubStore<T>().Count;

        public IDictionary<string, T> GetDictionary<T>() => (IDictionary<string,T>) _dataStore[typeof(T)];

        public bool Remove<T>(dynamic key)
        {
            dynamic ignored;
            return GetSubStore<T>().TryRemove(key, out ignored);
        }

    }
}
