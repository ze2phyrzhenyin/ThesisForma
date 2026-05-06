# Vercel Frontend Deployment

This deployment mode is the frontend MVP only. GitHub can contain the full monorepo, but Vercel should build only the `web` directory.

## Vercel Project Settings

- Root Directory: `web`
- Framework Preset: Vite
- Install Command: `npm ci`
- Build Command: `npm run build`
- Output Directory: `dist`

`web/vercel.json` contains the same settings plus a SPA rewrite to `index.html`.

## Environment Variables

For the current frontend-only MVP:

```text
VITE_APP_MODE=frontend-only
VITE_ENABLE_DOCX_RENDER=false
VITE_ENABLE_LOCAL_EXPORT=true
VITE_API_BASE_URL=
```

Leave `VITE_API_BASE_URL` empty unless a separate backend render service is deployed.

## What Works On Vercel Now

- create a structured thesis draft
- edit metadata
- add headings, paragraphs, tables, figures, references, citations, and cross references
- preview the generated outline / TOC
- run local structure validation
- save draft data in browser local storage
- import `ThesisDocument` JSON
- export `ThesisDocument` JSON

## What Is Not Deployed On Vercel Yet

- online DOCX generation
- .NET OpenXML rendering API
- persistent server-side file storage
- real user upload processing
- Microsoft Word automation

The DOCX generation button is disabled in frontend-only mode and displays a backend requirement message. To enable online DOCX generation later, deploy `src/ThesisDocx.Api` separately and set:

```text
VITE_ENABLE_DOCX_RENDER=true
VITE_API_BASE_URL=https://your-api.example.com
```

## Local Checks Before Push

Run:

```bash
scripts/pre-push-check
```

For Vercel-only verification:

```bash
scripts/vercel-build-check
```

Do not commit generated or private files from `out/`, `runtime/`, `onboarding-workspaces/`, `web/dist/`, `web/node_modules/`, or `.vercel/`.
