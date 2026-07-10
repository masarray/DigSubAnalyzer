# Landing Page Strategy

## Goal

Create a premium public landing page for Process Bus Insight that feels like a modern web product, while staying truthful to the product: a raw-passive IEC 61850 Process Bus engineering tool for SV, GOOSE, PTP, SCL binding, diagnostics, and evidence.

## Visual Direction

- White, calm, high-end technical aesthetic with the same identity as the WPF app.
- Navy, electric blue, and cyan are the primary colors, matching the application shell and accent system.
- Amber/red are reserved for phase, warning, and evidence accents.
- Real product screenshots should anchor the design so the page feels credible, not generic.
- Rounded glass cards, soft shadows, and clear whitespace.
- Avoid debug-console density on the public landing page.

## UX Narrative

The page should teach the product flow in this order:

1. Observe raw process-bus traffic.
2. Understand live SV, GOOSE, and PTP context.
3. Bind observed traffic to SCL expectations.
4. Diagnose missing, unexpected, mismatched, or conflicted streams.
5. Export or copy evidence for engineering use.

## Motion Strategy

Motion should feel satisfying, not distracting.

- Scroll reveal: blur + translate + scale, triggered once per section.
- Parallax: very low depth for hero and CTA scenes.
- Micro interactions: magnetic buttons, card tilt, selected audience pills.
- Scroll progress: thin electric-blue line at the top of the viewport.
- Respect `prefers-reduced-motion`.

## Content Rules

- Do not frame the app as an internal lab preview.
- Do not mention internal lab positioning on the public page.
- Do not imply certified timing measurement.
- Use honest terms:
  - raw-passive,
  - software-timestamp arrival timing screening,
  - SCL-aware validation,
  - target-aware diagnostics,
  - engineering evidence.

## Implementation

- Static page in `docs/index.html`.
- Styling in `docs/styles.css`.
- Motion and micro-interactions in `docs/app.js`.
- WebP product screenshots live under `docs/screenshot/`.
- No build dependency required.
- Suitable for GitHub Pages with `docs/` as the Pages root.
