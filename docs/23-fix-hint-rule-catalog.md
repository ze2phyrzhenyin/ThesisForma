# Fix Hint Rule Catalog

Fix hints are loaded from:

```text
resources/fix-hint-rules.json
```

Schema:

```text
schemas/fix-hint-rules.schema.json
```

Each rule maps a validator code, diff category, gate check, or diagnostic category to:

- suggested spec path;
- suggested template path;
- suggested action;
- docs reference;
- example fixture reference;
- confidence.

Fix hints are advisory. They help authors inspect the right rule or fixture, but they do not automatically repair a template and do not replace human review.
