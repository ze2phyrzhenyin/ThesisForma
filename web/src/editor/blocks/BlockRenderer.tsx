import type { Block } from '@/types';
import { ParagraphBlock } from './ParagraphBlock';
import { HeadingBlock } from './HeadingBlock';
import { ListBlock } from './ListBlock';
import { QuoteBlock } from './QuoteBlock';
import { BreakBlock } from './BreakBlock';
import { FigureBlock } from './FigureBlock';
import { TableBlock } from './TableBlock';
import { EquationBlock } from './EquationBlock';
import { BibliographyRefBlock } from './BibliographyRefBlock';
import { UnsupportedBlock } from './UnsupportedBlock';

interface Props {
  block: Block;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
  isLast: boolean;
}

export function BlockRenderer(props: Props) {
  const { block } = props;
  switch (block.type) {
    case 'paragraph':
      return <ParagraphBlock {...props} block={block} />;
    case 'heading':
      return <HeadingBlock {...props} block={block} />;
    case 'list':
      return <ListBlock {...props} block={block} />;
    case 'quote':
      return <QuoteBlock {...props} block={block} />;
    case 'figure':
      return <FigureBlock {...props} block={block} />;
    case 'table':
      return <TableBlock {...props} block={block} />;
    case 'equation':
      return <EquationBlock {...props} block={block} />;
    case 'pageBreak':
    case 'sectionBreak':
      return <BreakBlock {...props} block={block} />;
    case 'bibliography':
      return <BibliographyRefBlock {...props} block={block} />;
    default:
      return <UnsupportedBlock {...props} block={block} />;
  }
}
