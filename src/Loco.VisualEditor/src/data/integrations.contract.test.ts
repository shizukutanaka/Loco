import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { integrations } from './integrations';
import { templates } from './templates';

/**
 * Cross-stack contract test.
 *
 * The engine resolves a node to its handler by the composite key
 * `${node.Integration}:${node.Action}` (VisualWorkflowEngine.ExecuteNodeAsync),
 * and WorkflowConnectorBridge registers handlers as
 * `${connectorId}:${action.Id}`. So a palette entry whose id or action id does
 * not match the C# connector EXACTLY produces a node that looks fine in the
 * editor and fails at execution with "no handler registered".
 *
 * That already happened once: the palette said 'googlesheets'/'appendRow' while
 * the connector declares 'google-sheets'/'appendValues', making a fully
 * implemented connector unreachable. This test reads the real connector sources
 * so the two sides cannot drift apart silently again.
 *
 * It deliberately does NOT require the palette to cover every connector - that
 * gap is real (most connectors are still missing from the palette) but it is
 * tracked as work, not a regression. What it forbids is the palette pointing at
 * something that does not exist.
 */

const CONNECTOR_DIR = join(__dirname, '../../../Loco.Core/Integrations/Connectors');

/**
 * Node types the engine handles itself via RegisterDefaultHandlers(), looked up
 * by node TYPE rather than integration:action. These legitimately have no
 * connector behind them.
 */
const ENGINE_BUILTINS = new Set(['transform', 'condition', 'delay', 'loop', 'variable']);

/**
 * Engine built-ins split into two dispatch styles, and the distinction decides
 * whether an entry belongs in the palette list at all:
 *  - dispatched by node TYPE (transform/condition/delay/loop) - these must NOT
 *    appear in integrations.ts, or a drag would produce integration=X,
 *    type='action' and a handler key "X:<action>" registered nowhere.
 *  - dispatched by `${integration}:${action}` (variable:set / variable:get) -
 *    these belong in integrations.ts like any connector.
 */
const TYPE_DISPATCHED_BUILTINS = new Set(['transform', 'condition', 'delay', 'loop']);

/**
 * Palette entries that are not connector-backed and not engine built-ins.
 * Each one is a node a user can drag onto the canvas that cannot execute today.
 * Listed explicitly so the count cannot grow unnoticed; removing an entry from
 * here (by implementing or deleting it) is the goal.
 */
const KNOWN_UNBACKED = new Set([
  'database', // generic; the concrete connectors are mysql/postgresql/mongodb
  'ftp', // no connector exists
  'telegram', // no connector exists
  'webhook', // trigger surface; trigger wiring itself is unimplemented (O-7)
]);

function readConnectorIds(): Set<string> {
  const ids = new Set<string>();
  for (const file of readdirSync(CONNECTOR_DIR)) {
    if (!file.endsWith('.cs')) continue;
    const source = readFileSync(join(CONNECTOR_DIR, file), 'utf8');
    // e.g. public override string Id => "google-sheets";
    const match = source.match(/public\s+override\s+string\s+Id\s*=>\s*"([^"]+)"/);
    if (match) ids.add(match[1]);
  }
  return ids;
}

function readActionIds(connectorId: string): Set<string> {
  const actions = new Set<string>();
  for (const file of readdirSync(CONNECTOR_DIR)) {
    if (!file.endsWith('.cs')) continue;
    const source = readFileSync(join(CONNECTOR_DIR, file), 'utf8');
    const idMatch = source.match(/public\s+override\s+string\s+Id\s*=>\s*"([^"]+)"/);
    if (idMatch?.[1] !== connectorId) continue;

    // e.g. Id = "appendValues",
    for (const m of source.matchAll(/\bId\s*=\s*"([A-Za-z][A-Za-z0-9_]*)"/g)) {
      actions.add(m[1]);
    }
  }
  return actions;
}

