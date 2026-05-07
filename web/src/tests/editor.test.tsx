import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ThesisEditorPage } from '../components/thesis-editor/ThesisEditorPage';
import { ValidationPanel } from '../components/thesis-editor/ValidationPanel';
import { RenderPanel } from '../components/thesis-editor/RenderPanel';
import { HomePage } from '../app/HomePage';
import { App } from '../app/App';
import { TemplatesPage } from '../app/TemplatesPage';
import { Button, Modal } from '../components/ui/Primitives';
import { renderApi } from '../api/client';
import { editorReducer, blockFactories, localValidate } from '../components/thesis-editor/editorReducer';
import { collectReferenceTargets, createInitialState, createTableBlock, deserializeFromThesisDocument, serializeToThesisDocument, validateEditorState } from '../components/thesis-editor/serialization';
import type { ThesisEditorState } from '../components/thesis-editor/types';

const templateResponse = {
  templates: [
    {
      id: 'example-university-engineering',
      name: 'Example University Engineering Thesis',
      school: 'Example University',
      college: 'Example Engineering College',
      version: '1.0.0',
      status: 'ready',
      coverage: 0.875,
      readiness: 'ready',
      tags: ['example']
    }
  ]
};

beforeEach(() => {
  vi.restoreAllMocks();
  localStorage.clear();
  URL.createObjectURL = URL.createObjectURL ?? (() => 'blob:thesisforma-test');
  URL.revokeObjectURL = URL.revokeObjectURL ?? (() => undefined);
  vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:thesisforma-test');
  vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  if (!HTMLElement.prototype.scrollIntoView) {
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      value: vi.fn()
    });
  }
  vi.spyOn(HTMLElement.prototype, 'scrollIntoView').mockImplementation(() => undefined);
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.endsWith('/api/templates')) {
      return jsonResponse(templateResponse);
    }
    if (url.endsWith('/api/documents')) {
      return jsonResponse({ id: 'doc-test', document: {}, templateId: 'example-university-engineering' }, 201);
    }
    if (url.includes('/api/documents/doc-test/validate')) {
      return jsonResponse({ isValid: true, issues: [] });
    }
    if (url.includes('/api/documents/doc-test/render')) {
      return jsonResponse({ runId: 'run-test', status: 'valid', openXmlValid: true, formatValid: true, downloadUrl: '/api/runs/run-test/download', issues: [] });
    }
    if (url.endsWith('/api/assets/images')) {
      return jsonResponse({ assetId: 'asset-test', fileName: 'asset-test.png', imagePath: '../assets/asset-test.png', previewUrl: '/api/assets/asset-test', contentType: 'image/png' });
    }
    return jsonResponse({});
  }));
  vi.spyOn(window, 'confirm').mockReturnValue(true);
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
});

