using System.Collections.Concurrent;

namespace Kestrun
{
    public static class GlobalVariables
    {
        private record GlobalEntry(object Value, Type Type, bool ReadOnly);

        private static readonly ConcurrentDictionary<string, GlobalEntry> _store = new();

        public static bool Define<T>(string name, T value, bool readOnly = false)
        {

            var t = typeof(T);
            if (t.IsValueType)
                throw new ArgumentException(
                    $"Cannot define global variable '{name}' of value type '{t.FullName}'. Only reference types are allowed.",
                    nameof(value)
                );
            var entry = new GlobalEntry(value!, typeof(T), readOnly);

            return _store.AddOrUpdate(name,
                entry,
                (_, existing) =>
                {
                    if (existing.ReadOnly)
                        return existing; // ignore if read-only
                    return entry;
                }) == entry;
        }

        public static bool TryGet<T>(string name, out T? value)
        {
            if (_store.TryGetValue(name, out var entry) && entry.Value is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public static object? Get(string name)
            => _store.TryGetValue(name, out var entry) ? entry.Value : null;

        public static Type? GetTypeOf(string name)
            => _store.TryGetValue(name, out var entry) ? entry.Type : null;

        public static bool IsReadOnly(string name)
            => _store.TryGetValue(name, out var entry) && entry.ReadOnly;

        public static bool Remove(string name)
        {
            if (_store.TryGetValue(name, out var entry) && entry.ReadOnly)
                return false;
            return _store.TryRemove(name, out _);
        }
        public static IReadOnlyDictionary<string, object?> GetAllValues()
        {
            return _store.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value.Value);
        }

        public static IReadOnlyDictionary<string, (object Value, Type Type, bool ReadOnly)> Snapshot()
            => _store.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value.Value, kvp.Value.Type, kvp.Value.ReadOnly));

        /// <summary>
        /// If the variable exists and is not read-only, overwrite its Value (preserving its original Type & ReadOnly flag).
        /// </summary>
        public static bool UpdateValue(string name, object? newValue)
        {
            return _store.AddOrUpdate(
                name,
                // If it didn't exist before, create a new entry (not read‐only)
                key => new GlobalEntry(newValue!, newValue?.GetType() ?? typeof(object), ReadOnly: false),
                // If it did exist, and is read-only → keep it. Otherwise overwrite Value, keep Type & ReadOnly.
                (key, existing) => existing.ReadOnly
                    ? existing
                    : new GlobalEntry(newValue!, existing.Type, existing.ReadOnly)
            ) is GlobalEntry updated && !updated.ReadOnly;
        }
    }
}
