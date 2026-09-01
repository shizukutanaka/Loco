using System;
using System.Threading.Tasks;
using Loco.Cli;
using Xunit;
using FluentAssertions;

namespace Loco.Cli.Tests;

/// <summary>
/// Tests for the CLI's outermost contract: which argv reaches which command,
/// and which exit code comes back. These call the real Program.Main.
///
/// They replace sixteen tests that compared string literals to themselves
/// ("version".ToLowerInvariant() == "version") without ever referencing a
/// Loco.Cli type - twenty green results that could not have caught the CLI
/// being deleted outright.
///
/// Commands deliberately NOT invoked here, with the reason stated rather than
/// implied: `start` awaits Ctrl+C forever; `update` calls api.github.com;
/// `secrets` creates $HOME/.loco/secrets in its constructor; `test` shells
/// out to dotnet test; `setup` with NO arguments blocks on stdin and writes
/// to LocalApplicationData - its argument'd form is the safe one, inverted
/// from every other command.
/// </summary>
public class ProgramDispatchTests
{
    [Fact]
    public async Task No_arguments_shows_help_and_succeeds()
    {
        (await Program.Main(Array.Empty<string>())).Should().Be(0);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("-v")]
    [InlineData("--version")]
    [InlineData("help")]
    [InlineData("-h")]
    [InlineData("--help")]
    public async Task Informational_commands_exit_zero(string command)
    {
        (await Program.Main(new[] { command })).Should().Be(0);
    }

    [Fact]
    public async Task Commands_are_matched_case_insensitively()
    {
        (await Program.Main(new[] { "VERSION" })).Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_command_exits_nonzero()
    {
        (await Program.Main(new[] { "no-such-command" })).Should().Be(1);
    }

    [Fact]
    public async Task Setup_refuses_unknown_options()
    {
        // The guarded path: SetupCommand returns 1 for any argument before the
        // interactive wizard can start. The bare form is untestable here - it
        // reads stdin and writes real config - which is itself worth knowing.
        (await Program.Main(new[] { "setup", "--no-such-flag" })).Should().Be(1);
    }

    [Fact]
    public async Task Backup_config_without_a_verb_prints_usage_and_fails()
    {
        (await Program.Main(new[] { "backup-config" })).Should().Be(1);
    }

    [Fact]
    public async Task Workflow_without_a_file_is_a_parse_error()
    {
        // The file argument has no default, so this must fail in parsing,
        // before any handler runs.
        (await Program.Main(new[] { "workflow" })).Should().Be(1);
    }

    [Fact]
    public async Task Diag_runs_and_succeeds()
    {
        // DiagCommand only reads environment facts and prints them.
        (await Program.Main(new[] { "diag" })).Should().Be(0);
    }
}
