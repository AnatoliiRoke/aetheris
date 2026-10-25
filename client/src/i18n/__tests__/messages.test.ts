import { describe, expect, it } from 'vitest';
import { format } from '../context';
import { en } from '../en';
import { uk } from '../uk';

describe('messages', () => {
  it('has the same keys and parameters in both languages', () => {
    const parameters = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();

    expect(Object.keys(en).sort()).toEqual(Object.keys(uk).sort());

    for (const key of Object.keys(uk) as (keyof typeof uk)[]) {
      expect(parameters(en[key]), key).toEqual(parameters(uk[key]));
      expect(en[key].trim().length, key).toBeGreaterThan(0);
    }
  });

  it('fills the parameters', () => {
    expect(format('Кадр {index} з {total}', { index: 1, total: 4 })).toBe('Кадр 1 з 4');
    expect(format('{missing} stays', {})).toBe('{missing} stays');
  });
});
