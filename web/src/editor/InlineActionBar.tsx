import { useState, useRef, useEffect } from 'react';
import { useEditorStore } from './EditorContext';
import type { Editor } from '@tiptap/react';
import type { BibliographyEntry, BibliographyBlock } from '@/types';
import styles from './InlineActionBar.module.css';

interface Props {
  /** The TipTap editor instance for the currently focused block. */
  editor: Editor | null;
  visible: boolean;
}

type Mode = null | 'citation' | 'link';

export function InlineActionBar({ editor, visible }: Props) {
  const sections = useEditorStore((s) => s.envelope.document.sections);
  const [mode, setMode] = useState<Mode>(null);
  const [filter, setFilter] = useState('');
  const [linkUrl, setLinkUrl] = useState('');
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!visible) setMode(null);
  }, [visible]);

  useEffect(() => {
    if (!mode) return;
    const onClick = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setMode(null);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMode(null);
    };
    document.addEventListener('mousedown', onClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [mode]);

  const entries: BibliographyEntry[] = (() => {
    const sIdx = sections.findIndex((s) => s.kind === 'bibliography');
    if (sIdx < 0) return [];
    const block = sections[sIdx].blocks.find((b) => b.type === 'bibliography') as
      | BibliographyBlock
      | undefined;
    return block?.entries ?? [];
  })();

  const filtered = filter
    ? entries.filter(
        (e) =>
          e.id.toLowerCase().includes(filter.toLowerCase()) ||
          e.text.toLowerCase().includes(filter.toLowerCase())
      )
    : entries;

  if (!visible) return null;

  return (
    <div className={styles.bar} ref={containerRef}>
      <button
        type="button"
        className={styles.btn}
        onClick={() => editor?.chain().focus().toggleBold().run()}
        aria-label="加粗"
        title="加粗 ⌘B"
      >
        <strong>B</strong>
      </button>
      <button
        type="button"
        className={styles.btn}
        onClick={() => editor?.chain().focus().toggleItalic().run()}
        aria-label="斜体"
        title="斜体 ⌘I"
      >
        <em>I</em>
      </button>
      <button
        type="button"
        className={styles.btn}
        onClick={() => editor?.chain().focus().toggleUnderline().run()}
        aria-label="下划线"
        title="下划线 ⌘U"
      >
        <u>U</u>
      </button>
      <span className={styles.sep} aria-hidden />
      <button
        type="button"
        className={styles.btn}
        onClick={() => {
          setMode((m) => (m === 'link' ? null : 'link'));
          setLinkUrl('');
        }}
        aria-label="插入链接"
        title="插入超链接"
      >
        🔗
      </button>
      <button
        type="button"
        className={styles.btn}
        onClick={() => {
          setMode((m) => (m === 'citation' ? null : 'citation'));
          setFilter('');
        }}
        aria-label="插入引用"
        title="引用文献"
      >
        @
      </button>

      {mode === 'citation' && (
        <div className={styles.popover}>
          <input
            type="text"
            className={styles.search}
            placeholder="按编号或题名搜索…"
            autoFocus
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          />
          {entries.length === 0 ? (
            <div className={styles.empty}>
              参考文献库为空。先在右侧「参考文献」抽屉添加条目。
            </div>
          ) : filtered.length === 0 ? (
            <div className={styles.empty}>无匹配条目。</div>
          ) : (
            <ul className={styles.list}>
              {filtered.slice(0, 12).map((entry) => (
                <li key={entry.id}>
                  <button
                    type="button"
                    className={styles.entry}
                    onClick={() => {
                      if (!editor) return;
                      editor.commands.focus();
                      editor.commands.insertCitation({
                        targetId: entry.id,
                        displayText: `[${entry.id}]`
                      });
                      setMode(null);
                    }}
                  >
                    <code className={styles.entryId}>{entry.id}</code>
                    <span className={styles.entryText}>{entry.text || '（待补全）'}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {mode === 'link' && (
        <div className={styles.popover}>
          <input
            type="url"
            className={styles.search}
            placeholder="https://…"
            autoFocus
            value={linkUrl}
            onChange={(e) => setLinkUrl(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && linkUrl) {
                e.preventDefault();
                applyLink();
              }
            }}
          />
          <div className={styles.linkActions}>
            <button
              type="button"
              className={styles.linkBtn}
              onClick={() => {
                editor?.chain().focus().unsetLink().run();
                setMode(null);
              }}
            >
              移除链接
            </button>
            <button
              type="button"
              className={styles.linkBtnPrimary}
              onClick={applyLink}
              disabled={!linkUrl}
            >
              应用
            </button>
          </div>
        </div>
      )}
    </div>
  );

  function applyLink() {
    if (!editor || !linkUrl) return;
    editor.chain().focus().extendMarkRange('link').setLink({ href: linkUrl }).run();
    setMode(null);
  }
}
