import { useMemo } from 'react';
import { useEditorActions, useEditorStore } from '../EditorContext';
import {
  collectNotes,
  deleteNote,
  insertNoteAtBlockEnd,
  noteLabel,
  updateNoteText,
  type NoteEntry,
  type NoteKind
} from '../notes';
import drawerStyles from './drawer.module.css';

export function NotesPanel() {
  const document = useEditorStore((s) => s.envelope.document);
  const selectedBlock = useEditorStore((s) => s.selectedBlock);
  const actions = useEditorActions();
  const notes = useMemo(() => collectNotes(document), [document]);
  const footnotes = notes.filter((note) => note.kind === 'footnote');
  const endnotes = notes.filter((note) => note.kind === 'endnote');

  const addNote = (kind: NoteKind) => {
    actions.updateDocument((doc) => {
      const bodyIndex = doc.sections.findIndex((section) => section.kind === 'body');
      const sectionIndex = selectedBlock?.sectionIndex ?? (bodyIndex >= 0 ? bodyIndex : 0);
      const blockIndex = selectedBlock?.blockIndex ?? 0;
      insertNoteAtBlockEnd(doc, sectionIndex, blockIndex, kind);
    });
  };

  const update = (entry: NoteEntry, text: string) => {
    actions.updateDocument((doc) => updateNoteText(doc, entry, text));
  };

  const remove = (entry: NoteEntry) => {
    if (!window.confirm(`删除 ${entry.kind === 'footnote' ? '脚注' : '尾注'} ${entry.noteId}？正文中的引用标记会一并删除。`)) return;
    actions.updateDocument((doc) => deleteNote(doc, entry));
  };

  return (
    <div className={drawerStyles.panel}>
      <header className={drawerStyles.header}>
        <h2 className={drawerStyles.title}>脚注 / 尾注</h2>
        <div className={drawerStyles.headerActions}>
          <button type="button" className={drawerStyles.headerBtn} onClick={() => addNote('footnote')}>
            ＋ 脚注
          </button>
          <button type="button" className={drawerStyles.headerBtn} onClick={() => addNote('endnote')}>
            ＋ 尾注
          </button>
        </div>
      </header>

      {notes.length === 0 ? (
        <div className={drawerStyles.empty}>
          还没有注释。选中段落后新增脚注或尾注，会追加到当前文本块末尾。
        </div>
      ) : (
        <>
          <NoteGroup title="脚注" entries={footnotes} allEntries={notes} onUpdate={update} onRemove={remove} />
          <NoteGroup title="尾注" entries={endnotes} allEntries={notes} onUpdate={update} onRemove={remove} />
        </>
      )}
    </div>
  );
}

interface NoteGroupProps {
  title: string;
  entries: NoteEntry[];
  allEntries: NoteEntry[];
  onUpdate(entry: NoteEntry, text: string): void;
  onRemove(entry: NoteEntry): void;
}

function NoteGroup({ title, entries, allEntries, onUpdate, onRemove }: NoteGroupProps) {
  if (entries.length === 0) return null;
  return (
    <section className={drawerStyles.section}>
      <h3 className={drawerStyles.sectionTitle}>{title}</h3>
      <ul className={drawerStyles.list}>
        {entries.map((entry) => {
          const index = allEntries.filter((note) => note.kind === entry.kind).findIndex((note) => note.noteId === entry.noteId);
          return (
            <li key={`${entry.kind}-${entry.noteId}-${entry.sectionIndex}-${entry.blockIndex}-${entry.inlineIndex ?? 'block'}`} className={drawerStyles.item}>
              <div className={drawerStyles.itemHeader}>
                <strong>{noteLabel(entry.kind, Math.max(index, 0))}</strong>
                <code>{entry.noteId}</code>
              </div>
              <div className={drawerStyles.muted}>{entry.locationLabel}</div>
              <textarea
                className={drawerStyles.textInput}
                rows={3}
                value={entry.text}
                placeholder="注释内容"
                onChange={(e) => onUpdate(entry, e.target.value)}
              />
              {!entry.text.trim() && <div className={drawerStyles.warning}>内容为空，将在校验中显示警告。</div>}
              <button
                type="button"
                className={drawerStyles.removeBtn}
                onClick={() => onRemove(entry)}
                aria-label={`删除 ${entry.noteId}`}
              >
                ✕
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
