---
name: thesis-editor-design-system
description: Use when working on the web editor UI, layout, CSS tokens, visual components, interaction polish, accessibility, or responsive behavior. Emphasizes a restrained academic SaaS document-tool style.
---

# Thesis Editor Design System

Design direction:

- professional, clean, stable, academic
- three-column layout: outline, editor, side panels
- neutral background and white editor surface
- restrained accent color
- consistent 4/8/12/16/24/32 spacing
- clear focus states and accessible labels

Do not add:

- large gradients
- glassmorphism
- cartoon illustrations
- decorative animation
- noisy icon sets
- manual thesis formatting controls

Use existing primitives before creating new components:

- `Button`
- `Input`
- `Textarea`
- `Select`
- `Badge`
- `Card`
- `Panel`
- `EmptyState`
- `Modal`

Primary paths:

- `web/src/styles`
- `web/src/components/design-system`
- `web/src/components/thesis-editor`
