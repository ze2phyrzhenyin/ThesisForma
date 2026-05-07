import { Field, Input } from '../ui/Primitives';
import type { EditorAction } from './editorReducer';
import type { ThesisEditorState } from './types';

const FIELDS: Array<[keyof ThesisEditorState['metadata'], string]> = [
  ['title', '论文题目'],
  ['subtitle', '副标题'],
  ['author', '作者'],
  ['college', '学院'],
  ['major', '专业'],
  ['studentId', '学号'],
  ['advisor', '指导教师'],
  ['date', '日期']
];

export function MetadataForm({
  metadata,
  dispatch
}: {
  metadata: ThesisEditorState['metadata'];
  dispatch: React.Dispatch<EditorAction>;
}) {
  return (
    <div className="metadata-grid" data-testid="metadata-form">
      {FIELDS.map(([field, label]) => (
        <Field
          key={field}
          label={label}
          className={field === 'title' ? 'field-title' : undefined}
        >
          <Input
            aria-label={label}
            value={metadata[field]}
            onChange={event =>
              dispatch({ type: 'setMetadata', field, value: event.target.value })
            }
            placeholder={field === 'title' ? '请输入论文题目' : ''}
          />
        </Field>
      ))}
    </div>
  );
}