/**
 * Parameter names an action declares in its C# `Parameters = [...]` block.
 * Returns null when the block cannot be located, so a parsing gap reads as
 * "unknown" rather than as a mismatch.
 */
function readDeclaredParams(connectorId: string, actionId: string): Set<string> | null {
  for (const file of readdirSync(CONNECTOR_DIR)) {
    if (!file.endsWith('.cs')) continue;
    const source = readFileSync(join(CONNECTOR_DIR, file), 'utf8');
    const idMatch = source.match(/public\s+override\s+string\s+Id\s*=>\s*"([^"]+)"/);
    if (idMatch?.[1] !== connectorId) continue;

    // From this action's Id to the start of the next `new()` entry (or the end
    // of the Actions list).
    const start = source.search(new RegExp(`\\bId\\s*=\\s*"${actionId}"`));
    if (start === -1) return null;

    const rest = source.slice(start);
    const end = rest.search(/\n\s*new\(\)\s*\n\s*\{|\n\s*\];/);
    const block = end === -1 ? rest : rest.slice(0, end);

    const paramsAssignment = block.match(/Parameters\s*=\s*([A-Za-z_][A-Za-z0-9_]*)\s*\(\s*\)/);
    if (paramsAssignment) {
      // Shared helper, e.g. `Parameters = GetRequestParameters()` in
      // HttpConnector. Resolve the helper's body instead of the action block.
      const helper = paramsAssignment[1];
      const helperStart = source.search(
        new RegExp(`\\b${helper}\\s*\\(\\s*\\)\\s*(=>|\\n?\\s*\\{)`)
      );
      if (helperStart === -1) return null;
      const helperBody = source.slice(helperStart, helperStart + 4000);
      return new Set(
        [...helperBody.matchAll(/\bName\s*=\s*"([A-Za-z][A-Za-z0-9_]*)"/g)].map((m) => m[1])
      );
    }

    if (!/Parameters\s*=/.test(block)) return new Set();
    return new Set([...block.matchAll(/\bName\s*=\s*"([A-Za-z][A-Za-z0-9_]*)"/g)].map((m) => m[1]));
  }
  return null;
}

