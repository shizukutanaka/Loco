// Stand-ins for the test packages, so the test sources can be type-checked
// without a NuGet restore.
//
// api.nuget.org is refused by organization policy in the environment this
// repository is developed in, so `dotnet test` cannot run and - more to the
// point - the test assembly is never COMPILED. That is how three files naming
// types which do not exist sat in Loco.Core.Tests, taking the whole assembly
// down with them and every other test in it. Being unable to run tests is a
// constraint; being unable to tell whether they compile was a choice.
//
// What this buys and what it does not:
//
//   The src types are REAL here - the stubs replace only xunit, FluentAssertions
//   and WebApplicationFactory. So a test naming a property Loco.Core does not
//   have, calling a method with the wrong arguments, or constructing a type that
//   was renamed still fails to compile, which is the entire class of breakage
//   worth catching.
//
//   Assertion arguments are typed `object`, as FluentAssertions' own largely
//   are. A test comparing an int to a string will not be caught here; it would
//   be caught by running the test, which is what CI is for.
//
// These are compiled ONLY by scripts/typecheck-offline.sh. They are never part
// of a real build: the test projects reference the actual packages, and if this
// file ever diverged from them the CI run would say so.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FactAttribute : Attribute
    {
        public string? DisplayName { get; set; }
        public string? Skip { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TheoryAttribute : Attribute
    {
        public string? DisplayName { get; set; }
        public string? Skip { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class InlineDataAttribute : Attribute
    {
        public InlineDataAttribute(params object?[] data) { }
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

    /// <summary>Marker only; the runner supplies the fixture at run time.</summary>
    public interface IClassFixture<TFixture> where TFixture : class { }

    public interface IAsyncLifetime
    {
        Task InitializeAsync();
        Task DisposeAsync();
    }

    public static class Assert
    {
        public static void Equal<T>(T expected, T actual) { }
        public static void Equal(string? expected, string? actual, bool ignoreCase) { }
        public static void Equal<T>(T expected, T actual, IEqualityComparer<T> comparer) { }
        public static void NotEqual<T>(T expected, T actual) { }
        public static void True(bool condition, string? userMessage = null) { }
        public static void False(bool condition, string? userMessage = null) { }
        public static void Null(object? value) { }
        public static void NotNull(object? value) { }
        public static void Empty(System.Collections.IEnumerable collection) { }
        public static void NotEmpty(System.Collections.IEnumerable collection) { }
        public static void Single(System.Collections.IEnumerable collection) { }
        public static void Contains(string expected, string actual) { }
        public static void Contains<T>(T expected, IEnumerable<T> collection) { }
        public static void Contains<T>(IEnumerable<T> collection, Predicate<T> filter) { }
        public static void DoesNotContain(string expected, string actual) { }
        public static void DoesNotContain<T>(T expected, IEnumerable<T> collection) { }
        public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> filter) { }
        public static T Throws<T>(Action action) where T : Exception => null!;
        public static Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception => null!;
    }
}

namespace FluentAssertions
{
    /// <summary>
    /// One permissive assertion type standing in for FluentAssertions' many.
    /// Every method returns it so chains compile; the subject type is carried
    /// through so `.Which`, `.Subject` and the collection predicates keep the
    /// element type the test actually wrote.
    /// </summary>
    public class Assertions<T>
    {
        public Assertions<T> Be(object? expected, string because = "", params object[] args) => this;
        public Assertions<T> NotBe(object? unexpected, string because = "", params object[] args) => this;
        public Assertions<T> BeTrue(string because = "", params object[] args) => this;
        public Assertions<T> BeFalse(string because = "", params object[] args) => this;
        public Assertions<T> BeNull(string because = "", params object[] args) => this;
        public Assertions<T> NotBeNull(string because = "", params object[] args) => this;
        public Assertions<T> BeEmpty(string because = "", params object[] args) => this;
        public Assertions<T> NotBeEmpty(string because = "", params object[] args) => this;
        public Assertions<T> BeNullOrWhiteSpace(string because = "", params object[] args) => this;
        public Assertions<T> NotBeNullOrWhiteSpace(string because = "", params object[] args) => this;
        public Assertions<T> NotBeNullOrEmpty(string because = "", params object[] args) => this;
        public Assertions<T> Equal(object? expected, string because = "", params object[] args) => this;
        public Assertions<T> Equal(params object?[] expected) => this;
        public Assertions<T> BeEquivalentTo(object? expected, string because = "", params object[] args) => this;
        public Assertions<T> Contain(object? expected, string because = "", params object[] args) => this;
        // Collection predicates. The element type cannot be recovered from T
        // here without the real library's overload set, so the lambda parameter
        // is dynamic: `r => r.Success` compiles, but a typo in `.Success` is
        // NOT caught. Two dozen call sites; everything else stays checked.
        public Assertions<T> Contain(Func<dynamic, bool> predicate, string because = "", params object[] args) => this;
        public Assertions<T> OnlyContain(Func<dynamic, bool> predicate, string because = "", params object[] args) => this;
        public Assertions<T> AllSatisfy(Action<dynamic> inspector, string because = "", params object[] args) => this;
        public Assertions<T> NotContain(Func<dynamic, bool> predicate, string because = "", params object[] args) => this;
        public Assertions<T> NotContain(object? unexpected, string because = "", params object[] args) => this;
        public Assertions<T> ContainInOrder(params object?[] expected) => this;
        public Assertions<T> ContainKey(object? key, string because = "", params object[] args) => this;
        public Assertions<T> NotContainKey(object? key, string because = "", params object[] args) => this;
        /// <summary>Yields the single element, so `.Subject` is the item and not the collection.</summary>
        public SingleAssertions ContainSingle(string because = "", params object[] args) => new();
        public SingleAssertions ContainSingle(Func<dynamic, bool> predicate, string because = "", params object[] args) => new();
        public Assertions<T> HaveCount(int expected, string because = "", params object[] args) => this;
        public Assertions<T> BeOneOf(params object?[] validValues) => this;
        public Assertions<T> BeOneOf(object? a, object? b, string because) => this;
        public Assertions<T> BeGreaterThan(object? expected, string because = "", params object[] args) => this;
        public Assertions<T> BeGreaterThanOrEqualTo(object? expected, string because = "", params object[] args) => this;
        public Assertions<T> BeLessThan(object? expected, string because = "", params object[] args) => this;
        public Assertions<T> BeLessThanOrEqualTo(object? expected, string because = "", params object[] args) => this;
        public Assertions<T> BeInAscendingOrder(string because = "", params object[] args) => this;
        public Assertions<T> BeAssignableTo<TTarget>(string because = "", params object[] args) => this;
        public Assertions<T> Throw<TException>(string because = "", params object[] args) where TException : Exception => this;
        public Assertions<T> NotThrow(string because = "", params object[] args) => this;
        public Assertions<T> WithMessage(string expected, string because = "", params object[] args) => this;
        // Awaited at the call site, so these have to BE awaitable.
        public Task<Assertions<T>> ThrowAsync<TException>(string because = "", params object[] args) where TException : Exception => Task.FromResult(this);
        public Task<Assertions<T>> NotThrowAsync(string because = "", params object[] args) => Task.FromResult(this);

        /// <summary>Chain members. Typed loosely on purpose - what follows is another Should().</summary>
        public Assertions<T> And => this;
        public dynamic Which => null!;
        public dynamic WhoseValue => null!;
        public T Subject => default!;
    }

    /// <summary>
    /// What ContainSingle yields. Its Subject is the single element, which the
    /// real library types precisely and this cannot - hence dynamic.
    /// </summary>
    public class SingleAssertions
    {
        public dynamic Subject => null!;
        public dynamic Which => null!;
        public SingleAssertions And => this;
        public SingleAssertions Be(object? expected, string because = "", params object[] args) => this;
    }

    public static class AssertionExtensions
    {
        public static Assertions<T> Should<T>(this T? subject) => new();

        /// <summary>
        /// Defers a call so it can be asserted to throw (or not).
        ///
        /// Action&lt;T&gt; only, and deliberately: an expression lambda converts to
        /// it whether or not the call returns a value, so this covers both
        /// `s => s.StoreSecret(...)` and `c => c.GetCredential&lt;int?&gt;(...)`.
        /// Adding a Func overload alongside it makes every such lambda
        /// ambiguous instead (CS8917).
        /// </summary>
        public static Assertions<object> Invoking<T>(this T subject, Action<T> action) => new();
    }
}

namespace Microsoft.AspNetCore.Mvc.Testing
{
    /// <summary>
    /// Stands in for the real factory. Only the surface LocoApiFactory uses is
    /// declared - if a test reaches for more, this fails and says so rather
    /// than pretending.
    /// </summary>
    public class WebApplicationFactory<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        public HttpClient CreateClient() => new();
        protected virtual void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) { }
        protected virtual void Dispose(bool disposing) { }
        public void Dispose() => Dispose(true);
    }
}
