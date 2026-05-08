import { describe, it, expect } from 'vitest';
import { SECTION_META, SECTION_ORDER } from '@/editor/sectionMeta';

describe('section meta', () => {
  it('SECTION_ORDER covers all 8 thesis section kinds', () => {
    expect(SECTION_ORDER).toHaveLength(8);
    expect(new Set(SECTION_ORDER).size).toBe(8);
  });

  it('order positions cover before body, body before bibliography', () => {
    const coverPos = SECTION_ORDER.indexOf('cover');
    const bodyPos = SECTION_ORDER.indexOf('body');
    const bibPos = SECTION_ORDER.indexOf('bibliography');
    expect(coverPos).toBeLessThan(bodyPos);
    expect(bodyPos).toBeLessThan(bibPos);
  });

  it('cover and toc are not user-authored', () => {
    expect(SECTION_META.cover.authored).toBe(false);
    expect(SECTION_META.toc.authored).toBe(false);
    expect(SECTION_META.body.authored).toBe(true);
  });
});
