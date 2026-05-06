# Web Editor Design System

The editor uses a restrained academic document-tool style, not a marketing page and not a generic admin dashboard.

## Layout

- top toolbar: about 56-64px
- left outline panel: about 260px
- center editor surface: max width about 860-960px
- right side panel: about 320px

The center column uses a white paper-like surface without trying to mimic exact A4 Word layout.

## Tokens

The CSS tokens live in `web/src/styles/tokens.css`.

Core colors:

- background: `#F6F7F9`
- surface: `#FFFFFF`
- surfaceSubtle: `#FAFAFB`
- border: `#E5E7EB`
- textPrimary: `#111827`
- textSecondary: `#4B5563`
- accent: `#1F4E79`
- danger: `#B42318`
- warning: `#B54708`
- success: `#067647`

## Component Rules

Use the primitives in `web/src/components/design-system/Primitives.tsx` before adding custom controls. Keep buttons, inputs, badges, cards, panels, empty states, and modals visually consistent.

Do not add large gradients, glass effects, decorative animations, cartoon assets, or manual typography controls for thesis formatting.
