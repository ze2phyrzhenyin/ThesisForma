# Web Editor Design System

The editor uses a restrained academic document-tool style. It should feel like a mature structured writing tool, not a Word clone, not a marketing site, and not an admin dashboard.

## Design Principles

- Structure first: the UI captures thesis content, hierarchy, references, tables, figures, and bibliography.
- Template owned formatting: fonts, font sizes, line spacing, margins, page numbers, headers, captions, and table borders are not edited in the browser.
- Low noise: neutral surfaces, clear borders, small badges, and minimal shadows.
- Chinese academic writing workflow: metadata, abstract, body sections, bibliography, acknowledgements, and appendix are first-class structures.

## Tokens

CSS variables live in `web/src/styles/tokens.css`.

Core tokens:

- `--color-bg`: `#f6f7f9`
- `--color-bg-elevated`: `#ffffff`
- `--color-bg-subtle`: `#fafafb`
- `--color-text`: `#111827`
- `--color-text-secondary`: `#374151`
- `--color-text-muted`: `#6b7280`
- `--color-border`: `#e5e7eb`
- `--color-border-strong`: `#d1d5db`
- `--color-primary`: `#1f4e79`
- `--color-primary-soft`: `#eaf2ff`
- `--color-success`: `#067647`
- `--color-warning`: `#b54708`
- `--color-danger`: `#b42318`
- radii: `6px`, `10px`, `14px`
- spacing: `4`, `8`, `12`, `16`, `20`, `24`, `32`

Legacy token aliases remain for older CSS, but new components should use the `--color-*`, `--space-*`, and `--radius-*` names.

## Layout

The editor page uses a stable three-column layout:

- Top toolbar: sticky, 60px, product/title on the left, template/mode in the center, actions on the right.
- Left outline: about 260px, shows section nodes and heading hierarchy.
- Center canvas: max width about 920px, white document surface without simulating final Word layout.
- Right panel: about 320px, tabs for Properties, Validation, References, and Template.

At widths below 1100px, the right panel stacks under the editor. At narrower widths, the outline stacks as well. Mobile is supported as usable fallback, not a full mobile authoring target.

## Components

Use `web/src/components/design-system/Primitives.tsx` before adding new primitives:

- `Button`, `IconButton`
- `Input`, `Textarea`, `Select`, `Checkbox`
- `Card`, `Panel`, `SectionHeader`
- `Badge`, `StatusPill`
- `Tabs`, `SegmentedControl`
- `Modal`, `InlineAlert`, `EmptyState`, `Tooltip`

Every component uses design tokens and has hover/focus/disabled states. Modal closes on Escape.

## Prohibited UI Patterns

- large gradients
- glass effects
- decorative blobs or cartoon illustrations
- manual thesis typography controls
- Word-like free-layout editing
- random per-page colors
- heavy shadows or large animated transitions
