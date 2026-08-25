// A working subset of FluentAssertions, for running this repository's tests
// without NuGet. See Xunit.cs for why this exists.
//
// Every method here really compares and really throws. That is the whole point:
// an assertion library that quietly returns `this` turns a green run into a
// lie, and a green run is exactly what these tests are being asked to produce.
//
// Where the real library is cleverer, the method below says so rather than
// pretending. The notable narrowings:
//   - Be() compares by value (numbers across types), not by structural equality.
//   - BeEquivalentTo() compares collections order-insensitively and scalars by
//     value; it does not walk object graphs member by member.
//   - The collection predicates take dynamic, because the element type cannot
//     be recovered from the subject without the real library's overload set.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Dynamic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace FluentAssertions
{
    public class Assertions<T>
    {
        private readonly object? _subject;
        private readonly Func<object?>? _deferred;

        public Assertions(object? subject, Func<object?>? deferred = null)
        {
            _subject = subject is DynamicSubject wrapped ? wrapped.Value : subject;
            _deferred = deferred;
        }

        /// <summary>
        /// The call to run for a Throw/NotThrow assertion.
        ///
        /// Either Invoking(...) supplied one, or the subject IS the call:
        /// FluentAssertions lets you write `Action act = () => ...;
        /// act.Should().Throw&lt;T&gt;()` with no Invoking at all, and several
        /// tests here do.
        /// </summary>
        private Func<object?>? Deferred
        {
            get
            {
                if (_deferred is not null) return _deferred;

                // `var act = () => new Thing(null); act.Should().Throw<T>()` -
                // that lambda infers as Func<Thing>, not Action, so matching on
                // Action alone missed every constructor-throws test.
                if (_subject is Delegate call && call.Method.GetParameters().Length == 0)
                {
                    return () =>
                    {
                        try { return call.DynamicInvoke(); }
                        catch (TargetInvocationException wrapper) when (wrapper.InnerException is not null)
                        {
                            throw wrapper.InnerException;
                        }
                    };
                }

                return null;
            }
        }

        private static string Say(string because) =>
            string.IsNullOrWhiteSpace(because) ? "" : $" because {because}";

        private Assertions<T> Fail(string message, string because) =>
            throw new AssertionFailedException(message + Say(because));

        private IEnumerable<object?> Items()
        {
            if (_subject is IEnumerable e && _subject is not string)
                return e.Cast<object?>();
            throw new AssertionFailedException(
                $"expected a collection, but the subject was {Compare.Describe(_subject)}");
        }

        public Assertions<T> Be(object? expected, string because = "", params object[] args) =>
            Compare.AreEqual(expected, _subject)
                ? this
                : Fail($"expected {Compare.Describe(expected)}, found {Compare.Describe(_subject)}", because);

        public Assertions<T> NotBe(object? unexpected, string because = "", params object[] args) =>
            !Compare.AreEqual(unexpected, _subject)
                ? this
                : Fail($"expected anything but {Compare.Describe(unexpected)}", because);

        public Assertions<T> BeTrue(string because = "", params object[] args) =>
            _subject is true ? this : Fail($"expected true, found {Compare.Describe(_subject)}", because);

        public Assertions<T> BeFalse(string because = "", params object[] args) =>
            _subject is false ? this : Fail($"expected false, found {Compare.Describe(_subject)}", because);

        public Assertions<T> BeNull(string because = "", params object[] args) =>
            _subject is null ? this : Fail($"expected null, found {Compare.Describe(_subject)}", because);

        public Assertions<T> NotBeNull(string because = "", params object[] args) =>
            _subject is not null ? this : Fail("expected a value, found null", because);

        public Assertions<T> BeEmpty(string because = "", params object[] args) =>
            !Items().Any() ? this : Fail($"expected an empty collection, found {Compare.Describe(_subject)}", because);

        public Assertions<T> NotBeEmpty(string because = "", params object[] args) =>
            Items().Any() ? this : Fail("expected a non-empty collection", because);

        public Assertions<T> BeNullOrWhiteSpace(string because = "", params object[] args) =>
            string.IsNullOrWhiteSpace(_subject as string)
                ? this
                : Fail($"expected null or whitespace, found {Compare.Describe(_subject)}", because);

        public Assertions<T> NotBeNullOrWhiteSpace(string because = "", params object[] args) =>
            !string.IsNullOrWhiteSpace(_subject as string)
                ? this
                : Fail("expected a non-blank string", because);

        public Assertions<T> NotBeNullOrEmpty(string because = "", params object[] args)
        {
            if (_subject is string s) return !string.IsNullOrEmpty(s) ? this : Fail("expected a non-empty string", because);
            return _subject is not null && Items().Any() ? this : Fail("expected a non-empty collection", because);
        }

        /// <summary>Sequence equality, in order. Accepts either a collection or the items directly.</summary>
        public Assertions<T> Equal(params object?[] expected)
        {
            var wanted = expected.Length == 1 && expected[0] is IEnumerable single && expected[0] is not string
                ? single.Cast<object?>().ToList()
                : expected.ToList();

            var actual = Items().ToList();
            if (actual.Count == wanted.Count && actual.Zip(wanted).All(p => Compare.AreEqual(p.Second, p.First)))
                return this;

            throw new AssertionFailedException(
                $"expected {Compare.Describe(wanted)}, found {Compare.Describe(actual)}");
        }

        /// <summary>Same members, any order. Scalars fall back to Be().</summary>
        public Assertions<T> BeEquivalentTo(object? expected, string because = "", params object[] args)
        {
            if (_subject is not IEnumerable || _subject is string) return Be(expected, because);

            var actual = Items().ToList();
            var wanted = expected is IEnumerable e && expected is not string
                ? e.Cast<object?>().ToList()
                : new List<object?> { expected };

            var remaining = new List<object?>(wanted);
            foreach (var item in actual)
            {
                var match = remaining.FirstIndexOf(candidate => Compare.AreEqual(candidate, item));
                if (match < 0)
                    return Fail($"did not expect {Compare.Describe(item)} in {Compare.Describe(actual)}", because);
                remaining.RemoveAt(match);
            }

            return remaining.Count == 0
                ? this
                : Fail($"missing {Compare.Describe(remaining)} from {Compare.Describe(actual)}", because);
        }

        public Assertions<T> Contain(object? expected, string because = "", params object[] args)
        {
            if (_subject is string s)
                return s.Contains(Convert.ToString(expected) ?? "", StringComparison.Ordinal)
                    ? this
                    : Fail($"expected \"{s}\" to contain {Compare.Describe(expected)}", because);

            return Items().Any(item => Compare.AreEqual(expected, item))
                ? this
                : Fail($"expected {Compare.Describe(_subject)} to contain {Compare.Describe(expected)}", because);
        }

        public Assertions<T> Contain(Func<dynamic, bool> predicate, string because = "", params object[] args) =>
            Items().Any(item => predicate(new DynamicSubject(item)))
                ? this
                : Fail("expected an item matching the predicate", because);

        public Assertions<T> NotContain(object? unexpected, string because = "", params object[] args)
        {
            if (_subject is string s)
                return !s.Contains(Convert.ToString(unexpected) ?? "", StringComparison.Ordinal)
                    ? this
                    : Fail($"expected \"{s}\" not to contain {Compare.Describe(unexpected)}", because);

            return !Items().Any(item => Compare.AreEqual(unexpected, item))
                ? this
                : Fail($"expected {Compare.Describe(_subject)} not to contain {Compare.Describe(unexpected)}", because);
        }

        public Assertions<T> NotContain(Func<dynamic, bool> predicate, string because = "", params object[] args) =>
            !Items().Any(item => predicate(new DynamicSubject(item)))
                ? this
                : Fail("expected no item to match the predicate", because);

        public Assertions<T> OnlyContain(Func<dynamic, bool> predicate, string because = "", params object[] args) =>
            Items().All(item => predicate(new DynamicSubject(item)))
                ? this
                : Fail("expected every item to match the predicate", because);

        public Assertions<T> AllSatisfy(Action<dynamic> inspector, string because = "", params object[] args)
        {
            foreach (var item in Items()) inspector(new DynamicSubject(item));
            return this;
        }

        public Assertions<T> ContainInOrder(params object?[] expected)
        {
            var actual = Items().ToList();
            var at = 0;
            foreach (var wanted in expected)
            {
                at = actual.FindIndex(at, item => Compare.AreEqual(wanted, item));
                if (at < 0)
                    throw new AssertionFailedException(
                        $"expected {Compare.Describe(actual)} to contain {Compare.Describe(expected)} in order");
                at++;
            }
            return this;
        }

        private bool TryGetValue(object? key, out object? value)
        {
            value = null;
            if (_subject is IDictionary dictionary)
            {
                if (!dictionary.Contains(key!)) return false;
                value = dictionary[key!];
                return true;
            }

            // Generic dictionaries that do not implement the non-generic face.
            var method = _subject?.GetType().GetMethod("TryGetValue");
            if (method is null) return false;
            var parameters = new[] { key, null };
            var found = (bool)method.Invoke(_subject, parameters)!;
            value = parameters[1];
            return found;
        }

        public KeyedAssertions ContainKey(object? key, string because = "", params object[] args) =>
            TryGetValue(key, out var value)
                ? new KeyedAssertions(value)
                : throw new AssertionFailedException(
                    $"expected a key {Compare.Describe(key)}{Say(because)}, found {Compare.Describe(_subject)}");

        public Assertions<T> NotContainKey(object? key, string because = "", params object[] args) =>
            !TryGetValue(key, out _)
                ? this
                : Fail($"did not expect a key {Compare.Describe(key)}", because);

        public SingleAssertions ContainSingle(string because = "", params object[] args)
        {
            var items = Items().ToList();
            return items.Count == 1
                ? new SingleAssertions(items[0])
                : throw new AssertionFailedException(
                    $"expected exactly one item{Say(because)}, found {items.Count}");
        }

        public SingleAssertions ContainSingle(Func<dynamic, bool> predicate, string because = "", params object[] args)
        {
            var matches = Items().Where(item => predicate(new DynamicSubject(item))).ToList();
            return matches.Count == 1
                ? new SingleAssertions(matches[0])
                : throw new AssertionFailedException(
                    $"expected exactly one matching item{Say(because)}, found {matches.Count}");
        }

        public Assertions<T> HaveCount(int expected, string because = "", params object[] args)
        {
            var actual = Items().Count();
            return actual == expected
                ? this
                : Fail($"expected {expected} items, found {actual}", because);
        }

        public Assertions<T> BeOneOf(params object?[] validValues) =>
            validValues.Any(candidate => Compare.AreEqual(candidate, _subject))
                ? this
                : throw new AssertionFailedException(
                    $"expected one of {Compare.Describe(validValues)}, found {Compare.Describe(_subject)}");

        /// <summary>The trailing string is a "because" reason, not another candidate.</summary>
        public Assertions<T> BeOneOf(object? a, object? b, string because) =>
            Compare.AreEqual(a, _subject) || Compare.AreEqual(b, _subject)
                ? this
                : Fail($"expected {Compare.Describe(a)} or {Compare.Describe(b)}, found {Compare.Describe(_subject)}", because);

        private Assertions<T> CompareTo(object? expected, string because, Func<int, bool> accept, string wording)
        {
            if (_subject is not IComparable comparable)
                return Fail($"{Compare.Describe(_subject)} is not comparable", because);

            return accept(comparable.CompareTo(Convert.ChangeType(expected, _subject!.GetType())))
                ? this
                : Fail($"expected {wording} {Compare.Describe(expected)}, found {Compare.Describe(_subject)}", because);
        }

        public Assertions<T> BeGreaterThan(object? expected, string because = "", params object[] args) =>
            CompareTo(expected, because, c => c > 0, "greater than");

        public Assertions<T> BeGreaterThanOrEqualTo(object? expected, string because = "", params object[] args) =>
            CompareTo(expected, because, c => c >= 0, "at least");

        public Assertions<T> BeLessThan(object? expected, string because = "", params object[] args) =>
            CompareTo(expected, because, c => c < 0, "less than");

        public Assertions<T> BeLessThanOrEqualTo(object? expected, string because = "", params object[] args) =>
            CompareTo(expected, because, c => c <= 0, "at most");

        public Assertions<T> BeInAscendingOrder(string because = "", params object[] args)
        {
            var items = Items().ToList();
            for (var i = 1; i < items.Count; i++)
            {
                if (items[i - 1] is IComparable previous && previous.CompareTo(items[i]) > 0)
                    return Fail($"expected ascending order, found {Compare.Describe(items)}", because);
            }
            return this;
        }

        public Assertions<T> BeAssignableTo<TTarget>(string because = "", params object[] args) =>
            _subject is TTarget
                ? this
                : Fail($"expected something assignable to {typeof(TTarget).Name}, " +
                       $"found {_subject?.GetType().Name ?? "null"}", because);

        private Exception? Capture()
        {
            var call = Deferred
                ?? throw new AssertionFailedException(
                    "Throw/NotThrow needs a deferred call - use Invoking(...), or make the subject an Action");
            try { call(); return null; }
            catch (Exception ex) { return ex; }
        }

        /// <summary>
        /// Runs the deferred call and awaits it if it returned a Task. Without
        /// the await, an exception thrown inside an async method lands on the
        /// Task and is never seen - the assertion then reports "nothing was
        /// thrown" about a call that threw.
        /// </summary>
        private async Task<Exception?> CaptureAsync()
        {
            var call = Deferred
                ?? throw new AssertionFailedException(
                    "ThrowAsync/NotThrowAsync needs a deferred call - use Invoking(...) first");
            try
            {
                if (call() is Task task) await task;
                return null;
            }
            catch (Exception ex) { return ex; }
        }

        public ExceptionAssertions Throw<TException>(string because = "", params object[] args)
            where TException : Exception
        {
            var thrown = Capture();
            if (thrown is TException expected) return new ExceptionAssertions(expected);

            throw new AssertionFailedException(thrown is null
                ? $"expected {typeof(TException).Name}{Say(because)}, but nothing was thrown"
                : $"expected {typeof(TException).Name}{Say(because)}, but got {thrown.GetType().Name}: {thrown.Message}");
        }

        public Assertions<T> NotThrow(string because = "", params object[] args)
        {
            var thrown = Capture();
            return thrown is null
                ? this
                : Fail($"expected no exception, but got {thrown.GetType().Name}: {thrown.Message}", because);
        }

        public async Task<ExceptionAssertions> ThrowAsync<TException>(string because = "", params object[] args)
            where TException : Exception
        {
            var thrown = await CaptureAsync();
            if (thrown is TException expected) return new ExceptionAssertions(expected);

            throw new AssertionFailedException(thrown is null
                ? $"expected {typeof(TException).Name}{Say(because)}, but nothing was thrown"
                : $"expected {typeof(TException).Name}{Say(because)}, but got {thrown.GetType().Name}: {thrown.Message}");
        }

        public async Task<Assertions<T>> NotThrowAsync(string because = "", params object[] args)
        {
            var thrown = await CaptureAsync();
            return thrown is null
                ? this
                : Fail($"expected no exception, but got {thrown.GetType().Name}: {thrown.Message}", because);
        }

        public Assertions<T> And => this;
        // dynamic, so `.Subject.SomeProperty` compiles. At run time it resolves
        // by reflection against the real object, so a wrong member still fails -
        // just at the assertion rather than at build.
        public dynamic Which => new DynamicSubject(_subject);
        public dynamic Subject => new DynamicSubject(_subject);
    }

    /// <summary>What ContainKey yields, so `.WhoseValue` is the value at that key.</summary>
    public class KeyedAssertions
    {
        private readonly object? _value;
        public KeyedAssertions(object? value) => _value = value;
        public dynamic WhoseValue => new DynamicSubject(_value);
        public dynamic Which => new DynamicSubject(_value);
    }

    /// <summary>What ContainSingle yields, so `.Subject` is the item and not the collection.</summary>
    public class SingleAssertions
    {
        private readonly object? _item;
        public SingleAssertions(object? item) => _item = item;
        public dynamic Subject => new DynamicSubject(_item);
        public dynamic Which => new DynamicSubject(_item);
        public SingleAssertions And => this;

        public SingleAssertions Be(object? expected, string because = "", params object[] args) =>
            Compare.AreEqual(expected, _item)
                ? this
                : throw new AssertionFailedException(
                    $"expected {Compare.Describe(expected)}, found {Compare.Describe(_item)}");
    }

    public class ExceptionAssertions
    {
        private readonly Exception _thrown;
        public ExceptionAssertions(Exception thrown) => _thrown = thrown;
        public Exception Which => _thrown;
        public Exception Subject => _thrown;
        public ExceptionAssertions And => this;

        public ExceptionAssertions WithMessage(string expected, string because = "", params object[] args)
        {
            // The real library treats this as a wildcard match; substring is the
            // honest narrowing, and every call site here passes a plain phrase.
            var pattern = expected.Trim('*');
            return _thrown.Message.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                ? this
                : throw new AssertionFailedException(
                    $"expected a message containing \"{pattern}\", found \"{_thrown.Message}\"");
        }
    }

    /// <summary>
    /// A call captured by Invoking(...), waiting for a Throw/NotThrow assertion.
    ///
    /// A distinct type because the assertion is written `x.Invoking(...).Should()
    /// .Throw&lt;T&gt;()`: if Invoking returned Assertions directly, the Should()
    /// after it would wrap THAT in a second Assertions whose subject is the
    /// first - and the deferred call would be lost, which reads as "nothing was
    /// thrown" about a call that was never made.
    /// </summary>
    public sealed class DeferredCall
    {
        internal DeferredCall(Func<object?> call) => Call = call;
        internal Func<object?> Call { get; }
    }

    /// <summary>
    /// Wraps a value so `dynamic` member access still finds Should().
    ///
    /// The C# runtime binder does not look for extension methods, so
    /// `single.Subject.Name.Should()` fails on a plain dynamic even though it
    /// compiles. Routing member access through DynamicObject lets Should() be
    /// answered here, while every other member is reflected off the real value -
    /// so a wrong property name still fails, just at run time.
    /// </summary>
    public sealed class DynamicSubject : DynamicObject
    {
        private readonly object? _value;
        public DynamicSubject(object? value) => _value = value;

        internal object? Value => _value;

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = null;
            if (_value is null) return false;

            var type = _value.GetType();
            var property = type.GetProperty(binder.Name);
            if (property is not null)
            {
                result = Wrap(property.GetValue(_value));
                return true;
            }

            var field = type.GetField(binder.Name);
            if (field is null) return false;

            result = Wrap(field.GetValue(_value));
            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
        {
            if (binder.Name == "Should" && (args is null || args.Length == 0))
            {
                result = new Assertions<object>(_value);
                return true;
            }

            result = null;
            if (_value is null) return false;

            var method = _value.GetType().GetMethod(binder.Name,
                (args ?? Array.Empty<object?>()).Select(a => a?.GetType() ?? typeof(object)).ToArray());
            if (method is null) return false;

            result = Wrap(method.Invoke(_value, args));
            return true;
        }

        /// <summary>
        /// `r.Id == "rule-1"` inside a predicate: both sides must be unwrapped,
        /// or the binder sees DynamicSubject and refuses the operator.
        /// </summary>
        public override bool TryBinaryOperation(
            BinaryOperationBinder binder, object? arg, out object? result)
        {
            var right = arg is DynamicSubject other ? other.Value : arg;

            switch (binder.Operation)
            {
                case System.Linq.Expressions.ExpressionType.Equal:
                    result = Compare.AreEqual(_value, right);
                    return true;
                case System.Linq.Expressions.ExpressionType.NotEqual:
                    result = !Compare.AreEqual(_value, right);
                    return true;
            }

            if (_value is IComparable comparable && right is not null)
            {
                var order = comparable.CompareTo(Convert.ChangeType(right, _value.GetType()));
                result = binder.Operation switch
                {
                    System.Linq.Expressions.ExpressionType.LessThan => order < 0,
                    System.Linq.Expressions.ExpressionType.LessThanOrEqual => order <= 0,
                    System.Linq.Expressions.ExpressionType.GreaterThan => order > 0,
                    System.Linq.Expressions.ExpressionType.GreaterThanOrEqual => order >= 0,
                    _ => (object?)null,
                };
                if (result is not null) return true;
            }

            result = null;
            return false;
        }

        public override bool TryConvert(ConvertBinder binder, out object? result)
        {
            result = _value;
            return _value is null || binder.Type.IsInstanceOfType(_value);
        }

        public override string? ToString() => _value?.ToString();

        internal static object? Wrap(object? value) =>
            value is null || value is string || value.GetType().IsPrimitive
                ? new DynamicSubject(value)
                : new DynamicSubject(value);
    }

    public static class AssertionExtensions
    {
        public static Assertions<T> Should<T>(this T? subject) => new(Unwrap(subject));

        /// <summary>More specific than the generic Should, so it wins here.</summary>
        public static Assertions<object> Should(this DeferredCall call) => new(null, call.Call);

        /// <summary>
        /// Defers a call so it can be asserted to throw (or not). Action&lt;T&gt;
        /// only: an expression lambda converts to it whether or not the call
        /// returns a value, and adding a Func overload makes every such lambda
        /// ambiguous (CS8917).
        /// </summary>
        public static DeferredCall Invoking<T>(this T subject, Action<T> action) =>
            new(() => { action(subject); return null; });

        /// <summary>
        /// The async form, and it must exist separately.
        ///
        /// A lambda whose body returns a Task converts to Action&lt;T&gt; happily -
        /// by DISCARDING the Task. An exception thrown inside an async method
        /// lands on that Task, so the assertion saw "nothing was thrown" about a
        /// call that threw. C# prefers this overload for a Task-returning body
        /// and the Action one for a void body, which is exactly the split needed.
        /// </summary>
        public static DeferredCall Invoking<T>(this T subject, Func<T, Task> call) =>
            new(() => call(subject));

        internal static object? Unwrap(object? value) =>
            value is DynamicSubject wrapped ? wrapped.Value : value;
    }

    internal static class ListExtensions
    {
        public static int FirstIndexOf<T>(this List<T> list, Func<T, bool> predicate)
        {
            for (var i = 0; i < list.Count; i++)
                if (predicate(list[i])) return i;
            return -1;
        }
    }
}
