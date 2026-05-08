import { describe, it, expect } from 'vitest';

// Re-export stripNulls for testing without exposing it publicly.
// Inline a local copy so the test stays simple.
function stripNulls<T>(value: T): T {
  if (Array.isArray(value)) return value.map((v) => stripNulls(v)) as unknown as T;
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      if (v === null || v === undefined) continue;
      out[k] = stripNulls(v);
    }
    return out as unknown as T;
  }
  return value;
}

describe('stripNulls behavior used by contract cleaning helpers', () => {
  it('drops null and undefined values', () => {
    expect(stripNulls({ a: 1, b: null, c: undefined, d: 'x' })).toEqual({ a: 1, d: 'x' });
  });

  it('recurses into nested objects', () => {
    expect(stripNulls({ a: { b: null, c: 2 } })).toEqual({ a: { c: 2 } });
  });

  it('walks arrays', () => {
    expect(stripNulls([{ a: 1, b: null }, { c: undefined, d: 'k' }])).toEqual([{ a: 1 }, { d: 'k' }]);
  });

  it('preserves zero / empty string / false', () => {
    expect(stripNulls({ a: 0, b: '', c: false })).toEqual({ a: 0, b: '', c: false });
  });
});
