import { readFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import process from 'node:process';
import { describe, expect, it } from 'vitest';

/**
 * The sources of the screen of the civilization (task 00010): the new files of the task and
 * the style sheet it brings.
 */
const screenRoot = join(process.cwd(), 'src', 'pages', 'player');
const styleRoot = join(process.cwd(), 'src', 'styles');

/**
 * The files that the task 00010 brought or changed.
 */
const newFiles = [
  'GamePage.tsx',
  'CivilizationParts.tsx',
  'useBodies.ts',
  'useResourceNames.ts',
  'useResourceGroups.ts',
  'CivShell.tsx',
  'CivHeader.tsx',
  'CivHeaderParts.tsx',
  'CivMenu.tsx',
  'CivTabbar.tsx',
  'CivFooter.tsx',
  'civMenuItems.ts',
  'shellStore.ts',
  'useShell.ts',
  'ShellProvider.tsx',
];
const newStyles = ['civilization.css'];

/**
 * The colour a line of a style sheet writes by itself: a hex value or a function of colour. The
 * translucent surfaces are written with `color-mix` over the tokens, so a style sheet carries no
 * colour of its own.
 */
function colourIn(line: string): string | undefined {
  const code = line.replace(/\/\*.*?\*\//g, '');

  return (
    /#(?:[0-9a-f]{8}|[0-9a-f]{6}|[0-9a-f]{3,4})\b/i.exec(code)?.[0] ?? /\b(?:rgb|rgba|hsl|hsla|hwb|lab|lch|oklab|oklch)\(/i.exec(code)?.[0]
  );
}

describe('the screen of the civilization uses the tokens only (task 00010, acceptance criterion 3)', () => {
  it('finds a colour written in a style sheet and lets a mix of the tokens pass', () => {
    expect(colourIn('.a { background: rgba(7, 11, 22, 0.92); }')).toBe('rgba(');
    expect(colourIn('.a { color: #2EC4C4; }')).toBe('#2EC4C4');
    expect(colourIn('.a { background: color-mix(in srgb, var(--bg-0) 92%, transparent); }')).toBeUndefined();
    expect(colourIn('.a { color: var(--accent); } /* rgba(0, 0, 0, 0.5) in a comment */')).toBeUndefined();
  });

  it('has no hard-wired colour in the new style sheet', () => {
    const findings: string[] = [];

    for (const name of newStyles) {
      const file = join(styleRoot, name);

      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          const found = colourIn(line);

          if (found) {
            findings.push(`${relative(process.cwd(), file)}:${index + 1}: '${found}'`);
          }
        });
    }

    expect(findings).toEqual([]);
  });

  it('has no hard-wired colour in the new components', () => {
    const findings: string[] = [];

    for (const name of newFiles) {
      const file = join(screenRoot, name);

      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          const words = [...line.matchAll(/'([^']*)'|"([^"]*)"/g)].map((match) => match[1] ?? match[2] ?? '');
          const hex = words.find((word) => /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i.test(word));
          const rgb = words.find((word) => /^(?:rgb|rgba|hsl|hsla)\(/i.test(word));

          if (hex || rgb) {
            findings.push(`${relative(process.cwd(), file)}:${index + 1}: '${hex ?? rgb}'`);
          }
        });
    }

    expect(findings).toEqual([]);
  });

  it('has no hard-wired size of a font in the new style sheet', () => {
    const findings: string[] = [];

    for (const name of newStyles) {
      const file = join(styleRoot, name);

      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          // The size of a font is written with the property of the font; the tokens of the
          // project carry the sizes of the text.
          const sizes = [...line.matchAll(/font-size:\s*([^;]+);/g)].map((match) => match[1].trim());
          const hardWired = sizes.find((size) => !size.startsWith('var('));

          if (hardWired) {
            findings.push(`${relative(process.cwd(), file)}:${index + 1}: '${hardWired}'`);
          }
        });
    }

    expect(findings).toEqual([]);
  });

  it('names no game entity by an old code in the new files', () => {
    const codeMapPath = join(process.cwd(), '..', 'server', 'src', 'Aetheris.World', 'Data', 'neutral-code-map.json');
    const table = JSON.parse(readFileSync(codeMapPath, 'utf8')) as Record<string, Record<string, string>>;
    const codes = new Set(Object.values(table).flatMap((group) => Object.keys(group)).filter((code) => code !== 'gas_giant'));
    const findings: string[] = [];

    for (const name of newFiles) {
      const file = join(screenRoot, name);

      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          const words = [...line.matchAll(/'((?:[^'\\]|\\.)*)'|"((?:[^"\\]|\\.)*)"/g)]
            .map((match) => match[1] ?? match[2] ?? '')
            .filter((text) => !text.includes(' '))
            .flatMap((text) => text.split(/[^a-z0-9_]+/));
          const old = words.find((word) => codes.has(word));

          if (old) {
            findings.push(`${relative(process.cwd(), file)}:${index + 1}: '${old}'`);
          }
        });
    }

    expect(findings).toEqual([]);
  });
});
