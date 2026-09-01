using System;
using System.IO;
using System.Threading.Tasks;
using Loco.Cli;
using Loco.Core.Security;
using Xunit;
using FluentAssertions;

namespace Loco.Cli.Tests;

/// <summary>
/// Tests for `loco hash-password`.
///
/// This command is the product's first step. Every API controller carries
/// [Authorize], the token endpoint fails closed when Auth:Users is empty, and
/// the only way to fill Auth:Users is with a PasswordHasher-format hash - which
/// nothing outside Loco.Api could produce. The documented first step could not
/// be carried out, so nobody could start using Loco at all.
///
/// The property that matters is not that it prints something: it is that what
/// it prints VERIFIES against the password the user typed. A command that
/// emitted a well-formed but wrong hash would lock the user out just as
/// thoroughly, and look fine doing it.
/// </summary>
public class HashPasswordCommandTests : IDisposable
{
    private readonly TextReader _stdin = Console.In;
    private readonly TextWriter _stdout = Console.Out;

    public void Dispose()
    {
        Console.SetIn(_stdin);
        Console.SetOut(_stdout);
    }

    private static (int Exit, string Output) Run(string stdin, params string[] args)
    {
        Console.SetIn(new StringReader(stdin));
        var captured = new StringWriter();
        Console.SetOut(captured);

        var exit = Program.Main(args.Length == 0 ? new[] { "hash-password" } : args)
            .GetAwaiter().GetResult();

        return (exit, captured.ToString());
    }

    [Fact]
    public void Prints_a_hash_that_verifies_against_the_password()
    {
        var (exit, output) = Run("correct horse battery staple\n");

        exit.Should().Be(0);

        var hash = FindHash(output);
        PasswordHasher.Verify("correct horse battery staple", hash).Should().BeTrue(
            "a hash that does not verify locks the user out while looking correct");
    }

    [Fact]
    public void The_printed_hash_rejects_a_different_password()
    {
        var (_, output) = Run("the-real-one\n");

        PasswordHasher.Verify("something-else", FindHash(output)).Should().BeFalse();
    }

    [Fact]
    public void Hashing_the_same_password_twice_gives_different_hashes()
    {
        // Per-hash salt. Identical output would tell an attacker which users
        // share a password just by reading the config file.
        var first = FindHash(Run("same-password\n").Output);
        var second = FindHash(Run("same-password\n").Output);

        first.Should().NotBe(second);
        PasswordHasher.Verify("same-password", first).Should().BeTrue();
        PasswordHasher.Verify("same-password", second).Should().BeTrue();
    }

    [Fact]
    public void Prints_a_config_snippet_the_user_can_paste()
    {
        // The hash alone is not enough to act on - the user has to know which
        // key it goes under, and that scopes exist at all.
        var (_, output) = Run("pw\n");

        output.Should().Contain("\"Auth\"");
        output.Should().Contain("\"PasswordHash\"");
        output.Should().Contain("workflows:execute");
    }

    [Fact]
    public void Empty_input_fails_rather_than_hashing_nothing()
    {
        var (exit, output) = Run("\n");

        exit.Should().Be(1);
        output.Should().Contain("no password");
    }

    [Fact]
    public void Refuses_a_password_passed_as_an_argument()
    {
        // argv would be recorded in shell history and readable from the process
        // list by any other user on the machine.
        var (exit, output) = Run("", "hash-password", "my-password");

        exit.Should().Be(1);
        output.Should().Contain("standard input");
    }

    /// <summary>The hash line: the only line in PasswordHasher's format.</summary>
    private static string FindHash(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("PBKDF2$", StringComparison.Ordinal)
                && trimmed.Split('$').Length == 4)
            {
                return trimmed;
            }
        }

        throw new InvalidOperationException($"No hash found in output:\n{output}");
    }
}
