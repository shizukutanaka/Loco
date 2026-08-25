// Runs the tests. See Xunit.cs for why this exists rather than `dotnet test`.
//
// Finds every [Fact] and [Theory] in the loaded assembly, constructs its class,
// invokes it, and reports what happened. Deliberately small - it does not do
// parallelism, fixtures beyond a parameterless constructor, output capture, or
// any of the hundred things real xunit does. It does the one thing that was
// missing: it executes the assertions and tells you which ones fail.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Loco.OfflineTestRunner
{
    public static class Runner
    {
        private sealed record Failure(string Test, string Message);

        public static async Task<int> Main(string[] args)
        {
            var verbose = args.Contains("--verbose");
            var assembly = typeof(Runner).Assembly;
            var stopwatch = Stopwatch.StartNew();

            var passed = 0;
            var failures = new List<Failure>();

            var classes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetMethods().Any(HasTestAttribute))
                .OrderBy(t => t.FullName, StringComparer.Ordinal);

            foreach (var testClass in classes)
            {
                foreach (var method in testClass.GetMethods().Where(HasTestAttribute)
                             .OrderBy(m => m.Name, StringComparer.Ordinal))
                {
                    foreach (var (arguments, label) in CasesFor(method))
                    {
                        var name = $"{testClass.Name}.{method.Name}{label}";
                        object? instance = null;
                        try
                        {
                            instance = Activator.CreateInstance(testClass);
                            var result = method.Invoke(instance, arguments);
                            if (result is Task task) await task;

                            passed++;
                            if (verbose) Console.WriteLine($"  ok   {name}");
                        }
                        catch (Exception ex)
                        {
                            // Reflection wraps whatever the test threw.
                            var actual = ex is TargetInvocationException wrapper && wrapper.InnerException is not null
                                ? wrapper.InnerException
                                : ex;

                            var detail = actual is AssertionFailedException
                                ? actual.Message
                                : $"{actual.GetType().Name}: {actual.Message}";

                            failures.Add(new Failure(name, detail));
                            Console.WriteLine($"  FAIL {name}");
                        }
                        finally
                        {
                            if (instance is IDisposable disposable)
                            {
                                // A test that fails must still release its temp
                                // directory, or later tests fail for the wrong reason.
                                try { disposable.Dispose(); } catch { }
                            }
                        }
                    }
                }
            }

            stopwatch.Stop();

            if (failures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"{failures.Count} failure(s):");
                foreach (var failure in failures)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {failure.Test}");
                    foreach (var line in failure.Message.Split('\n'))
                        Console.WriteLine($"      {line.TrimEnd()}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"passed: {passed}   failed: {failures.Count}   ({stopwatch.ElapsedMilliseconds} ms)");

            return failures.Count == 0 ? 0 : 1;
        }

        private static bool HasTestAttribute(MethodInfo method) =>
            method.GetCustomAttribute<FactAttribute>() is not null ||
            method.GetCustomAttribute<TheoryAttribute>() is not null;

        /// <summary>
        /// One case per [InlineData], or a single case with no arguments. A
        /// [Theory] with no data would silently pass as a no-argument call in a
        /// naive runner, so it is reported as a failure instead.
        /// </summary>
        private static IEnumerable<(object?[]? Arguments, string Label)> CasesFor(MethodInfo method)
        {
            var inline = method.GetCustomAttributes<InlineDataAttribute>().ToList();

            if (inline.Count == 0)
            {
                if (method.GetParameters().Length > 0)
                {
                    yield return (null, "(no data)");
                    yield break;
                }
                yield return (null, "");
                yield break;
            }

            foreach (var data in inline)
            {
                var label = "(" + string.Join(", ", data.Data.Select(Compare.Describe)) + ")";
                yield return (Coerce(method, data.Data), label);
            }
        }

        /// <summary>
        /// InlineData arrives boxed and loosely typed - an int literal for a
        /// long parameter, for instance - so each argument is converted to what
        /// the method actually declares.
        /// </summary>
        private static object?[] Coerce(MethodInfo method, object?[] data)
        {
            var parameters = method.GetParameters();
            var coerced = new object?[data.Length];

            for (var i = 0; i < data.Length && i < parameters.Length; i++)
            {
                var target = Nullable.GetUnderlyingType(parameters[i].ParameterType)
                             ?? parameters[i].ParameterType;

                if (data[i] is null || target.IsInstanceOfType(data[i]))
                {
                    coerced[i] = data[i];
                    continue;
                }

                try
                {
                    coerced[i] = target.IsEnum
                        ? Enum.ToObject(target, data[i]!)
                        : Convert.ChangeType(data[i], target);
                }
                catch
                {
                    coerced[i] = data[i];
                }
            }

            return coerced;
        }
    }
}
