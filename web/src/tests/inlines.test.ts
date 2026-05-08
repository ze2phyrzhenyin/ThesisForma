import { describe, it, expect } from 'vitest';
import { inlinesToPMDoc, pmDocToInlines } from '@/editor/inlines';
import type { Inline } from '@/types';

describe('inline serialization', () => {
  it('round-trips plain text', () => {
    const inlines: Inline[] = [{ type: 'text', text: 'Hello 世界' }];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual(inlines);
  });

  it('round-trips bold + italic', () => {
    const inlines: Inline[] = [
      { type: 'text', text: 'a ' },
      { type: 'text', text: 'bold', bold: true },
      { type: 'text', text: ' and ' },
      { type: 'text', text: 'italic', italic: true }
    ];
    const out = pmDocToInlines(inlinesToPMDoc(inlines));
    expect(out).toEqual(inlines);
  });

  it('drops empty text runs', () => {
    const inlines: Inline[] = [{ type: 'text', text: '' }];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual([]);
  });

  it('underline mark survives', () => {
    const inlines: Inline[] = [{ type: 'text', text: 'u', underline: true }];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual(inlines);
  });
});
