using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace Loco.Cli.Tests;

/// <summary>
/// Tests for command-line parsing semantics.
///
/// Offline these run against the harness's parser
/// (scripts/offline-test-harness/NuGetPackageStubs.cs); in CI they compile
/// against the real System.CommandLine. Every assertion here is therefore
/// limited to behaviour the two share: subcommand dispatch, --option value
/// with aliases, bool flags, positional order, defaults, exit-code
/// propagation, and a non-zero exit for input that does not parse. If the
/// harness's parser ever drifts from the real package on this subset, CI is
/// what catches it - these same tests, against the real thing.
///
/// They exist because the previous stub returned 0 from InvokeAsync without
/// parsing anything, which meant the entire CLI had zero runtime coverage -
/// and the tests above it were tautologies comparing string literals to
/// themselves.
/// </summary>
public class CommandLineParsingTests
{
    [Fact]
    public async Task Dispatches_to_a_subcommand_by_name()
    {
        var reached = false;
        var sub = new Command("inner", "the subcommand");
        sub.SetHandler(() => { reached = true; });

        var root = new Command("outer", "the root");
        root.AddCommand(sub);

        var exit = await root.InvokeAsync(new[] { "inner" });

        exit.Should().Be(0);
        reached.Should().BeTrue();
    }

    [Fact]
    public async Task Binds_an_option_value_through_its_long_alias()
    {
        string? seen = null;
        var option = new Option<string?>(new[] { "--filter", "-f" }, "filter");
        var command = new Command("t", "test");
        command.AddOption(option);
        command.SetHandler((string? value) => { seen = value; }, option);

        await command.InvokeAsync(new[] { "--filter", "unit" });

        seen.Should().Be("unit");
    }

    [Fact]
    public async Task Binds_an_option_value_through_its_short_alias()
    {
        string? seen = null;
        var option = new Option<string?>(new[] { "--filter", "-f" }, "filter");
        var command = new Command("t", "test");
        command.AddOption(option);
        command.SetHandler((string? value) => { seen = value; }, option);

        await command.InvokeAsync(new[] { "-f", "unit" });

        seen.Should().Be("unit");
    }

    [Fact]
    public async Task Treats_a_bool_option_as_a_flag()
    {
        var seen = false;
        var option = new Option<bool>(new[] { "--verbose", "-v" }, "verbose");
        var command = new Command("t", "test");
        command.AddOption(option);
        command.SetHandler((bool value) => { seen = value; }, option);

        await command.InvokeAsync(new[] { "--verbose" });

        seen.Should().BeTrue();
    }

    [Fact]
    public async Task A_flag_not_given_is_false()
    {
        var seen = true;
        var option = new Option<bool>(new[] { "--verbose", "-v" }, "verbose");
        var command = new Command("t", "test");
        command.AddOption(option);
        command.SetHandler((bool value) => { seen = value; }, option);

        await command.InvokeAsync(Array.Empty<string>());

        seen.Should().BeFalse();
    }

    [Fact]
    public async Task Fills_positional_arguments_in_declaration_order()
    {
        // FilesCommand's search takes (pattern, directory) exactly like this.
        string? first = null, second = null;
        var pattern = new Argument<string>("pattern", "what to find");
        var directory = new Argument<string?>("directory", () => ".", "where");
        var command = new Command("t", "test");
        command.AddArgument(pattern);
        command.AddArgument(directory);
        command.SetHandler((string p, string? d) => { first = p; second = d; }, pattern, directory);

        await command.InvokeAsync(new[] { "*.json", "/tmp" });

        first.Should().Be("*.json");
        second.Should().Be("/tmp");
    }

    [Fact]
    public async Task Uses_the_declared_default_when_an_argument_is_omitted()
    {
        // LogsCommand declares Argument<int>("lines", () => 50).
        var seen = -1;
        var lines = new Argument<int>("lines", () => 50, "how many");
        var command = new Command("t", "test");
        command.AddArgument(lines);
        command.SetHandler((int value) => { seen = value; }, lines);

        await command.InvokeAsync(Array.Empty<string>());

        seen.Should().Be(50);
    }

    [Fact]
    public async Task Parses_an_int_argument()
    {
        var seen = -1;
        var lines = new Argument<int>("lines", () => 50, "how many");
        var command = new Command("t", "test");
        command.AddArgument(lines);
        command.SetHandler((int value) => { seen = value; }, lines);

        await command.InvokeAsync(new[] { "200" });

        seen.Should().Be(200);
    }

    [Fact]
    public async Task Propagates_the_handlers_exit_code()
    {
        var command = new Command("t", "test");
        command.SetHandler(() => Task.FromResult(42));

        var exit = await command.InvokeAsync(Array.Empty<string>());

        exit.Should().Be(42);
    }

    [Fact]
    public async Task Rejects_an_option_nobody_declared()
    {
        var handled = false;
        var command = new Command("t", "test");
        command.SetHandler(() => { handled = true; });

        var exit = await command.InvokeAsync(new[] { "--no-such-option" });

        exit.Should().NotBe(0, "input that does not parse must not exit 0");
        handled.Should().BeFalse("the handler must not run on a parse error");
    }

    [Fact]
    public async Task Rejects_a_missing_required_argument()
    {
        // An Argument<T> without a default is required; run-visual's file
        // argument depends on this being an error rather than a null.
        var handled = false;
        var file = new Argument<string>("file", "path");
        var command = new Command("t", "test");
        command.AddArgument(file);
        command.SetHandler((string _) => { handled = true; }, file);

        var exit = await command.InvokeAsync(Array.Empty<string>());

        exit.Should().NotBe(0);
        handled.Should().BeFalse();
    }

    [Fact]
    public async Task Options_and_positionals_can_interleave()
    {
        // `loco workflow file.json --dry-run` is exactly this shape.
        string? seenFile = null;
        var seenFlag = false;
        var file = new Argument<string>("file", "path");
        var dryRun = new Option<bool>(new[] { "--dry-run", "-n" }, "plan only");
        var command = new Command("t", "test");
        command.AddArgument(file);
        command.AddOption(dryRun);
        command.SetHandler((string f, bool d) => { seenFile = f; seenFlag = d; }, file, dryRun);

        await command.InvokeAsync(new[] { "wf.json", "--dry-run" });

        seenFile.Should().Be("wf.json");
        seenFlag.Should().BeTrue();
    }
}