describe('structured thesis editor UI', () => {
  it('Editor_ShouldRenderThreeColumnLayout', async () => {
    render(<ThesisEditorPage />);
    expect(await screen.findByTestId('three-column-layout')).toBeInTheDocument();
    expect(screen.getByText('论文结构大纲')).toBeInTheDocument();
    expect(screen.getByText('编辑辅助')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /属性/ })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /模板/ })).toBeInTheDocument();
  });

  it('HomePage_ShouldShowClearFrontendOnlyMode', () => {
    render(<HomePage onNew={vi.fn()} onTemplates={vi.fn()} />);
    expect(screen.getByText('论文结构化编辑器')).toBeInTheDocument();
    expect(screen.getByText(/支持结构化编辑、本地保存、导入\/导出 JSON/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '新建论文' })).toBeInTheDocument();
  });

  it('HomePage_ShouldDeleteLocalDraft', async () => {
    const user = userEvent.setup();
    localStorage.setItem('thesisforma.document.doc-delete', JSON.stringify({
      id: 'doc-delete',
      templateId: 'example-university-engineering',
      updatedAt: '2026-05-07T00:00:00.000Z',
      document: { metadata: { title: '待删除草稿' } }
    }));

    render(<HomePage onNew={vi.fn()} onTemplates={vi.fn()} onOpenDraft={vi.fn()} />);
    expect(screen.getByText('待删除草稿')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: '删除草稿：待删除草稿' }));

    expect(localStorage.getItem('thesisforma.document.doc-delete')).toBeNull();
    expect(screen.queryByText('待删除草稿')).not.toBeInTheDocument();
  });

  it('HomePage_ShouldShowImportFailureMessageForInvalidJson', async () => {
    const user = userEvent.setup();
    window.history.pushState({}, '', '/');
    render(<App />);
    const input = document.querySelector('[data-home-import]') as HTMLInputElement;
    await user.upload(input, new File(['{not-json'], 'bad.json', { type: 'application/json' }));

    expect(await screen.findByText('导入 JSON 失败')).toBeInTheDocument();
    expect(screen.getByText(/文件不是有效 JSON/)).toBeInTheDocument();
  });

  it('TemplatePage_ShouldShowTemplateStatusAndGaps', async () => {
    render(<TemplatesPage onSelect={vi.fn()} />);
    expect(await screen.findByText('Example University Engineering Thesis')).toBeInTheDocument();
    expect(screen.getAllByText('ready').length).toBeGreaterThan(0);
    expect(screen.getByText('Known gaps')).toBeInTheDocument();
    expect(screen.getByText('前端支持')).toBeInTheDocument();
  });

  it('Editor_ShouldShowAutosaveStatus', () => {
    render(<ThesisEditorPage />);
    expect(screen.getByText('未保存')).toBeInTheDocument();
  });

  it('FrontendOnlyAutosave_ShouldPersistMeaningfulDraftLocally', async () => {
    render(<ThesisEditorPage />);
    fireEvent.change(screen.getByLabelText('论文题目'), { target: { value: '本地自动保存论文' } });

    await waitFor(() => {
      const stored = Object.keys(localStorage)
        .filter(key => key.startsWith('thesisforma.document.'))
        .map(key => localStorage.getItem(key) ?? '')
        .join('\n');
      expect(stored).toContain('本地自动保存论文');
    }, { timeout: 1800 });
    expect(screen.getByText('已保存')).toBeInTheDocument();
  });

  it('EditorToolbar_ShouldShowHomeBackUndoRedoControls', () => {
    render(<ThesisEditorPage />);
    expect(screen.getByRole('button', { name: '首页' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '模板' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '后退' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '撤销' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '重做' })).toBeDisabled();
  });

  it('EditorUndoRedo_ShouldRestoreMetadataChange', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    const titleInput = screen.getByLabelText('论文题目');
    fireEvent.change(titleInput, { target: { value: '撤销测试论文' } });
    expect(titleInput).toHaveValue('撤销测试论文');

    await user.click(screen.getByRole('button', { name: '撤销' }));
    expect(titleInput).toHaveValue('');

    await user.click(screen.getByRole('button', { name: '重做' }));
    expect(titleInput).toHaveValue('撤销测试论文');
  });

  it('AppNavigation_ShouldReturnHomeFromEditor', async () => {
    const user = userEvent.setup();
    window.history.pushState({}, '', '/');
    render(<App />);
    await user.click(screen.getByRole('button', { name: '新建论文' }));
    expect(await screen.findByTestId('three-column-layout')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: '首页' }));
    expect(await screen.findByText('论文结构化编辑器')).toBeInTheDocument();
  });

  it('AppNavigation_ShouldReturnHomeWhenClickingTopLeftBrand', async () => {
    const user = userEvent.setup();
    window.history.pushState({}, '', '/');
    render(<App />);
    await user.click(screen.getByRole('button', { name: '新建论文' }));
    expect(await screen.findByTestId('three-column-layout')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ThesisForma 返回首页' }));
    expect(await screen.findByText('论文结构化编辑器')).toBeInTheDocument();
  });

  it('Editor_ShouldNotAllowManualFontSizeEditing', () => {
    render(<ThesisEditorPage />);
    expect(screen.getAllByText(/由模板.*控制/).length).toBeGreaterThan(0);
    expect(screen.queryByLabelText('字号')).not.toBeInTheDocument();
  });

  it('Editor_ShouldCreateHeadingBlock', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(screen.getAllByText('标题')[0]);
    expect(screen.getAllByTestId('block-heading').length).toBeGreaterThan(1);
  });

  it('Editor_ShouldCreateParagraphBlock', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(screen.getAllByText('正文段落')[0]);
    expect(screen.getByLabelText('正文段落')).toBeInTheDocument();
  });

  it('Editor_ShouldInsertTableBlock', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await insertTable(user);
    expect(screen.getByTestId('block-table')).toBeInTheDocument();
    expect(screen.getByLabelText('表格编辑器')).toBeInTheDocument();
  });

  it('TableEditor_ShouldAddRowAndColumn', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await insertTable(user);
    const before = screen.getAllByRole('textbox').length;
    await user.click(screen.getByText('添加行'));
    await user.click(screen.getByText('添加列'));
    expect(screen.getAllByRole('textbox').length).toBeGreaterThan(before);
  });

  it('InsertBlockMenu_ShouldGroupItemsClearly', () => {
    render(<ThesisEditorPage />);
    expect(screen.getAllByText('常用').length).toBeGreaterThan(0);
    expect(screen.getAllByText('学术元素').length).toBeGreaterThan(0);
    expect(screen.getAllByText('结构').length).toBeGreaterThan(0);
  });

  it('TableEditor_ShouldShowValidationForEmptyCaption', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await insertTable(user, '');
    expect(screen.getByText(/表格需要表名/)).toBeInTheDocument();
  });

  it('FigureEditor_ShouldUploadAndInsertImage', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await insertFigure(user);
    const input = document.querySelector('input[accept^="image"]') as HTMLInputElement;
    await user.upload(input, new File(['png'], 'a.png', { type: 'image/png' }));
    expect(await screen.findByAltText('图名待填写')).toBeInTheDocument();
  });

  it('FigureEditor_ShouldShowUploadPreviewAndSerializeAsset', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await insertFigure(user, '结构图', '流程图预览');
    const input = document.querySelector('input[accept^="image"]') as HTMLInputElement;
    await user.upload(input, new File(['png'], 'a.png', { type: 'image/png' }));
    expect(await screen.findByAltText('流程图预览')).toBeInTheDocument();
    expect(screen.getByText('本地资产已记录')).toBeInTheDocument();
  });

  it('BibliographyManager_ShouldAddEntry', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(screen.getByRole('tab', { name: /引用/ }));
    await user.click(screen.getByText('添加文献'));
    expect(screen.getByLabelText(/参考文献 key/)).toBeInTheDocument();
  });

  it('BibliographyManager_ShouldShowReferencedStatus', async () => {
    const user = userEvent.setup();
    const state = createReadyState();
    const paragraph = blockFactories.paragraph();
    if (paragraph.type !== 'paragraph') throw new Error('Expected paragraph factory');
    paragraph.inlines = [{ type: 'citation', targetId: 'ref-a', displayText: '[ref-a]' }];
    state.sections.find(section => section.id === 'body')!.blocks.push(paragraph);
    render(<ThesisEditorPage initialState={state} />);
    await user.click(screen.getByRole('tab', { name: /引用/ }));
    expect(screen.getByText('已引用')).toBeInTheDocument();
  });

  it('CitationPicker_ShouldInsertCitation', () => {
    const state = createInitialState();
    const paragraph = blockFactories.paragraph();
    const withParagraph = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: paragraph });
    const withRef = editorReducer(withParagraph, { type: 'addBibliographyEntry', entry: { id: 'r1', key: 'ref-a', text: 'A', entryType: 'book' } });
    const next = editorReducer(withRef, { type: 'insertCitation', blockId: paragraph.id, key: 'ref-a' });
    const inserted = next.sections.flatMap(section => section.blocks).find(block => block.id === paragraph.id);
    expect(inserted?.type === 'paragraph' && inserted.inlines.some(inline => inline.type === 'citation' && inline.targetId === 'ref-a')).toBe(true);
  });

  it('CrossReferencePicker_ShouldListFiguresTablesHeadings', () => {
    const state = createInitialState();
    const table = createTableBlock();
    const withTable = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: table });
    const targets = collectReferenceTargets(withTable);
    expect(targets.some(target => target.type === 'heading')).toBe(true);
    expect(targets.some(target => target.type === 'table')).toBe(true);
  });

  it('TocPreview_ShouldReflectHeadingBlocks', () => {
    const state = createInitialState();
    render(<ThesisEditorPage initialState={state} />);
    expect(screen.getByTestId('toc-preview')).toHaveTextContent('绪论');
  });

  it('ValidationPanel_ShouldShowIssuesAndJumpToBlock', async () => {
    const user = userEvent.setup();
    const onJump = vi.fn();
    render(<ValidationPanel issues={[{ code: 'table.caption.required', severity: 'error', message: '表格缺少表名。', blockId: 'heading-1' }]} onJump={onJump} />);
    expect(screen.getByTestId('validation-panel')).toHaveTextContent('表格缺少表名');
    await user.click(screen.getByText('跳转到内容块'));
    expect(onJump).toHaveBeenCalledWith('heading-1');
  });

  it('ValidationPanel_ShouldGroupIssuesBySeverity', () => {
    render(<ValidationPanel issues={[
      { code: 'a', severity: 'error', message: '错误问题' },
      { code: 'b', severity: 'warning', message: '警告问题' },
      { code: 'c', severity: 'info', message: '提示问题' }
    ]} onJump={vi.fn()} />);
    expect(screen.getByText('必须修复')).toBeInTheDocument();
    expect(screen.getByText('建议修复')).toBeInTheDocument();
    expect(screen.getAllByText('提示').length).toBeGreaterThan(1);
  });

  it('ValidationPanel_ShouldJumpToAndHighlightAffectedBlockInEditor', async () => {
    const user = userEvent.setup();
    const table = createTableBlock(2, 2, '');
    const state = {
      ...createInitialState(),
      metadata: {
        ...createInitialState().metadata,
        title: '校验跳转测试论文'
      },
      sections: createInitialState().sections.map(section =>
        section.id === 'body'
          ? { ...section, blocks: [...section.blocks, table] }
          : section
      )
    };

    render(<ThesisEditorPage initialState={state} />);
    await user.click(screen.getByRole('button', { name: '校验' }));
    await user.click(screen.getByRole('tab', { name: /校验/ }));
    await user.click(await screen.findByRole('button', { name: '跳转到内容块' }));

    const tableBlock = screen.getByTestId('block-table');
    await waitFor(() => expect(tableBlock).toHaveClass('active'));
    expect(tableBlock).toHaveClass('attention');
    expect(tableBlock).toHaveFocus();
    expect(HTMLElement.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it('TemplateStatusPanel_ShouldShowDraftTemplateGaps', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(await screen.findByRole('tab', { name: /模板/ }));
    expect(await screen.findByTestId('template-status-panel')).toHaveTextContent('覆盖');
  });

  it('RenderPanel_ShouldStartRenderAndShowDownload', async () => {
    render(<RenderPanel onRender={vi.fn()} run={{ runId: 'run-test', status: 'valid', openXmlValid: true, formatValid: true, downloadUrl: '/download.docx', issues: [] }} />);
    expect(screen.getByText('下载 DOCX')).toBeInTheDocument();
  });

  it('FrontendOnlyMode_ShouldDisableDocxRender', () => {
    render(<ThesisEditorPage initialState={createReadyState()} />);
    expect(screen.getAllByRole('button', { name: '生成 DOCX' })[0]).toBeDisabled();
  });

  it('RenderButton_ShouldShowBackendRequiredMessage', () => {
    render(<ThesisEditorPage initialState={createReadyState()} />);
    expect(screen.getByText(/当前部署仅支持结构化编辑与 JSON 导出/)).toBeInTheDocument();
  });

  it('ExportJson_ShouldDownloadValidThesisDocument', async () => {
    const user = userEvent.setup();
    let exportedBlob: Blob | undefined;
    vi.mocked(URL.createObjectURL).mockImplementation(blob => {
      exportedBlob = blob as Blob;
      return 'blob:thesis-document-json';
    });

    const state = createReadyState();
    state.metadata.title = '可导出论文';
    render(<ThesisEditorPage initialState={state} />);
    await user.click(screen.getByText('导出 JSON'));
    expect(screen.getByText('导出 ThesisDocument JSON')).toBeInTheDocument();
    expect(screen.getByText('结构校验通过')).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: '导出 JSON' }).at(-1)!);

    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();
    const parsed = JSON.parse(await readBlobText(exportedBlob!));
    expect(parsed.schemaVersion).toBe('1.1.0');
    expect(parsed.metadata.title).toBe('可导出论文');
  });

  it('ImportJson_ShouldLoadDocumentState', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(screen.getByText('导入 JSON'));
    const input = document.querySelector('input[accept="application/json,.json"]') as HTMLInputElement;
    await user.upload(input, new File([JSON.stringify({
      schemaVersion: '1.1.0',
      metadata: { title: '导入论文', author: '导入作者', college: '学院', major: '专业', studentId: '2026', advisor: '导师', date: '2026-05-06' },
      sections: [{ id: 'body', kind: 'body', title: '正文', blocks: [{ type: 'heading', id: 'h1', level: 1, text: '导入标题' }] }]
    })], 'document.json', { type: 'application/json' }));

    expect(await screen.findByDisplayValue('导入论文')).toBeInTheDocument();
    expect(screen.getByDisplayValue('导入标题')).toBeInTheDocument();
  });

  it('ImportJson_ShouldShowClearErrorForInvalidJson', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(screen.getByText('导入 JSON'));
    const input = document.querySelector('input[accept="application/json,.json"]') as HTMLInputElement;
    await user.upload(input, new File(['{bad-json'], 'bad.json', { type: 'application/json' }));

    expect(await screen.findByText('导入失败：文件不是有效 JSON。')).toBeInTheDocument();
  });

  it('Editor_ShouldCreateHeadingParagraphTableBibliography', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(screen.getAllByText('标题')[0]);
    await user.click(screen.getAllByText('正文段落')[0]);
    await insertTable(user);
    await user.click(screen.getByRole('tab', { name: /引用/ }));
    await user.click(screen.getByText('添加文献'));

    expect(screen.getAllByTestId('block-heading').length).toBeGreaterThan(1);
    expect(screen.getByLabelText('正文段落')).toBeInTheDocument();
    expect(screen.getByTestId('block-table')).toBeInTheDocument();
    expect(screen.getByLabelText(/参考文献 key/)).toBeInTheDocument();
  });

  it('BibliographyManager_ShouldAddTypedJournalEntry', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.click(screen.getByRole('tab', { name: /引用/ }));
    await user.click(screen.getByText('添加期刊'));

    expect(screen.getByText('期刊')).toBeInTheDocument();
    expect(screen.getByDisplayValue(/\[序号\] 作者\. 题名\[J\]/)).toBeInTheDocument();
  });

  it('RenderPanel_ShouldDisableDocxRenderInFrontendOnlyMode', () => {
    render(<RenderPanel onRender={vi.fn()} docxRenderEnabled={false} />);
    expect(screen.getByRole('button', { name: '生成 DOCX' })).toBeDisabled();
  });

  it('RenderPanel_ShouldExplainBackendRequirement', () => {
    render(<RenderPanel onRender={vi.fn()} docxRenderEnabled={false} />);
    expect(screen.getByText(/生成 DOCX 需要后端服务/)).toBeInTheDocument();
  });

  it('DesignSystem_Button_ShouldHaveDisabledAndFocusStates', () => {
    render(<Button disabled>不可用操作</Button>);
    expect(screen.getByRole('button', { name: '不可用操作' })).toBeDisabled();
  });

  it('Modal_ShouldCloseOnEscape', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Modal title="测试弹窗" onClose={onClose}>内容</Modal>);
    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });

  it('TocPreview_ShouldUpdateFromHeadings', async () => {
    const user = userEvent.setup();
    render(<ThesisEditorPage />);
    await user.clear(screen.getByLabelText('标题文本'));
    await user.type(screen.getByLabelText('标题文本'), '新的绪论标题');
    expect(screen.getByTestId('toc-preview')).toHaveTextContent('新的绪论标题');
  });
});

