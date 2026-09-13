// Small compatibility helper for idempotently attaching owner state to Unity
// objects without extending their lifetime through a strong reference.
using System.Runtime.CompilerServices;

namespace SwmarlyValheimQOL {
    public static class ConditionalWeakTableExtension {
        public static void TryAdd<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, ConditionalWeakTable<TKey, TValue>.CreateValueCallback createCallback) where TKey : class where TValue : class {
            table.GetValue(key, createCallback);
        }
    }
}
