---
name: template-package-system
description: Use when work involves TemplatePackage, TemplateResolver, FormatSpecMerger, template inheritance, variables, assets, or example template packages. Templates are data and must not become renderer branches keyed by school names.
---

# Template Package System

Keep template behavior declarative:

- model reusable school/college rules in `TemplatePackage` and `ThesisFormatSpec`;
- resolve inheritance through `TemplateResolver`;
- merge format specs deterministically through `FormatSpecMerger`;
- resolve variables from CLI values, thesis metadata, then defaults;
- resolve assets relative to the template that declares them;
- keep font assets metadata-only unless a reviewed embedding implementation exists.

Do not hardcode a real school or fictional school in renderer logic. If rendering needs a new behavior, add a model/schema field, example, validator coverage, tests, and docs.