describe('editor state serialization', () => {
  it('TableEditor_ShouldSerializeToThesisDocument', () => {
    const state = createInitialState();
    const table = createTableBlock(2, 2, '数据表');
    const next = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: table });
    const doc = serializeToThesisDocument(next);
    const body = doc.sections.find(section => section.id === 'body')!;
    expect(body.blocks.some((block: any) => block.type === 'table' && block.caption === '数据表')).toBe(true);
  });

  it('TableEditor_ShouldSerializeRowsAndColumns', () => {
    const table = createTableBlock(2, 3, '三列表');
    table.rows[0].cells[2].text = '第三列';
    const state = editorReducer(createInitialState(), { type: 'insertBlock', sectionId: 'body', block: table });
    const doc = serializeToThesisDocument(state);
    const body = doc.sections.find(section => section.id === 'body')!;
    const tableBlock = body.blocks.find((block: any) => block.type === 'table') as any;
    expect(tableBlock.rows).toHaveLength(2);
    expect(tableBlock.rows[0].cells).toHaveLength(3);
    expect(tableBlock.rows[0].cells[2].text).toBe('第三列');
  });

  it('Serializer_ShouldIncludeMetadata', () => {
    const state = createInitialState();
    state.metadata.title = '论文题目';
    const doc = serializeToThesisDocument(state);
    expect(doc.metadata.title).toBe('论文题目');
  });

  it('Serializer_ShouldExportBibliographyBlock', () => {
    const state = createInitialState();
    state.bibliography = [{ id: 'r1', key: 'ref-a', text: '参考文献 A', entryType: 'journal' }];
    const doc = serializeToThesisDocument(state);
    const refs = doc.sections.find(section => section.kind === 'bibliography')!;
    expect((refs.blocks[0] as any).entries[0].id).toBe('ref-a');
  });

  it('Serializer_ShouldKeepCitationInline', () => {
    const state = createInitialState();
    const paragraph = blockFactories.paragraph();
    const inserted = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: paragraph });
    const cited = editorReducer(inserted, { type: 'insertCitation', blockId: paragraph.id, key: 'ref-a' });
    const doc = serializeToThesisDocument(cited);
    expect(JSON.stringify(doc)).toContain('"type":"citation"');
  });

  it('BibliographyCitation_ShouldReferenceExistingKey', () => {
    const state = createInitialState();
    const paragraph = blockFactories.paragraph();
    const inserted = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: paragraph });
    const withBibliography = editorReducer(inserted, { type: 'addBibliographyEntry', entry: { id: 'r1', key: 'ref-a', text: 'A', entryType: 'book' } });
    const cited = editorReducer(withBibliography, { type: 'insertCitation', blockId: paragraph.id, key: 'ref-a' });
    expect(validateEditorState(cited).some(issue => issue.code === 'citation.targetMissing')).toBe(false);
  });

  it('ApiClient_ShouldNotCallRenderWhenApiDisabled', async () => {
    vi.mocked(fetch).mockClear();
    const run = await renderApi.render('doc-local', 'example-university-engineering');
    expect(run.status).toBe('disabled');
    expect(fetch).not.toHaveBeenCalled();
  });

  it('Deserializer_ShouldReadThesisDocumentMetadataAndHeading', () => {
    const state = deserializeFromThesisDocument({
      metadata: { title: '导入论文', author: '作者' },
      sections: [{ id: 'body', kind: 'body', title: '正文', blocks: [{ type: 'heading', id: 'h1', level: 1, text: '标题' }] }]
    });
    expect(state.metadata.title).toBe('导入论文');
    expect(state.sections[0].blocks[0].type).toBe('heading');
  });

  it('Serializer_ShouldKeepCrossReferenceInline', () => {
    const state = createInitialState();
    const paragraph = blockFactories.paragraph();
    const inserted = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: paragraph });
    const referenced = editorReducer(inserted, { type: 'insertReference', blockId: paragraph.id, bookmarkName: 'heading-1', label: '绪论' });
    const doc = serializeToThesisDocument(referenced);
    expect(JSON.stringify(doc)).toContain('"type":"reference"');
  });

  it('Validator_ShouldCatchMissingMetadataTitle', () => {
    const state = createInitialState();
    const issues = validateEditorState(state);
    expect(issues.some(issue => issue.code === 'metadata.title.required')).toBe(true);
  });

  it('Validator_ShouldCatchInvalidHeadingJump', () => {
    const state = createInitialState();
    state.sections.find(section => section.id === 'body')!.blocks = [{ type: 'heading', id: 'h3', level: 3, text: '三级标题' }];
    expect(validateEditorState(state).some(issue => issue.code === 'heading.levelJump')).toBe(true);
  });

  it('Validator_ShouldCatchMissingTableCaption', () => {
    const state = editorReducer(createInitialState(), { type: 'insertBlock', sectionId: 'body', block: createTableBlock(2, 2, '') });
    expect(localValidate(state).some(issue => issue.code === 'table.caption.required')).toBe(true);
  });

  it('Validator_ShouldCatchDanglingCitation', () => {
    const state = createInitialState();
    const paragraph = blockFactories.paragraph();
    const withParagraph = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: paragraph });
    const cited = editorReducer(withParagraph, { type: 'insertCitation', blockId: paragraph.id, key: 'missing' });
    expect(validateEditorState(cited).some(issue => issue.code === 'citation.targetMissing')).toBe(true);
  });

  it('Reducer_ShouldMoveBlocks', () => {
    const state = createInitialState();
    const a = blockFactories.paragraph();
    const b = blockFactories.paragraph();
    const withBlocks = editorReducer(editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: a }), { type: 'insertBlock', sectionId: 'body', block: b });
    const moved = editorReducer(withBlocks, { type: 'moveBlock', blockId: b.id, direction: 'up' });
    const body = moved.sections.find(section => section.id === 'body')!;
    expect(body.blocks.findIndex(block => block.id === b.id)).toBeLessThan(body.blocks.findIndex(block => block.id === a.id));
  });

  it('Reducer_ShouldDeleteBlock', () => {
    const state = createInitialState();
    const paragraph = blockFactories.paragraph();
    const withBlock = editorReducer(state, { type: 'insertBlock', sectionId: 'body', block: paragraph });
    const deleted = editorReducer(withBlock, { type: 'deleteBlock', blockId: paragraph.id });
    expect(JSON.stringify(deleted.sections)).not.toContain(paragraph.id);
  });

  it('Reducer_ShouldDeleteBibliographyEntry', () => {
    const state = editorReducer(createInitialState(), { type: 'addBibliographyEntry', entry: { id: 'r1', key: 'ref-a', text: 'A', entryType: 'book' } });
    const next = editorReducer(state, { type: 'deleteBibliographyEntry', key: 'ref-a' });
    expect(next.bibliography).toHaveLength(0);
  });

  it('Reducer_ShouldTrackAssets', () => {
    const state = editorReducer(createInitialState(), { type: 'addAsset', asset: { assetId: 'a', fileName: 'a.png', imagePath: '../assets/a.png', previewUrl: '/api/assets/a', contentType: 'image/png' } });
    expect(state.assets[0].assetId).toBe('a');
  });
});

