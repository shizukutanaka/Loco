import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Guards against reading node fields nobody writes.
 *
 * This bug class has appeared four separate times in this codebase, and every
 * instance was invisible to both the compiler and the tests:
 *
 *   node.data.type   - read in 30 places, never written. "Missing Trigger Node"
 *                      fired on every workflow and the type-specific validation
 *                      rules never ran at all.
 *   config.integration / config.actionType
 *                    - validation read these while the editor writes
 *                      data.integration and config.action.
 *   config.code      - left behind when the transform node stopped offering a
 *                      C# editor.
 *   config.duration  - delay nodes write `seconds`; the bottleneck check and
 *                      the simulator both read a key that never existed.
 *
 * The shared shape is a read of a plausible-sounding field that silently
 * evaluates to undefined. A test cannot know every valid key, but it CAN refuse
 * the specific ones already proven wrong - which is what stops a regression
 * from being reintroduced by someone reading the old code as an example.
 */

const SRC = join(__dirname, '..');

/** Field reads that are known-wrong, with what to use instead. */
const FORBIDDEN: Array<{ pattern: RegExp; use: string; why: string }> = [
  {
    pattern: /\.data\.type\b/,
    use: 'node.type',
    why: 'React Flow owns the node type; nothing writes data.type',
  },
  {
    pattern: /config\??\.integration\b|config\['integration'\]/,
    use: 'node.data.integration',
    why: 'the canvas drop handler writes integration on data, not config',
  },
  {
    pattern: /config\??\.actionType\b|config\['actionType'\]/,
    use: "config.action",
    why: 'PropertyPanel writes config.action; actionType is written nowhere',
  },
  {
    pattern: /config\??\.code\b|config\['code'\]/,
    use: 'config.json',
    why: 'the transform node writes a JSON literal, not C# code',
  },
  {
    pattern: /config\??\.duration\b|config\['duration'\]/,
    use: 'config.seconds',
    why: "the delay node writes seconds, which is what the engine's handler reads",
  },
];

function sourceFiles(dir: string, found: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry === 'dist') continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      sourceFiles(full, found);
    } else if (/\.tsx?$/.test(entry) && !/\.test\.tsx?$/.test(entry)) {
      found.push(full);
    }
  }
  return found;
}

describe('node field contract', () => {
  const files = sourceFiles(SRC);

  it('finds the source tree (guards against a wrong path)', () => {
    expect(files.length).toBeGreaterThan(50);
  });

  it.each(FORBIDDEN)('no source reads $use\'s wrong spelling', ({ pattern, use, why }) => {
    const offenders: string[] = [];

    for (const file of files) {
      // Strip comments before scanning: the notes explaining each mistake name
      // the wrong spelling on purpose. Block comments are blanked line-by-line
      // so reported line numbers still match the file.
      const source = readFileSync(file, 'utf8')
        .replace(/\/\*[\s\S]*?\*\//g, (block) => block.replace(/[^\n]/g, ' '));

      source.split('\n').forEach((line, index) => {
        const code = line.replace(/\/\/.*$/, '');
        if (pattern.test(code)) {
          offenders.push(`${file.slice(SRC.length + 1)}:${index + 1}`);
        }
      });
    }

    expect(offenders, `${why}. Use ${use} instead.`).toEqual([]);
  });
});
