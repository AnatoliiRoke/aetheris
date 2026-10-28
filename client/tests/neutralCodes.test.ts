import { readdirSync, readFileSync } from 'node:fs';
import { extname, join, relative } from 'node:path';
import process from 'node:process';
import { describe, expect, it } from 'vitest';

/**
 * The table of the old codes of the game entities and their neutral codes, kept by the server
 * with the migrations that applied it (task 00022).
 */
const codeMapPath = join(process.cwd(), '..', 'server', 'src', 'Aetheris.World', 'Data', 'neutral-code-map.json');

/**
 * The sources of the client; the tests run from the folder of the client.
 */
const sourceRoot = join(process.cwd(), 'src');

/**
 * The old codes that are also logical codes and stay: the class of the gas giants is implemented
 * by the code, its old type had the same word.
 */
const logicalCodes = new Set(['gas_giant']);

/**
 * The words of sizes in the style sheets (`--fs-small`): the size classes of the bodies had the
 * same words, but a style sheet only sizes text and boxes, so they are not looked for there.
 */
const styleSizeWords = new Set(['small', 'medium', 'large']);

const scriptExtensions = new Set(['.ts', '.tsx']);
const styleExtensions = new Set(['.css']);

function oldCodes(): Set<string> {
  const table = JSON.parse(readFileSync(codeMapPath, 'utf8')) as Record<string, Record<string, string>>;

  return new Set(Object.values(table).flatMap((group) => Object.keys(group)).filter((code) => !logicalCodes.has(code)));
}

function files(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory() ? files(join(directory, entry.name)) : [join(directory, entry.name)],
  );
}

/**
 * The words of the strings of a line of a script. A string with a space is a text for people and
 * the holes of a template are code, so both are skipped.
 */
function scriptWords(line: string): string[] {
  return [...line.matchAll(/'((?:[^'\\]|\\.)*)'|"((?:[^"\\]|\\.)*)"|`((?:[^`\\]|\\.)*)`/g)]
    .map((match) => (match[1] ?? match[2] ?? match[3] ?? '').replace(/\$\{[^}]*\}/g, ''))
    .filter((text) => !text.includes(' '))
    .flatMap((text) => text.split(/[^a-z0-9_]+/));
}

/**
 * The words of a line of a style sheet: class names and custom properties, comments aside.
 */
function styleWords(line: string): string[] {
  return line
    .replace(/\/\*.*?\*\//g, '')
    .split(/[^a-z0-9_]+/)
    .filter((word) => !styleSizeWords.has(word));
}

function findOldCodes(codes: Set<string>): string[] {
  const root = sourceRoot;

  return files(root).flatMap((file) => {
    const extension = extname(file);
    const words = scriptExtensions.has(extension) ? scriptWords : styleExtensions.has(extension) ? styleWords : null;

    if (!words) {
      return [];
    }

    return readFileSync(file, 'utf8')
      .split('\n')
      .flatMap((line, index) => words(line).filter((word) => codes.has(word)).map((word) => `${relative(root, file)}:${index + 1}: '${word}'`));
  });
}

describe('the client names no game entity by an old code (task 00022, acceptance criterion 5)', () => {
  it('finds no old code in the sources, the styles or the tests', () => {
    expect(findOldCodes(oldCodes())).toEqual([]);
  });

  it('finds an old code inside a key, a class or a file name', () => {
    const codes = oldCodes();

    expect(scriptWords("const key = 'races.terran.name';").filter((word) => codes.has(word))).toEqual(['terran']);
    expect(scriptWords("import ship from './images/ship-kvarr.webp';").filter((word) => codes.has(word))).toEqual(['kvarr']);
    expect(styleWords('.race-card--nexar { color: var(--race-nexar); }').filter((word) => codes.has(word))).toEqual(['nexar', 'nexar']);
    expect(scriptWords("const text = 'Iron and ice in a sentence';").filter((word) => codes.has(word))).toEqual([]);
  });
});
