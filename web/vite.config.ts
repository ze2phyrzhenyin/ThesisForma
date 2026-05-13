/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY ?? 'http://localhost:5143',
        changeOrigin: true
      }
    }
  },
  build: {
    target: 'es2022',
    sourcemap: true,
    chunkSizeWarningLimit: 650,
    rollupOptions: {
      output: {
        manualChunks(id) {
          const normalized = id.split(path.sep).join('/');
          if (normalized.includes('/node_modules/')) {
            if (normalized.includes('/@tiptap/') || normalized.includes('/prosemirror-')) {
              return 'vendor-editor-stack';
            }
            if (normalized.includes('/katex/')) {
              return 'vendor-katex';
            }
            if (normalized.includes('/@dnd-kit/')) {
              return 'vendor-dnd';
            }
            return 'vendor-app';
          }
          if (normalized.includes('/src/editor/')) {
            return 'editor';
          }
          if (normalized.includes('/src/templates/')) {
            return 'template-editor';
          }
        }
      }
    }
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/tests/setup.ts'],
    css: false,
    include: ['src/**/*.test.{ts,tsx}'],
    exclude: ['node_modules', 'dist', 'e2e']
  }
});
