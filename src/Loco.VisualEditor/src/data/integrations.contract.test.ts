import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { integrations } from './integrations';

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

  it('google-sheets specifically resolves (the regression that motivated this test)', () => {
    const sheets = integrations.find((i) => i.id === 'google-sheets');
    expect(sheets, "palette must use the connector's id, not 'googlesheets'").toBeDefined();
    expect(connectorIds.has('google-sheets')).toBe(true);
    expect(readActionIds('google-sheets').has('appendValues')).toBe(true);
  });
});
