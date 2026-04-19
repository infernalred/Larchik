import { describe, expect, it } from 'vitest';
import { toDateInputValue, toUtcIso } from './date-input';

describe('date-input', () => {
  it('maps ISO values to date inputs', () => {
    expect(toDateInputValue()).toBe('');
    expect(toDateInputValue('2026-04-19T12:30:00.000Z')).toBe('2026-04-19');
  });

  it('normalizes date inputs to UTC ISO', () => {
    expect(toUtcIso()).toBeUndefined();
    expect(toUtcIso('2026-04-19')).toBe('2026-04-19T00:00:00.000Z');
    expect(toUtcIso('2026-04-19T12:30:00+03:00')).toBe('2026-04-19T09:30:00.000Z');
    expect(toUtcIso('bad-date')).toBeUndefined();
  });
});
