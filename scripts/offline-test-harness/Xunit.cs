// A working subset of xunit, for running this repository's tests without NuGet.
//
// api.nuget.org is refused by organization policy here, so `dotnet test` cannot
// run: xunit, FluentAssertions and the test SDK cannot be restored. That left
// 271 backend tests that had never been executed even once.
//
// The requirement, though, was never "restore NuGet" - it was "run the tests and
// see whether the assertions hold". This is the smaller thing that does that:
// the attributes the tests are marked with, the assertions they call, and (in
// Runner.cs) a reflection loop that invokes them.
//
// These are NOT stubs. Every assertion here really compares and really throws,
// because an assertion that quietly passes is worse than no test at all. Where
// the semantics are narrower than the real library's, the method says so.
//
// It does not replace `dotnet test`. It cannot host an ASP.NET app, so the four
// controller test classes that need WebApplicationFactory are excluded and
// reported as skipped rather than counted as passing. The real suite still runs
// in CI - see docs/ci/ci.yml.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Xunit
{
    /// <summary>Raised by every failed assertion. Runner.cs reports these as failures.</summary>
    public class AssertionFailedException : Exception
    {
        public AssertionFailedException(string message) : base(message) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FactAttribute : Attribute
    {
        public string? DisplayName { get; set; }
        public string? Skip { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TheoryAttribute : Attribute
    {
        public string? DisplayName { get; set; }
        public string? Skip { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class InlineDataAttribute : Attribute
    {
        // `[InlineData(null)]` passes a null ARRAY rather than an array
        // containing null, so normalize here instead of at every reader.
        public InlineDataAttribute(params object?[]? data) => Data = data ?? new object?[] { null };
        public object?[] Data { get; }
    }

    /// <summary>
    /// Names a static property or method supplying a [Theory]'s cases, each an
    /// object[] of arguments.
    ///
    /// Added for the condition truth table, whose cases are read from a JSON
    /// file shared with the editor's test suite and so cannot be written as
    /// compile-time [InlineData]. Real xunit resolves the member on the test
    /// class unless MemberType says otherwise; so does the runner.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MemberDataAttribute : Attribute
    {
        public MemberDataAttribute(string memberName, params object?[] parameters)
        {
            MemberName = memberName;
            Parameters = parameters ?? Array.Empty<object?>();
        }

        public string MemberName { get; }
        public object?[] Parameters { get; }
        public Type? MemberType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class CollectionAttribute : Attribute
    {
        public CollectionAttribute(string name) { }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TraitAttribute : Attribute
    {
        public TraitAttribute(string name, string value) { }
    }

    /// <summary>
    /// Marker. The runner constructs a fixture per class and passes it in, the
    /// same way xunit does - but only for fixtures it can build, which excludes
    /// anything needing an ASP.NET host.
    /// </summary>
    public interface IClassFixture<TFixture> where TFixture : class { }

    public interface IAsyncLifetime
    {
        Task InitializeAsync();
        Task DisposeAsync();
    }

    public static class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!Compare.AreEqual(expected, actual))
                throw new AssertionFailedException(
                    $"Assert.Equal() failure\nExpected: {Compare.Describe(expected)}\nActual:   {Compare.Describe(actual)}");
        }

        public static void Equal(string? expected, string? actual, bool ignoreCase)
        {
            var same = string.Equals(expected, actual,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            if (!same)
                throw new AssertionFailedException(
                    $"Assert.Equal() failure (ignoreCase: {ignoreCase})\nExpected: {expected}\nActual:   {actual}");
        }

        public static void Equal<T>(T expected, T actual, IEqualityComparer<T> comparer)
        {
            if (!comparer.Equals(expected, actual))
                throw new AssertionFailedException(
                    $"Assert.Equal() failure\nExpected: {Compare.Describe(expected)}\nActual:   {Compare.Describe(actual)}");
        }

        public static void NotEqual<T>(T expected, T actual)
        {
            if (Compare.AreEqual(expected, actual))
                throw new AssertionFailedException(
                    $"Assert.NotEqual() failure - both were {Compare.Describe(actual)}");
        }

        public static void True(bool condition, string? userMessage = null)
        {
            if (!condition)
                throw new AssertionFailedException(userMessage ?? "Assert.True() failure - value was false");
        }

        public static void False(bool condition, string? userMessage = null)
        {
            if (condition)
                throw new AssertionFailedException(userMessage ?? "Assert.False() failure - value was true");
        }

        public static void Null(object? value)
        {
            if (value is not null)
                throw new AssertionFailedException($"Assert.Null() failure - was {Compare.Describe(value)}");
        }

        public static void NotNull(object? value)
        {
            if (value is null)
                throw new AssertionFailedException("Assert.NotNull() failure - was null");
        }

        public static void Empty(IEnumerable collection)
        {
            if (collection.Cast<object?>().Any())
                throw new AssertionFailedException("Assert.Empty() failure - collection had items");
        }

        public static void NotEmpty(IEnumerable collection)
        {
            if (!collection.Cast<object?>().Any())
                throw new AssertionFailedException("Assert.NotEmpty() failure - collection was empty");
        }

        public static void Single(IEnumerable collection)
        {
            var count = collection.Cast<object?>().Count();
            if (count != 1)
                throw new AssertionFailedException($"Assert.Single() failure - collection had {count} items");
        }

        public static void Contains(string expected, string actual)
        {
            if (actual is null || !actual.Contains(expected, StringComparison.Ordinal))
                throw new AssertionFailedException(
                    $"Assert.Contains() failure\nNot found: {expected}\nIn value:  {actual}");
        }

        public static void Contains<T>(T expected, IEnumerable<T> collection)
        {
            if (!collection.Any(item => Compare.AreEqual(expected, item)))
                throw new AssertionFailedException(
                    $"Assert.Contains() failure - {Compare.Describe(expected)} not in the collection");
        }

        public static void Contains<T>(IEnumerable<T> collection, Predicate<T> filter)
        {
            if (!collection.Any(item => filter(item)))
                throw new AssertionFailedException(
                    "Assert.Contains() failure - no item matched the predicate");
        }

        public static void DoesNotContain(string expected, string actual)
        {
            if (actual is not null && actual.Contains(expected, StringComparison.Ordinal))
                throw new AssertionFailedException(
                    $"Assert.DoesNotContain() failure - found {expected} in {actual}");
        }

        public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
        {
            if (collection.Any(item => Compare.AreEqual(expected, item)))
                throw new AssertionFailedException(
                    $"Assert.DoesNotContain() failure - {Compare.Describe(expected)} was in the collection");
        }

        public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> filter)
        {
            if (collection.Any(item => filter(item)))
                throw new AssertionFailedException(
                    "Assert.DoesNotContain() failure - an item matched the predicate");
        }

        public static T Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                throw new AssertionFailedException(
                    $"Assert.Throws<{typeof(T).Name}>() failure - threw {other.GetType().Name}: {other.Message}");
            }

            throw new AssertionFailedException($"Assert.Throws<{typeof(T).Name}>() failure - nothing was thrown");
        }

        public static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action();
            }
            catch (T expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                throw new AssertionFailedException(
                    $"Assert.ThrowsAsync<{typeof(T).Name}>() failure - threw {other.GetType().Name}: {other.Message}");
            }

            throw new AssertionFailedException($"Assert.ThrowsAsync<{typeof(T).Name}>() failure - nothing was thrown");
        }
    }

    /// <summary>
    /// Value comparison shared by the assertions.
    ///
    /// Numbers are compared by value across types, because a test asserting
    /// `Should().Be(5432)` against an int? must pass, and boxed Equals says no
    /// when the types differ. Everything else falls back to Equals, and
    /// sequences compare element by element.
    /// </summary>
    public static class Compare
    {
        public static bool AreEqual(object? expected, object? actual)
        {
            if (expected is null && actual is null) return true;
            if (expected is null || actual is null) return false;
            if (Equals(expected, actual)) return true;

            if (IsNumeric(expected) && IsNumeric(actual))
                return Convert.ToDecimal(expected) == Convert.ToDecimal(actual);

            // Enum vs its underlying value, which InlineData often supplies.
            if (expected.GetType().IsEnum && IsNumeric(actual))
                return Convert.ToDecimal(expected) == Convert.ToDecimal(actual);
            if (actual.GetType().IsEnum && IsNumeric(expected))
                return Convert.ToDecimal(actual) == Convert.ToDecimal(expected);

            if (expected is string || actual is string) return false;

            // Dictionary entries. KeyValuePair<string, object> and
            // KeyValuePair<string, object?> are different struct types, so
            // ValueType.Equals says no even when key and value match - which
            // made every dictionary comparison fail for the wrong reason.
            if (IsKeyValuePair(expected, out var expectedKey, out var expectedValue) &&
                IsKeyValuePair(actual, out var actualKey, out var actualValue))
            {
                return AreEqual(expectedKey, actualKey) && AreEqual(expectedValue, actualValue);
            }

            if (expected is IEnumerable left && actual is IEnumerable right)
                return SequenceEqual(left, right);

            return false;
        }

        public static bool SequenceEqual(IEnumerable left, IEnumerable right)
        {
            var a = left.Cast<object?>().ToList();
            var b = right.Cast<object?>().ToList();
            return a.Count == b.Count && a.Zip(b).All(pair => AreEqual(pair.First, pair.Second));
        }

        private static bool IsKeyValuePair(object value, out object? key, out object? item)
        {
            key = null;
            item = null;
            var type = value.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
                return false;

            key = type.GetProperty("Key")!.GetValue(value);
            item = type.GetProperty("Value")!.GetValue(value);
            return true;
        }

        private static bool IsNumeric(object value) => value is
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

        public static string Describe(object? value) => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            IEnumerable e and not string =>
                "[" + string.Join(", ", e.Cast<object?>().Take(12).Select(Describe)) + "]",
            _ => value.ToString() ?? value.GetType().Name,
        };
    }
}
