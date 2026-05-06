import { describe, expect, it } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';

describe('vercel frontend deployment configuration', () => {
  it('VercelBuild_ShouldHaveStaticOutput', () => {
    const root = process.cwd();
    const vercel = JSON.parse(fs.readFileSync(path.join(root, 'vercel.json'), 'utf8'));
    const envExample = fs.readFileSync(path.join(root, '.env.example'), 'utf8');

    expect(vercel.framework).toBe('vite');
    expect(vercel.buildCommand).toBe('npm run build');
    expect(vercel.outputDirectory).toBe('dist');
    expect(vercel.installCommand).toBe('npm ci');
    expect(envExample).toContain('VITE_APP_MODE=frontend-only');
    expect(envExample).toContain('VITE_ENABLE_DOCX_RENDER=false');
    expect(envExample).toContain('VITE_API_BASE_URL=');
  });
});
