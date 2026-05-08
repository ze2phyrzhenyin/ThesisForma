import { useState } from 'react';
import { useEditorActions } from '../EditorContext';
import { InlineEditor } from '../InlineEditor';
import { InlineActionBar } from '../InlineActionBar';
import { BlockShell } from './BlockShell';
import { blockFactory } from '../store';
import type { HeadingBlock as HeadingBlockData } from '@/types';
import type { Editor } from '@tiptap/react';
import styles from './blocks.module.css';

interface Props {
  block: HeadingBlockData;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

const LEVELS: HeadingBlockData['level'][] = [1, 2, 3, 4, 5, 6];

export function HeadingBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
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
      badge={`标题 ${block.level}`}
      toolbar={
        <>
          <InlineActionBar editor={editor} visible={selected} />
          <span className={styles.toolbarSeparator}>·</span>
          <span className={styles.toolbarLabel}>级别：</span>
          {LEVELS.map((l) => (
            <button
              key={l}
              type="button"
              className={styles.toolbarBtn}
              data-active={block.level === l}
              onClick={() =>
                actions.updateBlock(sectionIndex, blockIndex, (b) => {
                  if (b.type !== 'heading') return;
                  b.level = l;
                })
              }
            >
              H{l}
            </button>
          ))}
          <span className={styles.toolbarSeparator}>·</span>
          <label className={styles.toolbarCheck}>
            <input
              type="checkbox"
              checked={block.numbered ?? false}
              onChange={(e) =>
                actions.updateBlock(sectionIndex, blockIndex, (b) => {
                  if (b.type !== 'heading') return;
                  if (e.target.checked) b.numbered = true;
                  else delete b.numbered;
                })
              }
            />
            自动编号
          </label>
        </>
      }
    >
      <div className={styles.heading} data-level={block.level}>
        <InlineEditor
          inlines={block.inlines}
          placeholder={`${block.level} 级标题`}
          ariaLabel={`${block.level} 级标题`}
          autofocus={selected && block.inlines.length === 0}
          onChange={(inlines) => actions.replaceInlines(sectionIndex, blockIndex, inlines)}
          onEnter={() =>
            actions.insertBlock(sectionIndex, blockIndex + 1, blockFactory.paragraph())
          }
          onBackspaceEmpty={() => {
            if (totalBlocks > 1) actions.deleteBlock(sectionIndex, blockIndex);
          }}
          onFocus={() => actions.selectBlock(sectionIndex, blockIndex)}
          onEditorReady={setEditor}
        />
      </div>
    </BlockShell>
  );
}
