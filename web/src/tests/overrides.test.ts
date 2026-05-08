import { afterEach, describe, it, expect } from 'vitest';
import {
  clearOverrides,
  loadOverrides,
  saveOverrides,
  SECTION_BUCKET_FOR_KIND
} from '@/editor/overrides';

const DOC_ID = 'doc-test-overrides';

afterEach(() => {
  clearOverrides(DOC_ID);
});

describe('document overrides', () => {
  it('round-trips through localStorage', () => {
    saveOverrides(DOC_ID, {
      toc: { minLevel: 1, maxLevel: 3 },
      headerFooter: { headerText: '论文' },
      sectionFormats: { body: { pageNumberStyle: 'decimal', startPageNumber: 1 } }
    });
    const loaded = loadOverrides(DOC_ID);
    expect(loaded.toc?.maxLevel).toBe(3);
    expect(loaded.headerFooter?.headerText).toBe('论文');
    expect(loaded.sectionFormats?.body?.pageNumberStyle).toBe('decimal');
  });

  it('strips empty branches so an empty override clears the entry', () => {
    saveOverrides(DOC_ID, { toc: {}, headerFooter: { headerText: '' } });
    expect(loadOverrides(DOC_ID)).toEqual({});
  });

  it('clearOverrides wipes the entry', () => {
    saveOverrides(DOC_ID, { toc: { minLevel: 2 } });
    expect(loadOverrides(DOC_ID).toc?.minLevel).toBe(2);
    clearOverrides(DOC_ID);
    expect(loadOverrides(DOC_ID)).toEqual({});
  });

  it('maps each section kind into a bucket', () => {
    expect(SECTION_BUCKET_FOR_KIND.cover).toBe('cover');
    expect(SECTION_BUCKET_FOR_KIND.abstract).toBe('frontMatter');
    expect(SECTION_BUCKET_FOR_KIND.toc).toBe('frontMatter');
    expect(SECTION_BUCKET_FOR_KIND.body).toBe('body');
    expect(SECTION_BUCKET_FOR_KIND.bibliography).toBe('body');
  });

  it('round-trips defaultFont / bodyParagraph / headings', () => {
    saveOverrides(DOC_ID, {
      defaultFont: { eastAsia: '宋体', latin: 'Times New Roman', sizePt: 12 },
      bodyParagraph: {
        lineSpacingMultiple: 1.5,
        firstLineIndentChars: 2,
        spaceBeforePt: 0,
        spaceAfterPt: 0,
        alignment: 'both',
        widowControl: true
      },
      headings: {
        '1': {
          font: { eastAsia: '黑体', sizePt: 22, bold: true },
          alignment: 'center',
          pageBreakBefore: true,
          numbered: true
        },
        '2': { font: { sizePt: 16 } }
      }
    });
    const loaded = loadOverrides(DOC_ID);
    expect(loaded.defaultFont?.eastAsia).toBe('宋体');
    expect(loaded.bodyParagraph?.lineSpacingMultiple).toBe(1.5);
    expect(loaded.bodyParagraph?.firstLineIndentChars).toBe(2);
    expect(loaded.headings?.['1']?.alignment).toBe('center');
    expect(loaded.headings?.['1']?.font?.sizePt).toBe(22);
    expect(loaded.headings?.['2']?.font?.sizePt).toBe(16);
  });

  it('round-trips toc.includeSectionIds and treats empty array as cleared', () => {
    saveOverrides(DOC_ID, {
      toc: { minLevel: 1, maxLevel: 3, includeSectionIds: ['abstract', 'body'] }
    });
    expect(loadOverrides(DOC_ID).toc?.includeSectionIds).toEqual(['abstract', 'body']);

    // empty array is stripped (treated as "no override")
    saveOverrides(DOC_ID, { toc: { includeSectionIds: [] } });
    expect(loadOverrides(DOC_ID)).toEqual({});
  });

  it('round-trips sectionInstances with header/footer text', () => {
    saveOverrides(DOC_ID, {
      sectionInstances: {
        body: {
          headerText: '论文标题',
          footerText: '第 PAGE 页',
          pageNumberStyle: 'decimal',
          startPageNumber: 1,
          paragraph: { lineSpacingMultiple: 1.75, firstLineIndentChars: 2 }
        }
      }
    });
    const loaded = loadOverrides(DOC_ID);
    expect(loaded.sectionInstances?.body?.headerText).toBe('论文标题');
    expect(loaded.sectionInstances?.body?.paragraph?.lineSpacingMultiple).toBe(1.75);
    expect(loaded.sectionInstances?.body?.startPageNumber).toBe(1);
  });
});
