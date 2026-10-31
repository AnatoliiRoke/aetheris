import { describe, expect, it } from 'vitest';

/**
 * The source files of the screens of the player: the components and the hooks, without the
 * tests, which live one folder deeper.
 */
const sourceFiles = Object.entries(import.meta.glob<string>('../*.{ts,tsx}', { query: '?raw', import: 'default', eager: true })).map(([path, text]) => ({
  name: path.replace('../', ''),
  text,
}));

/**
 * The comments are not the code: a colour in a comment is not a style.
 */
const withoutComments = (text: string) => text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');

describe('the screens of the game use only the tokens (task 00020, ui-art/05)', () => {
  it('finds the source files of the screens', () => {
    expect(sourceFiles.length).toBeGreaterThan(20);
    expect(sourceFiles.map((file) => file.name)).toEqual(expect.arrayContaining(['PlanetPage.tsx', 'BuildPage.tsx', 'ResearchPage.tsx', 'StoragePage.tsx', 'CivShell.tsx']));
  });

  it('has no hex colour and no rgb colour in the components', () => {
    const found = sourceFiles.filter((file) => /#[0-9a-fA-F]{3,8}\b/.test(withoutComments(file.text)) || /\brgba?\(/.test(withoutComments(file.text)));

    expect(found.map((file) => file.name)).toEqual([]);
  });

  it('has no size of a font in the components', () => {
    const found = sourceFiles.filter((file) => /font-?size/i.test(withoutComments(file.text)) || /fontFamily|font-family/.test(withoutComments(file.text)));

    expect(found.map((file) => file.name)).toEqual([]);
  });

  it('has no visible text written in the components', () => {
    // Every visible text comes from a key of the dictionary, so a Cyrillic letter in a component is a hard-coded string.
    const found = sourceFiles.filter((file) => /[А-Яа-яІіЇїЄєҐґ]/.test(withoutComments(file.text)));

    expect(found.map((file) => file.name)).toEqual([]);
  });
});
