/**
 * Whether one outgoing edge should be followed after its source node ran,
 * mirroring the engine.
 *
 * The authority is Loco.Core's ConnectionRouter, which is what actually runs a
 * workflow. This exists because "Test Workflow" has to predict it, and it was
 * not predicting it at all: the simulator's edge filter read only the source
 * handle and never the edge's condition, so an edge marked 'error' was
 * followed after the node SUCCEEDED. A user marks a cleanup branch as the
 * error path, presses Test Workflow, and watches it run.
 *
 * The cases both sides must satisfy live in
 * `tests/shared/connection-routing-table.json`.
 *
 * Two independent things decide this and they are not the same: which HANDLE
 * the edge leaves from (a condition node draws 'true' and 'false'; everything
 * else has one unnamed output) and what the EdgeConditionPanel wrote on it.
 */

/** Edge conditions the engine understands. */
export const SUPPORTED_CONDITIONS = ['default', 'success', 'error', 'always'] as const;

/** Thrown for a routing decision the engine refuses to guess at. */
export class RoutingError extends Error {}

export function shouldFollowConnection(
  sourceOutput: string | null | undefined,
  condition: string | null | undefined,
  sourceSucceeded: boolean,
  verdict: boolean | null | undefined,
  sourceNodeName = ''
): boolean {
  // A named branch handle answers first: a false branch must not run just
  // because the node that evaluated the condition did not throw.
  if (sourceOutput === 'true' || sourceOutput === 'false') {
    if (verdict === null || verdict === undefined) {
      throw new RoutingError(
        `Node '${sourceNodeName}' has a '${sourceOutput}' branch edge but produced no ` +
          'condition verdict. Only a condition node has true/false outputs; connect ' +
          "this edge to the node's default output instead."
      );
    }

    if (verdict !== (sourceOutput === 'true')) return false;
  }

  if (condition === null || condition === undefined || condition === 'default' || condition === 'success') {
    return sourceSucceeded;
  }

  if (condition === 'error') return !sourceSucceeded;

  // A cleanup step that must run whether or not the step before it failed.
  if (condition === 'always') return true;

  // An expression the engine cannot evaluate. Returning true here would mean a
  // custom condition always fires - the one outcome that looks like it works
  // while ignoring what was written.
  throw new RoutingError(
    `Edge condition '${condition}' is not supported. Use 'success', 'error' or ` +
      "'always', or put the comparison in a condition node."
  );
}
