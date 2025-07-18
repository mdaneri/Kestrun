using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kestrun
{
    /// <summary>
    /// Thread‑safe, case‑insensitive global key/value store for reference‑type objects.
    /// </summary>
    public static partial class SharedState
    {
        // ── configuration ───────────────────────────────────────────────
        private static readonly Regex _validName =
            MyRegex();

        // StringComparer.OrdinalIgnoreCase ⇒ 100 % case‑insensitive keys
        private static readonly ConcurrentDictionary<string, object?> _store =
            new(StringComparer.OrdinalIgnoreCase);

        // ── public API ──────────────────────────────────────────────────
        /// <summary>Add or overwrite a value (reference‑types only).</summary>
        public static bool Set(string name, object? value)
        {
            ValidateName(name);
            ValidateValue(name, value);
            _store[name] = value;
            return true;
        }

        /// <summary>
        /// Strongly‑typed fetch. Returns <c>false</c> if the key is missing
        /// or the stored value can’t be cast to <typeparamref name="T"/>.
        /// </summary>
        public static bool TryGet<T>(string name, out T? value)
        {
            if (_store.TryGetValue(name, out var raw) && raw is T cast)
            {
                value = cast;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>Untyped fetch; <c>null</c> if absent.</summary>
        public static object? Get(string name) =>
            _store.TryGetValue(name, out var val) ? val : null;

        /// <summary>Remove an entry (case‑insensitive key match).</summary>
        public static bool Remove(string name) =>
            _store.TryRemove(name, out _);

        /// <summary>Snapshot of *all* current variables (shallow copy).</summary>
        public static IReadOnlyDictionary<string, object?> Snapshot() =>
            _store.ToDictionary(kvp => kvp.Key, kvp => kvp.Value,
                                StringComparer.OrdinalIgnoreCase);

        /// <summary>Snapshot of keys only—handy for quick listings.</summary>
        public static IReadOnlyCollection<string> KeySnapshot() =>
            [.. _store.Keys];

        // ── helpers ────────────────────────────────────────────────────
        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !_validName.IsMatch(name))
                throw new ArgumentException(
                    $"'{name}' is not a valid identifier for C# / PowerShell.",
                    nameof(name));
        }

        private static void ValidateValue(string name, object? value)
        {
            if (value is null) return;                       // null is allowed
            var t = value.GetType();
            if (t.IsValueType)
                throw new ArgumentException(
                    $"Cannot define global variable '{name}' of value type '{t.FullName}'. " +
                    "Only reference types are allowed.",
                    nameof(value));
        }

        [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
        private static partial Regex MyRegex();
    }
}
