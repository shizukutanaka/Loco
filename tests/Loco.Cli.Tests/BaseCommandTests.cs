using System.Collections.Generic;
using Loco.Cli.Commands;
using Xunit;
using FluentAssertions;

namespace Loco.Cli.Tests;

/// <summary>
/// Tests for BaseCommand's hand-rolled argument helpers, used by the commands
/// that do not go through System.CommandLine (setup, secrets, backup-config).
///
/// TryConsumeOption's return value is a trap worth pinning exactly as it is:
/// it returns TRUE when the option is ABSENT (nothing to consume is fine) and
/// FALSE only when the option is present with no value following it. It is a
/// "did parsing stay valid" flag, not a "was it found" flag - the out value's
/// null-ness answers that.
/// </summary>
public class BaseCommandTests
{
    [Fact]
    public void TryConsumeOption_removes_the_option_and_its_value()
    {
        var args = new List<string> { "create", "--name", "nightly", "--force" };

        var ok = BaseCommand.TryConsumeOption(args, "--name", out var value);

        ok.Should().BeTrue();
        value.Should().Be("nightly");
        args.Should().Equal("create", "--force");
    }

    [Fact]
    public void TryConsumeOption_returns_true_when_the_option_is_absent()
    {
        var args = new List<string> { "create" };

        var ok = BaseCommand.TryConsumeOption(args, "--name", out var value);

        ok.Should().BeTrue("absence is not a parse error");
        value.Should().BeNull("null is what says it was not found");
        args.Should().Equal("create");
    }

    [Fact]
    public void TryConsumeOption_fails_only_when_the_value_is_missing()
    {
        var args = new List<string> { "create", "--name" };

        var ok = BaseCommand.TryConsumeOption(args, "--name", out var value);

        ok.Should().BeFalse("an option at the end of argv has no value to take");
        value.Should().BeNull();
    }

    [Fact]
    public void ConsumeFlag_reports_and_removes_a_present_flag()
    {
        var args = new List<string> { "delete", "--force" };

        BaseCommand.ConsumeFlag(args, "--force").Should().BeTrue();
        args.Should().Equal("delete");
    }

    [Fact]
    public void ConsumeFlag_is_false_for_an_absent_flag()
    {
        var args = new List<string> { "delete" };

        BaseCommand.ConsumeFlag(args, "--force").Should().BeFalse();
        args.Should().Equal("delete");
    }
}
