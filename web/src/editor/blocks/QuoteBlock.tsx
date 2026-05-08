import { useEditorActions } from '../EditorContext';
import { InlineEditor } from '../InlineEditor';
import { BlockShell } from './BlockShell';
import { blockFactory } from '../store';
import type { QuoteBlock as QuoteBlockData } from '@/types';
import styles from './blocks.module.css';

interface Props {
  block: QuoteBlockData;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

export function QuoteBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
  const actions = useEditorActions();

  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge="引文"
    >
      <div className={styles.quote}>
        <InlineEditor
          inlines={block.inlines}
          placeholder="块级引用"
          ariaLabel="引文"
          onChange={(inlines) => actions.replaceInlines(sectionIndex, blockIndex, inlines)}
          onEnter={() =>
            actions.insertBlock(sectionIndex, blockIndex + 1, blockFactory.paragraph())
          }
          onBackspaceEmpty={() => {
            if (totalBlocks > 1) actions.deleteBlock(sectionIndex, blockIndex);
          }}
          onFocus={() => actions.selectBlock(sectionIndex, blockIndex)}
        />
      </div>
    </BlockShell>
  );
}
