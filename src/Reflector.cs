using System;
using System.Collections.Generic;
using System.Reflection;

namespace ControllerEverywhere
{
    // Tiny helper for poking non-public fields on KSP classes. Caches FieldInfo to
    // avoid reflection overhead on every frame.
    internal static class Reflector
    {
        private static readonly Dictionary<string, FieldInfo> _cache = new Dictionary<string, FieldInfo>();

        private static FieldInfo F(Type t, string name)
        {
            var key = t.FullName + "." + name;
            if (_cache.TryGetValue(key, out var fi)) return fi;
            fi = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                                  | BindingFlags.Instance | BindingFlags.Static);
            _cache[key] = fi;
            return fi;
        }

        public static T Get<T>(object obj, string name)
        {
            var fi = F(obj.GetType(), name);
            if (fi == null) return default(T);
            return (T)fi.GetValue(obj);
        }

        public static void Set<T>(object obj, string name, T value)
        {
            var fi = F(obj.GetType(), name);
            if (fi == null) return;
            fi.SetValue(obj, value);
        }

        public static T Get<T>(Type t, string name)  // static fields
        {
            var fi = F(t, name);
            if (fi == null) return default(T);
            return (T)fi.GetValue(null);
        }
    }
}
