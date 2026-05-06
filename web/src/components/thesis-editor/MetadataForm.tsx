import { Field, Input } from '../design-system/Primitives';
import type { EditorAction } from './editorReducer';
import type { ThesisEditorState } from './types';

export function MetadataForm({ metadata, dispatch }: { metadata: ThesisEditorState['metadata']; dispatch: React.Dispatch<EditorAction> }) {
  const fields: Array<[keyof ThesisEditorState['metadata'], string]> = [
    ['title', '论文题目'],
    ['subtitle', '副标题'],
    ['author', '作者'],
    ['college', '学院'],
    ['major', '专业'],
    ['studentId', '学号'],
    ['advisor', '指导教师'],
    ['date', '日期']
  ];

  return (
    <div className="stack" data-testid="metadata-form">
      {fields.map(([field, label]) => (
        <Field key={field} label={label}>
          <Input
            aria-label={label}
            value={metadata[field]}
            onChange={event => dispatch({ type: 'setMetadata', field, value: event.target.value })}
          />
        </Field>
      ))}
    </div>
  );
}
