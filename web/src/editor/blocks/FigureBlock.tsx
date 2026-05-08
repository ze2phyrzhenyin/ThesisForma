import { useState } from 'react';
import { useEditorActions } from '../EditorContext';
import { BlockShell } from './BlockShell';
import { uploadImage, ThesisApiError, assetUrl, isApiBacked } from '@/api/client';
import type { FigureBlock as FigureBlockData, ImageContentType } from '@/types';
import styles from './blocks.module.css';
import figureStyles from './FigureBlock.module.css';

interface Props {
  block: FigureBlockData;
  sectionIndex: number;
  blockIndex: number;
  selected: boolean;
  totalBlocks: number;
}

const ALLOWED_TYPES: ImageContentType[] = ['image/png', 'image/jpeg', 'image/jpg', 'image/gif', 'image/bmp'];

export function FigureBlock({ block, sectionIndex, blockIndex, selected, totalBlocks }: Props) {
  const actions = useEditorActions();
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const upload = async (file: File) => {
    setError(null);
    setUploading(true);
    try {
      if (!isApiBacked) {
        const imageDataBase64 = await fileToBase64(file);
        actions.updateBlock(sectionIndex, blockIndex, (b) => {
          if (b.type !== 'figure') return;
          b.imageDataBase64 = imageDataBase64;
          delete b.imagePath;
          b.imageContentType = file.type as ImageContentType;
        });
        return;
      }
      const res = await uploadImage(file);
      actions.updateBlock(sectionIndex, blockIndex, (b) => {
        if (b.type !== 'figure') return;
        b.imagePath = res.imagePath;
        delete b.imageDataBase64;
        b.imageContentType = res.contentType as ImageContentType;
      });
    } catch (e) {
      if (e instanceof ThesisApiError) {
        setError(e.payload?.message ?? e.message);
      } else if (e instanceof Error) {
        setError(e.message);
      } else {
        setError('上传失败');
      }
    } finally {
      setUploading(false);
    }
  };

  const onDrop = (ev: React.DragEvent) => {
    ev.preventDefault();
    const file = ev.dataTransfer.files[0];
    if (file && ALLOWED_TYPES.includes(file.type as ImageContentType)) upload(file);
    else setError('请使用 PNG / JPEG / GIF / BMP 图片');
  };

  const previewSrc = block.imageDataBase64
    ? `data:${block.imageContentType};base64,${block.imageDataBase64}`
    : block.imagePath
      ? deriveAssetPreview(block.imagePath)
      : null;

  return (
    <BlockShell
      selected={selected}
      onSelect={() => actions.selectBlock(sectionIndex, blockIndex)}
      onDelete={() => actions.deleteBlock(sectionIndex, blockIndex)}
      onMoveUp={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex - 1)}
      onMoveDown={() => actions.moveBlock(sectionIndex, blockIndex, blockIndex + 1)}
      canMoveUp={blockIndex > 0}
      canMoveDown={blockIndex < totalBlocks - 1}
      badge="图"
      toolbar={
        <>
          <span className={styles.toolbarLabel}>宽度（cm）：</span>
          <input
            type="number"
            min={1}
            max={20}
            step={0.5}
            value={block.widthCm ?? ''}
            placeholder="自动"
            className={figureStyles.numInput}
            onChange={(e) =>
              actions.updateBlock(sectionIndex, blockIndex, (b) => {
                if (b.type !== 'figure') return;
                const v = e.target.valueAsNumber;
                if (Number.isFinite(v) && v > 0) b.widthCm = v;
                else delete b.widthCm;
              })
            }
          />
          <span className={styles.toolbarSeparator}>·</span>
          <span className={styles.toolbarLabel}>高度（cm）：</span>
          <input
            type="number"
            min={1}
            max={25}
            step={0.5}
            value={block.heightCm ?? ''}
            placeholder="自动"
            className={figureStyles.numInput}
            onChange={(e) =>
              actions.updateBlock(sectionIndex, blockIndex, (b) => {
                if (b.type !== 'figure') return;
                const v = e.target.valueAsNumber;
                if (Number.isFinite(v) && v > 0) b.heightCm = v;
                else delete b.heightCm;
              })
            }
          />
        </>
      }
    >
      <figure className={figureStyles.figure} onDragOver={(e) => e.preventDefault()} onDrop={onDrop}>
        {previewSrc ? (
          <div className={figureStyles.imageWrap}>
            <img
              src={previewSrc}
              alt={block.caption}
              className={figureStyles.image}
              style={{
                maxWidth: block.widthCm ? `${block.widthCm}cm` : '100%',
                maxHeight: block.heightCm ? `${block.heightCm}cm` : undefined
              }}
            />
            <button
              type="button"
              className={figureStyles.replaceBtn}
              onClick={() => document.getElementById(`fig-up-${block.id}-replace`)?.click()}
            >
              替换图片
            </button>
          </div>
        ) : (
          <label className={figureStyles.dropzone}>
            <input
              id={`fig-up-${block.id}`}
              type="file"
              accept="image/png,image/jpeg,image/gif,image/bmp"
              hidden
              onChange={(e) => {
                const f = e.target.files?.[0];
                if (f) upload(f);
              }}
            />
            <span className={figureStyles.dropzoneIcon}>↑</span>
            <strong>{uploading ? '上传中…' : '点击选择 / 拖入图片'}</strong>
            <span className={figureStyles.dropzoneHint}>PNG · JPEG · GIF · BMP · ≤ 5MB</span>
          </label>
        )}
        <input
          id={`fig-up-${block.id}-replace`}
          type="file"
          accept="image/png,image/jpeg,image/gif,image/bmp"
          hidden
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) upload(f);
          }}
        />
        <figcaption className={figureStyles.caption}>
          <span className={figureStyles.captionLabel}>题注</span>
          <input
            className={figureStyles.captionInput}
            value={block.caption}
            placeholder="图 1-1 系统架构示意"
            onChange={(e) =>
              actions.updateBlock(sectionIndex, blockIndex, (b) => {
                if (b.type !== 'figure') return;
                b.caption = e.target.value;
              })
            }
          />
        </figcaption>
        {error && <div className={figureStyles.error}>{error}</div>}
      </figure>
    </BlockShell>
  );
}

/**
 * `imagePath` returned by the API is a relative path like "../assets/asset-xxx.png".
 * For preview we need an HTTP URL. We extract the asset id from the filename.
 */
function deriveAssetPreview(imagePath: string): string | null {
  const match = imagePath.match(/(asset-[a-z0-9_-]+)/i);
  if (match) return assetUrl(match[1]);
  return null;
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const value = String(reader.result ?? '');
      const comma = value.indexOf(',');
      resolve(comma >= 0 ? value.slice(comma + 1) : value);
    };
    reader.onerror = () => reject(new Error('图片读取失败'));
    reader.readAsDataURL(file);
  });
}
