import { useState } from 'react';
import { useEditorActions } from '../EditorContext';
import { InlineEditor } from '../InlineEditor';
import { InlineActionBar } from '../InlineActionBar';
import { BlockShell } from './BlockShell';
import { blockFactory } from '../store';
import type { ParagraphBlock as ParagraphBlockData, TextAlignment } from '@/types';
import type { Editor } from '@tiptap/react';
import styles from './blocks.module.css';

interface Props {
  block: ParagraphBlockData;
  sectionIndex: number;
  blockIndex: number;
  isLast: boolean;
  selected: boolean;
  totalBlocks: number;
}

const ALIGN_LABELS: Record<TextAlignment, string> = {
  left: '左',
  center: '中',
  right: '右',
  both: '两端'
};

export function ParagraphBlock({
  block,
  sectionIndex,
  blockIndex,
  isLast,
  selected,
  totalBlocks
}: Props) {
  const actions = useEditorActions();
  const [editor, setEditor] = useState<Editor | null>(null);

  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge={block.alignment ? `段落 · ${ALIGN_LABELS[block.alignment]}` : '段落'}
      toolbar={
        <>
          <InlineActionBar editor={editor} visible={selected} />
          <span className={styles.toolbarSeparator}>·</span>
          <span className={styles.toolbarLabel}>对齐：</span>
          {(['left', 'center', 'right', 'both'] as TextAlignment[]).map((a) => (
            <button
              key={a}
              type="button"
              className={styles.toolbarBtn}
              data-active={(block.alignment ?? 'left') === a}
              onClick={() =>
                actions.updateBlock(sectionIndex, blockIndex, (b) => {
                  if (b.type !== 'paragraph') return;
                  if (a === 'left') delete b.alignment;
                  else b.alignment = a;
                })
              }
            >
              {ALIGN_LABELS[a]}
            </button>
          ))}
        </>
      }
    >
      <div className={styles.paragraph} data-align={block.alignment ?? 'left'}>
        <InlineEditor
          inlines={block.inlines}
          placeholder={
            blockIndex === 0 && isLast ? '开始写正文，按 Enter 换段，按 Backspace 删除空段。' : ''
          }
          ariaLabel="段落"
          autofocus={selected && blockIndex === totalBlocks - 1 && block.inlines.length === 0}
          onChange={(inlines) => actions.replaceInlines(sectionIndex, blockIndex, inlines)}
          onEnter={() =>
            actions.insertBlock(sectionIndex, blockIndex + 1, blockFactory.paragraph())
          }
          onBackspaceEmpty={() => {
            if (totalBlocks > 1) {
              actions.deleteBlock(sectionIndex, blockIndex);
            }
          }}
          onFocus={() => actions.selectBlock(sectionIndex, blockIndex)}
          onEditorReady={setEditor}
        />
      </div>
    </BlockShell>
  );
}
