import { useEffect, useRef, useState } from 'react';
import katex from 'katex';
import 'katex/dist/katex.min.css';
import { useEditorActions } from '../EditorContext';
import { BlockShell } from './BlockShell';
import type { EquationBlock as EquationBlockData } from '@/types';
import styles from './blocks.module.css';
import eqStyles from './EquationBlock.module.css';

interface Props {
  block: EquationBlockData;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

type SourceMode = 'latex' | 'omml' | 'plain';

export function EquationBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
  const actions = useEditorActions();
  const previewRef = useRef<HTMLDivElement>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);

  const mode: SourceMode = block.sourceType ?? (block.latex ? 'latex' : block.omml ? 'omml' : 'plain');

  useEffect(() => {
    if (!previewRef.current || mode !== 'latex' || !block.latex) {
      setPreviewError(null);
      if (previewRef.current) previewRef.current.innerHTML = '';
      return;
    }
    try {
      katex.render(block.latex, previewRef.current, {
        throwOnError: true,
        displayMode: block.display ?? true
      });
      setPreviewError(null);
    } catch (e) {
      setPreviewError(e instanceof Error ? e.message : 'LaTeX 解析失败');
    }
  }, [block.latex, block.display, mode]);

  const update = (updater: (b: EquationBlockData) => void) =>
    actions.updateBlock(sectionIndex, blockIndex, (b) => {
      if (b.type !== 'equation') return;
      updater(b);
    });

  const setMode = (next: SourceMode) =>
    update((b) => {
      b.sourceType = next;
      // Clear other source fields to keep schema oneOf clean
      if (next !== 'latex') delete b.latex;
      if (next !== 'omml') delete b.omml;
      if (next !== 'plain') delete b.plainText;
    });

  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge="公式"
      toolbar={
        <>
          <label className={styles.toolbarCheck}>
            <input
              type="checkbox"
              checked={block.display ?? true}
              onChange={(e) => update((b) => (b.display = e.target.checked))}
            />
            独立行
          </label>
          <span className={styles.toolbarSeparator}>·</span>
          <label className={styles.toolbarCheck}>
            <input
              type="checkbox"
              checked={block.numbering?.enabled ?? false}
              onChange={(e) =>
                update((b) => {
                  if (e.target.checked) {
                    b.numbering = b.numbering ?? { enabled: true, format: '({index})' };
                    b.numbering.enabled = true;
                  } else {
                    delete b.numbering;
                  }
                })
              }
            />
            自动编号
          </label>
          {block.numbering?.enabled && (
            <input
              type="text"
              className={eqStyles.formatInput}
              value={block.numbering.format ?? '({index})'}
              placeholder="({index})"
              onChange={(e) =>
                update((b) => {
                  if (!b.numbering) return;
                  b.numbering.format = e.target.value;
                })
              }
              title="编号格式，必须包含 {index}"
            />
          )}
        </>
      }
    >
      <div className={eqStyles.eq}>
        <div className={eqStyles.tabs}>
          {(['latex', 'omml', 'plain'] as SourceMode[]).map((m) => (
            <button
              key={m}
              type="button"
              className={eqStyles.tab}
              data-active={mode === m}
              onClick={() => setMode(m)}
            >
              {m === 'latex' ? 'LaTeX' : m === 'omml' ? 'OMML' : '纯文本'}
            </button>
          ))}
        </div>

        <div className={eqStyles.body}>
          {mode === 'latex' && (
            <div className={eqStyles.latexPane}>
              <textarea
                className={eqStyles.codeInput}
                placeholder="\\frac{a}{b} = c"
                value={block.latex ?? ''}
                rows={4}
                onChange={(e) => update((b) => (b.latex = e.target.value))}
                spellCheck={false}
              />
              <div className={eqStyles.preview}>
                {!block.latex && <span className={eqStyles.previewHint}>预览</span>}
                <div ref={previewRef} className={eqStyles.previewBody} />
                {previewError && <div className={eqStyles.previewError}>{previewError}</div>}
              </div>
            </div>
          )}
          {mode === 'omml' && (
            <textarea
              className={eqStyles.codeInput}
              placeholder='<m:oMath xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math">…</m:oMath>'
              value={block.omml ?? ''}
              rows={6}
              onChange={(e) => update((b) => (b.omml = e.target.value))}
              spellCheck={false}
            />
          )}
          {mode === 'plain' && (
            <input
              type="text"
              className={eqStyles.codeInput}
              placeholder="a + b = c"
              value={block.plainText ?? ''}
              onChange={(e) => update((b) => (b.plainText = e.target.value))}
            />
          )}
        </div>

        <div className={eqStyles.captionRow}>
          <span className={eqStyles.captionLabel}>题注</span>
          <input
            className={eqStyles.captionInput}
            placeholder="（可选）公式说明"
            value={block.caption ?? ''}
            onChange={(e) =>
              update((b) => {
                if (e.target.value) b.caption = e.target.value;
                else delete b.caption;
              })
            }
          />
        </div>
      </div>
    </BlockShell>
  );
}
