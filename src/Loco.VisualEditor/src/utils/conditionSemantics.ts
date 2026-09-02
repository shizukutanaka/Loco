/**
 * The condition node's comparison semantics, mirroring the engine.
 *
 * The authority is Loco.Core's ConditionEvaluator, which is what actually runs
 * a workflow. This exists because "Test Workflow" has to predict that, and it
 * was not predicting it: the simulator returned false where the engine failed
 * the node, so the tester reported a green run for a workflow that would die.
 *
 * The two are separate implementations in separate languages, so the guard
 * against drift is not shared code but a shared table -
 * `tests/shared/condition-truth-table.json` - which both test suites read and
 * both implementations must satisfy.
 *
 * The numeric rule is deliberately stricter than `Number()`. JavaScript reads
 * '' as 0 and '0x10' as 16; .NET accepts '1,000' and 'Infinity'. Taking either
 * host's default would guarantee the two disagree.
 */

/** Operations the engine implements. Anything else evaluates to false. */
export const SUPPORTED_OPERATIONS = [
  'equals',
  'not_equals',
  'greater_than',
  'less_than',
  'contains',
] as const;

/** Thrown for an ordering comparison whose operands are not both numbers. */
export class ConditionError extends Error {}

/** ^[+-]?(digits[.digits] | .digits)([eE][+-]?digits)?$ */
const NUMERIC_LITERAL = /^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$/;

/**
 * The value's text form. Booleans render lowercase to agree with the C# side,
 * whose default `ToString()` would produce "True".
 */
function asText(value: unknown): string | null {
  if (value === null || value === undefined) return null;
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (typeof value === 'string') return value;
  return String(value);
}

/** The value as a finite number, or null when it is not one. */
function asNumber(value: unknown): number | null {
  if (value === null || value === undefined || typeof value === 'boolean') return null;
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;

  const text = asText(value)?.trim();
  if (!text || !NUMERIC_LITERAL.test(text)) return null;

  const parsed = Number(text);
  return Number.isFinite(parsed) ? parsed : null;
}

function describe(value: unknown): string {
  return value === null || value === undefined
    ? 'an unresolved value'
    : `'${asText(value)}'`;
}

function areEqual(left: unknown, right: unknown): boolean {
  const leftNumber = asNumber(left);
  const rightNumber = asNumber(right);

  if (leftNumber !== null && rightNumber !== null) return leftNumber === rightNumber;

  return asText(left) === asText(right);
}

/**
 * Evaluates one comparison.
 *
 * @throws {ConditionError} for an ordering comparison on non-numbers. The
 * engine fails the node in that case, so predicting `false` here would be the
 * simulator telling the user a run will succeed when it will not.
 */
export function compare(
  left: unknown,
  operation: string,
  right: unknown,
  nodeName = ''
): boolean {
  if (!(SUPPORTED_OPERATIONS as readonly string[]).includes(operation)) return false;

  switch (operation) {
    case 'equals':
      return areEqual(left, right);

    case 'not_equals':
      return !areEqual(left, right);

    case 'contains':
      return (asText(left) ?? '').includes(asText(right) ?? '');

    case 'greater_than':
    case 'less_than': {
      const leftNumber = asNumber(left);
      const rightNumber = asNumber(right);

      if (leftNumber === null || rightNumber === null) {
        const subject = nodeName ? `Condition '${nodeName}'` : 'Condition';
        throw new ConditionError(
          `${subject} cannot use '${operation}' on ${describe(left)} and ${describe(right)}: ` +
            "both sides must be numbers. Use 'equals' or 'contains' to compare text, or " +
            'check that the reference on each side resolves to a value.'
        );
      }

      return operation === 'greater_than'
        ? leftNumber > rightNumber
        : leftNumber < rightNumber;
    }

    default:
      return false;
  }
}
