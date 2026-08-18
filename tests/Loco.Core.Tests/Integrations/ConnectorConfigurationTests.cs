using System;
using System.Collections.Generic;
using Loco.Core.Integrations.Core;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Integrations;

/// <summary>
/// Tests for ConnectorConfiguration's typed accessors.
///
/// These sit on the only seam between where a credential is stored and where it
/// is used, and the two sides disagree about representation on purpose: the
/// secrets store holds text, while a connector asks for the type the field
/// actually is. GetCredential&lt;int?&gt;("port") appears in the Postgres, MySQL,
/// Redis and SMTP connectors, and every value reaching it is a string.
///
/// Before the accessors converted, that combination threw: unboxing an object
/// holding "5432" to int? is an InvalidCastException. Supplying the port broke
/// the connection while leaving it blank worked, because the missing-key path
/// returned the default instead. A test that stores an int directly cannot
/// reproduce it, which is why the cases below store strings - the way the real
/// store does.
///
/// NOTE: authored where dotnet test could not run (NuGet egress blocked by
/// organization policy); the first CI run is what executes these.
/// </summary>
public class ConnectorConfigurationTests
{
    private static ConnectorConfiguration WithCredential(string name, object? value) =>
        new() { Credentials = new Dictionary<string, object?> { [name] = value } };

    [Fact]
    public void Reads_a_numeric_credential_that_was_stored_as_text()
    {
        // Exactly what JsonFileConnectionStore produces, and exactly what
        // PostgreSqlConnector asks for.
        WithCredential("port", "5432").GetCredential<int?>("port").Should().Be(5432);
    }

    [Fact]
    public void Falls_back_to_the_default_when_the_value_is_not_a_number()
    {
        // A mistyped port must not take the workflow down with it.
        var port = WithCredential("port", "not-a-port").GetCredential<int?>("port") ?? 5432;

        port.Should().Be(5432);
    }

    [Fact]
    public void Falls_back_to_the_default_when_the_number_does_not_fit()
    {
        WithCredential("port", "99999999999999999999")
            .GetCredential<int?>("port").Should().BeNull();
    }

    [Fact]
    public void Reads_a_credential_stored_as_its_own_type_unchanged()
    {
        WithCredential("port", 6379).GetCredential<int?>("port").Should().Be(6379);
    }

    [Fact]
    public void Reads_a_boolean_credential_stored_as_text()
    {
        WithCredential("useTls", "true").GetCredential<bool?>("useTls").Should().BeTrue();
    }

    [Fact]
    public void Renders_a_numeric_credential_as_text_when_a_connector_asks_for_a_string()
    {
        // The reverse direction: settings loaded from JSON can arrive as numbers
        // while the connector interpolates them into a connection string.
        WithCredential("port", 5432).GetCredentialString("port").Should().Be("5432");
    }

    [Fact]
    public void Reads_an_enum_credential_by_name_ignoring_case()
    {
        WithCredential("type", "oauth2")
            .GetCredential<AuthenticationType?>("type").Should().Be(AuthenticationType.OAuth2);
    }

    [Fact]
    public void Returns_the_default_for_a_credential_that_was_never_set()
    {
        var config = new ConnectorConfiguration();

        config.GetCredential<int?>("port").Should().BeNull();
        config.GetCredentialString("apiKey").Should().BeNull();
    }

    [Fact]
    public void Returns_the_default_for_a_credential_explicitly_set_to_null()
    {
        WithCredential("apiKey", null).GetCredentialString("apiKey").Should().BeNull();
    }

    [Fact]
    public void Never_throws_for_a_value_it_cannot_convert()
    {
        // The guarantee that matters: a bad credential degrades to the
        // connector's default, it does not surface as an unhandled exception
        // halfway through initialization.
        var config = WithCredential("port", new object());

        config.Invoking(c => c.GetCredential<int?>("port")).Should().NotThrow();
    }

    [Fact]
    public void Applies_the_same_conversion_to_settings()
    {
        var config = new ConnectorConfiguration
        {
            Settings = new Dictionary<string, object?> { ["timeoutSeconds"] = "30" },
        };

        config.GetSetting<int?>("timeoutSeconds").Should().Be(30);
    }

    [Fact]
    public void Parses_numbers_the_same_way_in_every_locale()
    {
        // Invariant culture, so a machine with a comma decimal separator reads
        // the same credential the same way.
        WithCredential("rate", "1.5").GetCredential<double?>("rate").Should().Be(1.5d);
    }
}
