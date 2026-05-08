import { useEditorActions, useEditorStore } from '../EditorContext';
import { SECTION_META } from '../sectionMeta';
import { BlockRenderer } from '../blocks/BlockRenderer';
import { InsertBlockMenu } from '../blocks/InsertBlockMenu';
import { MetadataPanel } from '../panels/MetadataPanel';
import { VariablesPanel } from '../panels/VariablesPanel';
import { OverridesPanel } from '../panels/OverridesPanel';
import { blockFactoryFor } from '../store';
import type { BlockType } from '@/types';
import styles from './Canvas.module.css';

const PHASE_1_ENABLED = new Set<BlockType>([
  'paragraph',
  'heading',
  'list',
  'quote',
  'figure',
  'table',
  'equation',
  'pageBreak',
  'sectionBreak'
]);

export function Canvas() {
  const view = useEditorStore((s) => s.view);
  const sections = useEditorStore((s) => s.envelope.document.sections);
  const selected = useEditorStore((s) => s.selectedBlock);
  const actions = useEditorActions();

  if (view.kind === 'metadata') {
    return (
      <div className={styles.canvasOuter}>
        <div className={styles.canvasInner}>
          <MetadataPanel />
        </div>
      </div>
    );
  }

  if (view.kind === 'variables') {
    return (
      <div className={styles.canvasOuter}>
        <div className={styles.canvasInner}>
          <VariablesPanel />
        </div>
      </div>
    );
  }

  if (view.kind === 'overrides') {
    return (
      <div className={styles.canvasOuter}>
        <div className={styles.canvasInner}>
          <OverridesPanel />
        </div>
      </div>
    );
  }

  const section = sections[view.sectionIndex];
  if (!section) {
    return (
      <div className={styles.canvasOuter}>
        <div className={styles.canvasInner}>
          <div className={styles.empty}>章节不存在。</div>
        </div>
      </div>
    );
  }

  const meta = SECTION_META[section.kind];
  const blocks = section.blocks;

  const insertAt = (idx: number, type: BlockType) => {
    const block = blockFactoryFor(type);
    if (block) actions.insertBlock(view.sectionIndex, idx, block);
  };

  return (
    <div className={styles.canvasOuter}>
      <div className={styles.canvasInner}>
        <header className={styles.sectionHeader}>
          <h1 className={styles.sectionTitle}>{section.title ?? meta.label}</h1>
          <p className={styles.sectionDesc}>{meta.description}</p>
        </header>

        {!meta.authored && blocks.length === 0 && (
          <div className={styles.autofill}>
            这个章节由模板自动渲染，正常情况下你不需要在此输入。
            <br />
            <small>如果模板不提供，你也可以下方手动添加内容。</small>
          </div>
        )}

        <div className={styles.blocks}>
          {blocks.length === 0 ? (
            <InsertBlockMenu
              variant="large"
              enabled={PHASE_1_ENABLED}
              onPick={(t) => insertAt(0, t)}
            />
          ) : (
            <>
              {blocks.map((block, i) => (
                <div
                  key={block.id ?? `${section.kind}-${i}`}
                  data-block-index={`${view.sectionIndex}-${i}`}
                  className={styles.blockSlot}
                >
                  <InsertBlockMenu
                    variant="inline"
                    enabled={PHASE_1_ENABLED}
                    onPick={(t) => insertAt(i, t)}
                  />
                  <BlockRenderer
                    block={block}
                    sectionIndex={view.sectionIndex}
                    blockIndex={i}
                    selected={
                      selected?.sectionIndex === view.sectionIndex && selected.blockIndex === i
                    }
                    totalBlocks={blocks.length}
                    isLast={i === blocks.length - 1}
                  />
                </div>
              ))}
              <InsertBlockMenu
                variant="inline"
                enabled={PHASE_1_ENABLED}
                onPick={(t) => insertAt(blocks.length, t)}
              />
            </>
          )}
        </div>
      </div>
    </div>
  );
}