describe('integrations palette <-> connector contract', () => {
  const connectorIds = readConnectorIds();

  it('finds the C# connector sources (guards against a wrong path)', () => {
    expect(connectorIds.size).toBeGreaterThan(20);
    expect(connectorIds.has('slack')).toBe(true);
  });

  it('every palette integration is backed by a connector, an engine built-in, or a known gap', () => {
    const unbacked = integrations
      .map((i) => i.id)
      .filter(
        (id) => !connectorIds.has(id) && !ENGINE_BUILTINS.has(id) && !KNOWN_UNBACKED.has(id)
      );

    expect(unbacked).toEqual([]);
  });

  it('type-dispatched built-ins are not listed as integrations', () => {
    // A palette entry for one of these would be dragged as type='action' with
    // integration=<id>, producing a handler key like "transform:execute" that
    // the engine never registers - the node would fail at execution. There used
    // to be exactly such a 'transform' entry.
    const wrongly = integrations
      .map((i) => i.id)
      .filter((id) => TYPE_DISPATCHED_BUILTINS.has(id));

    expect(wrongly).toEqual([]);
  });

  it('the known-unbacked list has not grown', () => {
    // If this fails, a palette entry was added with no connector behind it.
    const actuallyUnbacked = integrations
      .map((i) => i.id)
      .filter((id) => !connectorIds.has(id) && !ENGINE_BUILTINS.has(id));

    expect(new Set(actuallyUnbacked)).toEqual(KNOWN_UNBACKED);
  });

  it('connector-backed integrations use action ids the connector actually declares', () => {
    const mismatches: string[] = [];

    for (const integration of integrations) {
      if (!connectorIds.has(integration.id)) continue;

      const declared = readActionIds(integration.id);
      for (const action of integration.actions ?? []) {
        if (!declared.has(action.id)) {
          mismatches.push(`${integration.id}:${action.id}`);
        }
      }
    }

    expect(mismatches).toEqual([]);
  });

  it('palette parameter names are all declared by the connector action', () => {
    // Action ids were the first mismatch found; parameter NAMES are the same
    // class of bug one level down. A palette parameter the connector never
    // declares is silently dropped - the user fills in a field that reaches
    // nothing. This caught six of them: slack/github 'token', stripe 'apiKey',
    // email 'from', sendgrid 'body' (it reads html/text), redis 'ttl' (it reads
    // expirySeconds). The first four were credentials, which belong to a
    // connection, not to node config.
    //
    // Compares against the connector's DECLARED parameters rather than the keys
    // its code reads: declarations are the contract, and reads can hide behind
    // helpers (HttpConnector applies 'headers' via AddHeaders(), so a
    // reads-based check would report a false positive there).
    const mismatches: string[] = [];

    for (const integration of integrations) {
      if (!connectorIds.has(integration.id)) continue;

      for (const action of integration.actions ?? []) {
        const declared = readDeclaredParams(integration.id, action.id);
        // Skip when the action's parameter block could not be located, so a
        // parsing gap cannot masquerade as a finding.
        if (declared === null) continue;

        for (const param of action.parameters ?? []) {
          if (!declared.has(param.name)) {
            mismatches.push(`${integration.id}:${action.id} -> ${param.name}`);
          }
        }
      }
    }

    expect(mismatches).toEqual([]);
  });

  it('every template node resolves to a real integration and action', () => {
    // Templates are the first thing a new user runs, so a template node that
    // cannot execute is the worst version of this bug class. Two were broken:
    // sendgrid used action 'send' (the connector declares 'sendEmail') and five
    // nodes used the connector-less 'database' integration.
    const bad: string[] = [];

    for (const template of templates) {
      for (const node of template.workflow.nodes ?? []) {
        const integrationId = node.data?.integration as string | undefined;
        const actionId = (node.data?.config as Record<string, unknown> | undefined)?.action as
          | string
          | undefined;

        // Type-dispatched nodes (trigger/transform/condition/delay/loop) resolve
        // by node type, so they need no integration:action pair.
        if (!integrationId || node.type !== 'action') continue;

        const integration = integrations.find((i) => i.id === integrationId);
        if (!integration) {
          bad.push(`${template.id}: unknown integration '${integrationId}'`);
          continue;
        }
        if (actionId && !integration.actions?.some((a) => a.id === actionId)) {
          bad.push(`${template.id}: '${integrationId}' has no action '${actionId}'`);
        }
      }
    }

    expect(bad).toEqual([]);
  });

  it('template node parameters are names the connector declares', () => {
    // Action ids in templates were checked above; the parameter NAMES are the
    // same class of bug one level down, and the palette had six of them.
    const bad: string[] = [];

    for (const template of templates) {
      for (const node of template.workflow.nodes ?? []) {
        if (node.type !== 'action') continue;

        const integrationId = node.data?.integration as string | undefined;
        const config = node.data?.config as Record<string, unknown> | undefined;
        const actionId = config?.action as string | undefined;
        const params = config?.parameters as Record<string, unknown> | undefined;

        if (!integrationId || !actionId || !params) continue;
        if (!connectorIds.has(integrationId)) continue;

        const declared = readDeclaredParams(integrationId, actionId);
        if (declared === null || declared.size === 0) continue;

        for (const name of Object.keys(params)) {
          if (!declared.has(name)) {
            bad.push(`${template.id}: ${integrationId}:${actionId} -> ${name}`);
          }
        }
      }
    }

    expect(bad).toEqual([]);
  });

  it('google-sheets specifically resolves (the regression that motivated this test)', () => {
    const sheets = integrations.find((i) => i.id === 'google-sheets');
    expect(sheets, "palette must use the connector's id, not 'googlesheets'").toBeDefined();
    expect(connectorIds.has('google-sheets')).toBe(true);
    expect(readActionIds('google-sheets').has('appendValues')).toBe(true);
  });
});
