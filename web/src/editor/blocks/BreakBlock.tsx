import { useEditorActions } from '../EditorContext';
import { BlockShell } from './BlockShell';
import type { PageBreakBlock, SectionBreakBlock } from '@/types';
import styles from './blocks.module.css';

interface Props {
  block: PageBreakBlock | SectionBreakBlock;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

export function BreakBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
  const actions = useEditorActions();
  const label = block.type === 'pageBreak' ? '分页符' : '分节符';
  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge={label}
    >
      <div className={styles.break} aria-label={label}>
        <span className={styles.breakLine} />
        <span className={styles.breakText}>— {label} —</span>
        <span className={styles.breakLine} />
      </div>
    </BlockShell>
  );
}
