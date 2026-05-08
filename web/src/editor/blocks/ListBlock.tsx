import { useEditorActions } from '../EditorContext';
import { InlineEditor } from '../InlineEditor';
import { BlockShell } from './BlockShell';
import { newBlockId } from '../ids';
import { plainTextToInlines } from '../inlines';
import type { Inline, ListBlock as ListBlockData, ParagraphBlock } from '@/types';
import styles from './blocks.module.css';

interface Props {
  block: ListBlockData;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

function extractItemInlines(blocks: ListBlockData['items'][number]['blocks']): Inline[] {
  for (const b of blocks) {
    if (b.type === 'paragraph') return b.inlines;
  }
  return [];
}

function makeItemParagraph(text = ''): ParagraphBlock {
  return { type: 'paragraph', id: newBlockId('p'), inlines: plainTextToInlines(text) };
}

export function ListBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
  const actions = useEditorActions();

  const updateBlock = (updater: (b: ListBlockData) => void) =>
    actions.updateBlock(sectionIndex, blockIndex, (b) => {
      if (b.type !== 'list') return;
      updater(b);
    });

  const ListTag = block.ordered ? 'ol' : 'ul';

  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge={block.ordered ? '有序列表' : '无序列表'}
      toolbar={
        <>
          <button
            type="button"
            className={styles.toolbarBtn}
            data-active={!block.ordered}
            onClick={() =>
              updateBlock((b) => {
                delete b.ordered;
              })
            }
          >
            • 无序
          </button>
          <button
            type="button"
            className={styles.toolbarBtn}
            data-active={block.ordered === true}
            onClick={() =>
              updateBlock((b) => {
                b.ordered = true;
              })
            }
          >
            1. 有序
          </button>
        </>
      }
    >
      <ListTag className={styles.list}>
        {block.items.map((item, itemIdx) => {
          const inlines = extractItemInlines(item.blocks);
          const isLast = itemIdx === block.items.length - 1;
          return (
            <li key={itemIdx} className={styles.listItem}>
              <InlineEditor
                inlines={inlines}
                placeholder={isLast ? '列表项' : ''}
                ariaLabel={`列表项 ${itemIdx + 1}`}
                onChange={(next) =>
                  updateBlock((b) => {
                    const target = b.items[itemIdx];
                    if (!target) return;
                    const paraIdx = target.blocks.findIndex((x) => x.type === 'paragraph');
                    if (paraIdx >= 0) {
                      const para = target.blocks[paraIdx];
                      if (para.type === 'paragraph') para.inlines = next;
                    } else {
                      target.blocks.unshift(makeItemParagraph());
                      const p = target.blocks[0];
                      if (p.type === 'paragraph') p.inlines = next;
                    }
                  })
                }
                onEnter={() => {
                  // If current item is empty AND it's the last, exit list by creating a paragraph below
                  const empty = inlines.length === 0;
                  if (empty && isLast && block.items.length > 1) {
                    updateBlock((b) => {
                      b.items.pop();
                    });
                    actions.insertBlock(sectionIndex, blockIndex + 1, makeItemParagraph());
                    return;
                  }
                  updateBlock((b) => {
                    b.items.splice(itemIdx + 1, 0, { blocks: [makeItemParagraph()] });
                  });
                }}
                onBackspaceEmpty={() => {
                  if (block.items.length === 1) {
                    actions.deleteBlock(sectionIndex, blockIndex);
                  } else {
                    updateBlock((b) => {
                      b.items.splice(itemIdx, 1);
                    });
                  }
                }}
                onFocus={() => actions.selectBlock(sectionIndex, blockIndex)}
              />
            </li>
          );
        })}
      </ListTag>
    </BlockShell>
  );
}
