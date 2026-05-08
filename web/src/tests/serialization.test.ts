import { describe, it, expect } from 'vitest';
import { inlinesToPMDoc, pmDocToInlines } from '@/editor/inlines';
import type { Inline } from '@/types';

describe('inline serialization with citations / references / hyperlinks', () => {
  it('round-trips citation', () => {
    const inlines: Inline[] = [
      { type: 'text', text: '研究表明 ' },
      { type: 'citation', targetId: 'smith2023', displayText: '[1]' },
      { type: 'text', text: ' 这一观点存在争议。' }
    ];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual(inlines);
  });

  it('round-trips reference with fallback', () => {
    const inlines: Inline[] = [
      { type: 'text', text: '见 ' },
      { type: 'reference', bookmarkName: 'fig-arch', fallbackText: '图 2-1' }
    ];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual(inlines);
  });

  it('round-trips hyperlink', () => {
    const inlines: Inline[] = [
      { type: 'text', text: '官方网站：' },
      { type: 'hyperlink', text: 'example.com', uri: 'https://example.com' }
    ];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual(inlines);
  });

  it('round-trips footnote and endnote chips without losing note content', () => {
    const inlines: Inline[] = [
      { type: 'text', text: '正文' },
      { type: 'footnote', noteId: 'fn-1', inlines: [{ type: 'text', text: '脚注内容' }] },
      { type: 'endnote', noteId: 'en-1', inlines: [{ type: 'text', text: '尾注内容' }] }
    ];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual(inlines);
  });

  it('preserves bold + italic combo', () => {
    const inlines: Inline[] = [{ type: 'text', text: 'mixed', bold: true, italic: true }];
    expect(pmDocToInlines(inlinesToPMDoc(inlines))).toEqual(inlines);
  });
});
