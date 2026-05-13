# Component 07: Web Performance And Bundle Shape

## Goal

Keep the structured editor responsive and make production bundles predictable as TipTap, KaTeX, table editing, and template tools grow.

## Owners

- Web editor worker.
- CI-quality worker for bundle-size gates if introduced.

## Write Scope

- `web/src/app/router.tsx`
- `web/src/pages`
- `web/src/editor`
- `web/vite.config.ts`
- `web/e2e`
- `web/README.md`

## Required Behavior

- Heavy editor subpanels and optional preview libraries are lazy-loaded where practical.
- Vite chunking separates editor, template editor, vendor editor stack, and KaTeX where beneficial.
- Build output is monitored with an explicit size budget or documented threshold.
- E2E still covers editor load, table edit, template edit, and export flow after splitting.

## Acceptance Gates

```bash
npm --prefix web run typecheck
npm --prefix web test
npm --prefix web run build
npm --prefix web run e2e
scripts/ci-quality-gate
```

Required test evidence:

- production build has no unexpected chunk warning or the warning threshold is justified;
- lazy-loaded routes/panels render in Playwright;
- no text overlap or empty loading screen in key desktop and mobile viewports.

## Boundaries

- Do not trade editor correctness for smaller bundles.
- Do not add decorative UI or marketing surfaces.