function createReadyState(): ThesisEditorState {
  const state = createInitialState();
  state.documentId = 'doc-test';
  state.metadata = {
    title: '论文题目',
    subtitle: '',
    author: '作者',
    college: '学院',
    major: '专业',
    studentId: '20260001',
    advisor: '导师',
    date: '2026-05-06'
  };
  state.bibliography = [{ id: 'r1', key: 'ref-a', text: '参考文献 A', entryType: 'book' }];
  return state;
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

function readBlobText(blob: Blob) {
  if (typeof blob.text === 'function') {
    return blob.text();
  }

  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error);
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.readAsText(blob);
  });
}

async function insertTable(user: ReturnType<typeof userEvent.setup>, caption = '样例表格') {
  await user.click(screen.getAllByText('表格')[0]);
  const captionInput = await screen.findByLabelText('表名');
  await user.clear(captionInput);
  if (caption) await user.type(captionInput, caption);
  await user.click(screen.getByRole('button', { name: '创建表格' }));
}

async function insertFigure(user: ReturnType<typeof userEvent.setup>, caption = '图名待填写', alt = '') {
  await user.click(screen.getAllByText('图片')[0]);
  const captionInput = await screen.findByLabelText('图名');
  await user.clear(captionInput);
  await user.type(captionInput, caption);
  if (alt) await user.type(screen.getByLabelText('替代文本'), alt);
  await user.click(screen.getByRole('button', { name: '插入图片块' }));
}
