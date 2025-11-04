#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Loco.Core.Validation;

/// <summary>
/// Null safety patterns using C# nullable reference types
/// Enables compile-time null safety checking
/// </summary>
public static class NullSafetyPatterns
{
    /// <summary>
    /// Safely accesses nullable value with fallback
    /// Returns default value if null
    /// </summary>
    public static T SafeGet<T>(T? value, T defaultValue) where T : notnull
    {
        return value ?? defaultValue;
    }

    /// <summary>
    /// Ensures value is not null, throws if it is
    /// </summary>
    public static T ThrowIfNull<T>(T? value, string paramName) where T : notnull
    {
        if (value == null)
            throw new ArgumentNullException(paramName, "Value cannot be null");

        return value;
    }

    /// <summary>
    /// Tries to get value from nullable, returns success/failure
    /// </summary>
    public static bool TryGetValue<T>(T? value, [NotNullWhen(true)] out T? result) where T : class
    {
        if (value != null)
        {
            result = value;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Safe string operations with null-aware behavior
    /// </summary>
    public static class SafeString
    {
        /// <summary>
        /// Null-coalescing alternative to empty string
        /// </summary>
        public static string NotNullOrEmpty(string? value)
        {
            return !string.IsNullOrEmpty(value) ? value : string.Empty;
        }

        /// <summary>
        /// Safe substring with bounds checking
        /// </summary>
        public static string? SafeSubstring(string? value, int startIndex, int length)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            if (startIndex >= value.Length)
                return null;

            var actualLength = Math.Min(length, value.Length - startIndex);
            return value.Substring(startIndex, actualLength);
        }

        /// <summary>
        /// Null-safe string concatenation
        /// </summary>
        public static string Concatenate(params string?[] values)
        {
            return string.Concat(values.Where(v => v != null));
        }
    }

    /// <summary>
    /// Safe collection operations
    /// </summary>
    public static class SafeCollection
    {
        /// <summary>
        /// Safe enumeration of nullable collection
        /// </summary>
        public static IEnumerable<T> SafeEnumerate<T>(IEnumerable<T>? collection) where T : notnull
        {
            return collection ?? Enumerable.Empty<T>();
        }

        /// <summary>
        /// Safe first element access
        /// </summary>
        public static T? SafeFirst<T>(IEnumerable<T>? collection) where T : class
        {
            return collection?.FirstOrDefault();
        }

        /// <summary>
        /// Safe element at index access
        /// </summary>
        public static T? SafeAt<T>(IList<T>? collection, int index) where T : class
        {
            if (collection == null || index < 0 || index >= collection.Count)
                return null;

            return collection[index];
        }

        /// <summary>
        /// Null-safe count
        /// </summary>
        public static int SafeCount<T>(IEnumerable<T>? collection)
        {
            return collection?.Count() ?? 0;
        }

        /// <summary>
        /// Null-safe any check
        /// </summary>
        public static bool SafeAny<T>(IEnumerable<T>? collection, Func<T, bool>? predicate = null)
        {
            if (collection == null)
                return false;

            return predicate == null ? collection.Any() : collection.Any(predicate);
        }
    }

    /// <summary>
    /// Safe dictionary operations
    /// </summary>
    public static class SafeDictionary
    {
        /// <summary>
        /// Null-safe dictionary lookup
        /// </summary>
        public static TValue? SafeGet<TKey, TValue>(
            IDictionary<TKey, TValue>? dictionary,
            TKey? key) where TKey : notnull where TValue : class
        {
            if (dictionary == null || key == null)
                return null;

            return dictionary.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Null-safe dictionary add (won't throw if key exists)
        /// </summary>
        public static void SafeAdd<TKey, TValue>(
            IDictionary<TKey, TValue>? dictionary,
            TKey? key,
            TValue? value) where TKey : notnull
        {
            if (dictionary == null || key == null)
                return;

            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = value!;
            }
        }

        /// <summary>
        /// Null-safe dictionary merge
        /// </summary>
        public static Dictionary<TKey, TValue> SafeMerge<TKey, TValue>(
            IDictionary<TKey, TValue>? dict1,
            IDictionary<TKey, TValue>? dict2) where TKey : notnull
        {
            var result = new Dictionary<TKey, TValue>();

            if (dict1 != null)
            {
                foreach (var kvp in dict1)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            if (dict2 != null)
            {
                foreach (var kvp in dict2)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }
    }
}

/// <summary>
/// Nullable-aware option type pattern
/// Similar to Maybe/Option in functional languages
/// </summary>
public abstract record Option<T> where T : notnull
{
    public sealed record Some(T Value) : Option<T>;
    public sealed record None() : Option<T>;

    /// <summary>
    /// Maps option value if Some, returns None if None
    /// </summary>
    public Option<U> Map<U>(Func<T, U> map) where U : notnull
    {
        return this switch
        {
            Some(var value) => new Option<U>.Some(map(value)),
            _ => new Option<U>.None()
        };
    }

    /// <summary>
    /// Flatmaps option value
    /// </summary>
    public Option<U> FlatMap<U>(Func<T, Option<U>> flatMap) where U : notnull
    {
        return this switch
        {
            Some(var value) => flatMap(value),
            _ => new Option<U>.None()
        };
    }

    /// <summary>
    /// Gets value or default
    /// </summary>
    public T GetValueOrDefault(T defaultValue)
    {
        return this switch
        {
            Some(var value) => value,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Executes action if Some
    /// </summary>
    public void IfSome(Action<T> action)
    {
        if (this is Some { Value: var value })
        {
            action(value);
        }
    }

    /// <summary>
    /// Executes action if None
    /// </summary>
    public void IfNone(Action action)
    {
        if (this is None)
        {
            action();
        }
    }
}

/// <summary>
/// Extension methods for null safety
/// </summary>
public static class NullSafetyExtensions
{
    /// <summary>
    /// Converts nullable to Option type
    /// </summary>
    public static Option<T> ToOption<T>(this T? value) where T : notnull
    {
        return value == null ? new Option<T>.None() : new Option<T>.Some(value);
    }

    /// <summary>
    /// Safe null-coalescing with null-safe check
    /// </summary>
    public static T NotNull<T>(this T? value, [NotNull] string message) where T : notnull
    {
        if (value == null)
            throw new InvalidOperationException(message);

        return value;
    }

    /// <summary>
    /// Safe null-coalescing for operations
    /// </summary>
    public static TResult? SafeMap<TInput, TResult>(
        this TInput? input,
        Func<TInput, TResult?> selector) where TInput : notnull where TResult : notnull
    {
        return input != null ? selector(input) : null;
    }

    /// <summary>
    /// Filters out null values in enumerables
    /// </summary>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : notnull
    {
        return source.Where(x => x != null)!;
    }

    /// <summary>
    /// Safe dictionary try-get pattern
    /// </summary>
    public static TValue? TryGetSafe<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key) where TKey : notnull where TValue : class
    {
        return dictionary.TryGetValue(key, out var value) ? value : null;
    }
}

/// <summary>
/// Example of proper null-safe API design
/// </summary>
public class NullSafeApiExample
{
    /// <summary>
    /// Demonstrates proper nullable parameter handling
    /// </summary>
    public string ProcessData(
        string requiredInput,           // Non-nullable - must not be null
        string? optionalInput,          // Nullable - can be null
        List<string>? items,            // Nullable collection
        Action<string>? callback)       // Nullable delegate
    {
        // Use null-coalescing for optional inputs
        var input = optionalInput ?? "default";

        // Safe enumeration of nullable collection
        var itemCount = items?.Count ?? 0;

        // Safe callback invocation
        callback?.Invoke("Processing");

        return $"Processed: {requiredInput}, {input}, items: {itemCount}";
    }

    /// <summary>
    /// Demonstrates return type null safety
    /// </summary>
    public string? GetValueSafely(string key)
    {
        // Return can be null (indicated by ?)
        var dictionary = new Dictionary<string, string?>();
        return dictionary.TryGetSafe(key);
    }

    /// <summary>
    /// Demonstrates NotNull attribute for validation
    /// </summary>
    public void ProcessRequired(
        [NotNull] string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(nameof(value));
        }

        // Compiler knows value is not null here
        var length = value.Length;
    }
}
