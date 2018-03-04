using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl.Threading
{
    public class Lockable<T>
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
                    _value = value;
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

        public static Lockable<T> operator -(Lockable<T> a, Lockable<T> b) => new Lockable<T>(Difference(a.Value, b.Value));
        
        public static Lockable<T> operator +(Lockable<T> a, Lockable<T> b) => new Lockable<T>(Sum(a.Value, b.Value));

        public static Lockable<T> operator *(Lockable<T> a, Lockable<T> b) => new Lockable<T>(Product(a.Value, b.Value));

        private static T Sum(T a, T b) => (dynamic)a + (dynamic)b;

        private static T Difference(T a, T b) => (dynamic)a - (dynamic)b;

        private static T Product(T a, T b) => (dynamic)a * (dynamic)b;
    }
}
