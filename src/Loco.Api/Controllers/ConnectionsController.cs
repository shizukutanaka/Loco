using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Loco.Api.Contracts;
using Loco.Core.Integrations.Core;
using Loco.Core.Storage;

namespace Loco.Api.Controllers;

/// <summary>
/// Stored connector credentials - what the editor calls "connections".
///
/// Implements the contract the editor's client already speaks
/// (src/Loco.VisualEditor/src/api/connections.ts), which was written against a
/// server side that did not exist. Two properties are load-bearing and enforced
/// here, not merely documented:
///
///  1. Secret values travel client -> server only. No response on this
///     controller carries one: <see cref="StoredConnection"/> records which
///     fields are set (ConfiguredFields), never their values. A response body is
///     the easiest place for a secret to leak into logs and error reporters.
///  2. Verification runs server-side (POST {id}/test), so checking a credential
///     never ships it to the browser.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class ConnectionsController : ControllerBase
{
    private readonly JsonFileConnectionStore _store;
    private readonly ConnectorRegistry _registry;
    private readonly ILogger<ConnectionsController> _logger;

    public ConnectionsController(
        JsonFileConnectionStore store,
        ConnectorRegistry registry,
        ILogger<ConnectionsController> logger)
    {
        _store = store;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>List connections (metadata only), optionally filtered by connector.</summary>
    [HttpGet]
    [Authorize(Policy = "CanViewWorkflows")]
    public async Task<IActionResult> GetConnections(
        [FromQuery] string? connectorId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var all = await _store.ListAsync(connectorId, cancellationToken);

        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(Envelope.Ok(new
        {
            connections = items,
            total = all.Count,
            page,
            pageSize,
        }));
    }

    /// <summary>Get one connection's metadata. Never returns secret values.</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanViewWorkflows")]
    public async Task<IActionResult> GetConnection(string id, CancellationToken cancellationToken)
    {
        var connection = await _store.GetAsync(id, cancellationToken);
        if (connection is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Connection '{id}' was not found"));
        }

        return Ok(Envelope.Ok(connection));
    }

    /// <summary>Create a connection. The server assigns the id.</summary>
    [HttpPost]
    [Authorize(Policy = "CanManageWorkflows")]
    public async Task<IActionResult> CreateConnection(
        [FromBody] ConnectionCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(Envelope.Fail("INVALID_ARGUMENT", "Connection name is required"));
        }

        if (string.IsNullOrWhiteSpace(request.ConnectorId))
        {
            return BadRequest(Envelope.Fail("INVALID_ARGUMENT", "connectorId is required"));
        }

        if (_registry.GetConnector(request.ConnectorId) is null)
        {
            return BadRequest(Envelope.Fail(
                "UNKNOWN_CONNECTOR", $"No connector is registered with id '{request.ConnectorId}'"));
        }

        var id = Guid.NewGuid().ToString("N");
        var created = await _store.SaveAsync(
            id, request.ConnectorId, request.Name, request.Secrets, cancellationToken);

        // Log the id and connector, never the payload.
        _logger.LogInformation(
            "Created connection {ConnectionId} for connector {ConnectorId}", id, request.ConnectorId);

        return CreatedAtAction(nameof(GetConnection), new { id }, Envelope.Ok(created));
    }

    /// <summary>
    /// Update a connection. Omitting <c>secrets</c> renames without resubmitting
    /// credentials; supplying it REPLACES the stored set.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "CanManageWorkflows")]
    public async Task<IActionResult> UpdateConnection(
        string id, [FromBody] ConnectionUpdateRequest request, CancellationToken cancellationToken)
    {
        var existing = await _store.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Connection '{id}' was not found"));
        }

        var updated = await _store.SaveAsync(
            id,
            existing.ConnectorId,
            request.Name ?? existing.Name,
            request.Secrets,
            cancellationToken);

        _logger.LogInformation("Updated connection {ConnectionId}", id);
        return Ok(Envelope.Ok(updated));
    }

    /// <summary>Delete a connection and the secrets it holds.</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "CanManageWorkflows")]
    public async Task<IActionResult> DeleteConnection(string id, CancellationToken cancellationToken)
    {
        var removed = await _store.DeleteAsync(id, cancellationToken);
        if (!removed)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Connection '{id}' was not found"));
        }

        _logger.LogInformation("Deleted connection {ConnectionId}", id);
        return Ok(Envelope.Ok(message: "Connection deleted"));
    }

    /// <summary>
    /// Exercise a connection against its real service, server-side, so the
    /// secret is never sent back to the browser.
    /// </summary>
    [HttpPost("{id}/test")]
    [Authorize(Policy = "CanManageWorkflows")]
    public async Task<IActionResult> TestConnection(string id, CancellationToken cancellationToken)
    {
        var connection = await _store.GetAsync(id, cancellationToken);
        if (connection is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Connection '{id}' was not found"));
        }

        var connector = _registry.GetConnector(connection.ConnectorId);
        if (connector is null)
        {
            return BadRequest(Envelope.Fail(
                "UNKNOWN_CONNECTOR",
                $"No connector is registered with id '{connection.ConnectorId}'"));
        }

        var config = await _store.BuildConfigurationAsync(id, cancellationToken);
        if (config is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Connection '{id}' was not found"));
        }

        try
        {
            await connector.InitializeAsync(config, cancellationToken);
            var result = await connector.TestConnectionAsync(cancellationToken);

            return Ok(Envelope.Ok(new
            {
                success = result.Success,
                message = result.Message,
                responseTimeMs = (int)result.ResponseTime.TotalMilliseconds,
            }));
        }
        catch (Exception ex)
        {
            // Report that the test failed without echoing the exception's detail
            // into the response, since a connector message can quote the value
            // it was given.
            _logger.LogWarning(ex, "Connection test failed for {ConnectionId}", id);

            return Ok(Envelope.Ok(new
            {
                success = false,
                message = "Connection test failed. See server logs for details.",
                responseTimeMs = 0,
            }));
        }
    }
}

/// <summary>
/// Request body for creating a connection. Mirrors the editor's
/// ConnectionCreateRequest. <c>Secrets</c> is write-only by construction: no
/// response type on this controller carries it back.
/// </summary>
public class ConnectionCreateRequest
{
    public string ConnectorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Credential field name -> secret value. Sent, never returned.</summary>
    public Dictionary<string, string>? Secrets { get; set; }
}

/// <summary>
/// Request body for updating a connection. Omit <c>Secrets</c> to rename
/// without resubmitting credentials; supplying it replaces the whole set, so
/// "which fields are set" cannot become ambiguous.
/// </summary>
public class ConnectionUpdateRequest
{
    public string? Name { get; set; }
    public Dictionary<string, string>? Secrets { get; set; }
}
