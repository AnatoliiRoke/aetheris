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
const newFiles = ['GamePage.tsx', 'CivilizationParts.tsx', 'useBodies.ts', 'useResourceNames.ts', 'useResourceGroups.ts'];
const newStyles = ['civilization.css'];

/**
 * The words of the colours and the sizes of a line of a style sheet: the custom properties,
 * the class names and the values, the comments aside.
 */
function styleWords(line: string): string[] {
  return line
    .replace(/\/\*.*?\*\//g, '')
    .split(/[^a-z0-9-]+/)
    .filter((word) => word.length > 0);
}

describe('the screen of the civilization uses the tokens only (task 00010, acceptance criterion 3)', () => {
  it('has no hard-wired colour in the new style sheet', () => {
    const findings: string[] = [];

    for (const name of newStyles) {
      const file = join(styleRoot, name);

      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          const words = styleWords(line);
          const hex = words.find((word) => /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i.test(word));
          const rgb = words.find((word) => /^(?:rgb|rgba|hsl|hsla)\d+$/i.test(word));

          if (hex || rgb) {
            findings.push(`${relative(process.cwd(), file)}:${index + 1}: '${hex ?? rgb}'`);
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
