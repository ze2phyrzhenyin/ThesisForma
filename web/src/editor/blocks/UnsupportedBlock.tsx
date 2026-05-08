import { useEditorActions } from '../EditorContext';
import { BlockShell } from './BlockShell';
import type { Block } from '@/types';
import styles from './blocks.module.css';

interface Props {
  block: Block;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

export function UnsupportedBlock({
  block,
  sectionIndex,
  blockIndex,
  selected,
  totalBlocks
}: Props) {
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
      badge={`${block.type}（占位）`}
    >
      <div className={styles.unsupported}>
        <strong>{block.type}</strong> 块的可视化编辑将在阶段 2 上线。导入的内容已保留，导出仍可正常工作。
      </div>
    </BlockShell>
  );
}
