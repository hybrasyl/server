using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Threading
{
    class Lockable<T>
    {
        private T _value;
        private object _lock = new object();
        public T Value
        {
            get
            {
                lock (_lock)
                {
                    return _value;
                }
            }
            set
            {
                lock (_lock)
                {
                    _value = Value;
                }
            }
        }
        public Lockable(T value)
        {
            Value = value;
        }

        public static implicit operator T(Lockable<T> value)
        {
            return value.Value;
        }
    }
}
