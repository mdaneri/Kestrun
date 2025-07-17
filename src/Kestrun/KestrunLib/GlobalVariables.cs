using System.Collections.Concurrent;

namespace KestrumLib
{
    /// <summary>
    /// Thread-safe store for arbitrary global variables used within Kestrun.
    /// </summary>
    public static class GlobalVariables
    {
        private record GlobalEntry(object Value, Type Type, bool ReadOnly);

        private static readonly ConcurrentDictionary<string, GlobalEntry> _store = new();

        /// <summary>
        /// Defines a new global variable or updates an existing one.
        /// </summary>
        /// <typeparam name="T">Type of the value being stored.</typeparam>
        /// <param name="name">Variable name.</param>
        /// <param name="value">Value to store.</param>
        /// <param name="readOnly">Whether the variable is read-only.</param>
        /// <returns>True if a new variable was created, false if an existing one was updated.</returns>
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

        /// <summary>
        /// Attempts to retrieve a global variable and cast it to the specified type.
        /// </summary>
        /// <typeparam name="T">Expected type of the variable.</typeparam>
        /// <param name="name">Variable name.</param>
        /// <param name="value">When this method returns, contains the variable value if found and of the correct type.</param>
        /// <returns>True if the variable exists and is of the correct type.</returns>
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

        /// <summary>
        /// Gets the raw value of a global variable if it exists.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <returns>The stored value or <c>null</c> if not defined.</returns>
        public static object? Get(string name)
            => _store.TryGetValue(name, out var entry) ? entry.Value : null;

        /// <summary>
        /// Gets the declared type of a global variable.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <returns>The stored type or <c>null</c> if the variable does not exist.</returns>
        public static Type? GetTypeOf(string name)
            => _store.TryGetValue(name, out var entry) ? entry.Type : null;

        /// <summary>
        /// Determines whether a global variable is read-only.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <returns><c>true</c> if the variable exists and is read-only.</returns>
        public static bool IsReadOnly(string name)
            => _store.TryGetValue(name, out var entry) && entry.ReadOnly;

        /// <summary>
        /// Removes a global variable if it exists and is not read-only.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <returns>True if the variable was removed.</returns>
        public static bool Remove(string name)
        {
            if (_store.TryGetValue(name, out var entry) && entry.ReadOnly)
                return false;
            return _store.TryRemove(name, out _);
        }
        /// <summary>
        /// Returns a read-only dictionary of all variable names and their values.
        /// </summary>
        public static IReadOnlyDictionary<string, object?> GetAllValues()
        {
            return _store.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value.Value);
        }

        /// <summary>
        /// Creates a snapshot of all variables including their types and read-only flag.
        /// </summary>
        /// <returns>A dictionary mapping names to value/type/readOnly tuples.</returns>
        public static IReadOnlyDictionary<string, (object Value, Type Type, bool ReadOnly)> Snapshot()
            => _store.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value.Value, kvp.Value.Type, kvp.Value.ReadOnly));

        /// <summary>
        /// Updates the value of an existing variable without changing its type or read-only state.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <param name="newValue">New value to store.</param>
        /// <returns>True if the value was updated.</returns>
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
