---
name: docx-regression-testing
description: Use when working on snapshots, fixtures, baselines, DOCX unzip normalization, relationship ids, volatile properties, and regression tests.
---

# DOCX Regression Testing

DOCX snapshots must normalize volatile values:

- timestamps;
- package core-property entry ids;
- relationship ids when included;
- zip entry order;
- generated ids.

Prefer compact structural snapshots plus targeted XML assertions over enormous raw XML baselines.
