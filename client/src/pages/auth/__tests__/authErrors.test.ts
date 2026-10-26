import { describe, expect, it } from 'vitest';
import { nicknameProblems, planetNameProblems } from '../authErrors';

describe('visible name characters', () => {
  it('accepts the apostrophe in a planet name', () => {
    expect(planetNameProblems("Кам'яна")).toEqual([]);
    expect(planetNameProblems('Кам’яна')).toEqual([]);
  });

  it('refuses the apostrophe in a nickname', () => {
    expect(nicknameProblems("Кам'яна")).toEqual(['error.nickname_invalid_characters']);
    expect(nicknameProblems('Кам’яна')).toEqual(['error.nickname_invalid_characters']);
  });

  it('refuses other punctuation in a planet name', () => {
    expect(planetNameProblems('Кам"яна')).toEqual(['error.planet_name_invalid_characters']);
  });
});
