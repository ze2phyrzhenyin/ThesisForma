import { useEditorActions } from '../EditorContext';
import { BlockShell } from './BlockShell';
import type { BibliographyBlock } from '@/types';
import styles from './blocks.module.css';
import bibStyles from './BibliographyRefBlock.module.css';

interface Props {
  block: BibliographyBlock;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

export function BibliographyRefBlock({
  block,
  sectionIndex,
  blockIndex,
  selected,
  totalBlocks
}: Props) {
  const actions = useEditorActions();
  const count = block.entries.length;
  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge="参考文献"
    >
      <div className={bibStyles.box}>
        <strong>共 {count} 条参考文献</strong>
        <p className={styles.toolbarLabel}>
          条目集中在右栏「参考文献」抽屉里管理。导出时按当前模板的格式渲染。
        </p>
      </div>
    </BlockShell>
  );
}
