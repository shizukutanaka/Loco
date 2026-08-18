using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Loco.Api.Contracts;
using Loco.Core.Integrations.Core;

namespace Loco.Api.Controllers;

/// <summary>
/// What each connector needs in order to act as you.
///
/// Every connector already declares its credentials precisely - name, label,
/// type, whether it is required - in <see cref="IConnector.AuthConfig"/>, and
/// the names there match the ones it reads at execution time for all 28 of
/// them. None of that reached the browser, so the connections UI had to ask the
/// user to type field names from memory next to the warning "must match the
/// connector's field name". A typo produced a connection that saved cleanly,
/// listed cleanly, and failed at execution with a credential the connector
/// never found.
///
/// This endpoint publishes the declaration so the form can render the actual
/// fields. Read-only and secret-free: it describes what a credential is called,
/// never what any stored credential is.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class ConnectorsController : ControllerBase
{
    private readonly ConnectorRegistry _registry;

    public ConnectorsController(ConnectorRegistry registry) => _registry = registry;

    /// <summary>Every registered connector and the credential fields it declares.</summary>
    [HttpGet]
    [Authorize(Policy = "CanViewWorkflows")]
    public IActionResult GetConnectors()
    {
        var connectors = _registry.GetConnectorIds()
            .Select(id => _registry.GetConnector(id))
            .Where(c => c is not null)
            .Select(c => Describe(c!))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(Envelope.Ok(new { connectors, total = connectors.Count }));
    }

    /// <summary>One connector's declaration, for the credential form.</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanViewWorkflows")]
    public IActionResult GetConnector(string id)
    {
        var connector = _registry.GetConnector(id);
        if (connector is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"No connector is registered with id '{id}'"));
        }

        return Ok(Envelope.Ok(Describe(connector)));
    }

    /// <summary>
    /// Shapes one connector for the editor. Deliberately narrow: the editor
    /// needs to render a credential form and label a connector, so anything the
    /// form cannot use stays out rather than becoming a contract to maintain.
    /// </summary>
    private static ConnectorDescriptor Describe(IConnector connector) => new(
        connector.Id,
        connector.Name,
        connector.Description,
        connector.Category.ToString(),
        connector.AuthConfig.Type.ToString(),
        connector.AuthConfig.RequiredCredentials
            .Select(field => new CredentialFieldDescriptor(
                field.Name,
                field.Label,
                // "password" tells the form to mask the input; the frontend
                // treats every other type as plain text.
                field.Type == ParameterType.Password ? "password" : "text",
                field.Required,
                field.Description))
            .ToList());
}

/// <summary>
/// One connector as the credential form needs it. Serialized camelCase by the
/// API's JSON options, matching ConnectorDescriptor in
/// src/Loco.VisualEditor/src/api/connectors.ts.
/// </summary>
/// <param name="Id">Connector id, e.g. "slack" - what a connection stores.</param>
/// <param name="AuthType">
/// The connector's authentication style, so the form can explain what it is
/// asking for rather than presenting an unlabelled list of secrets.
/// </param>
public sealed record ConnectorDescriptor(
    string Id,
    string Name,
    string Description,
    string Category,
    string AuthType,
    IReadOnlyList<CredentialFieldDescriptor> CredentialFields);

/// <summary>
/// One credential field the connector reads. This is a description of a field,
/// never a value: no endpoint on this controller touches stored secrets.
/// </summary>
/// <param name="Name">
/// The exact key the connector reads, e.g. Slack's "botToken". This is the
/// value the form must submit - it is what stops a hand-typed name from
/// producing a connection that saves and then fails.
/// </param>
/// <param name="Type">"password" to mask the input, "text" otherwise.</param>
public sealed record CredentialFieldDescriptor(
    string Name,
    string Label,
    string Type,
    bool Required,
    string? Description);
