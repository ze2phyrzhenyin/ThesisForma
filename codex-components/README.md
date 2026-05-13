# Codex Components Mirror

The requested repository paths are:

- `.agents/skills/...`
- `.codex/config.toml`
- `.codex/agents/*.toml`
- `.codex/hooks/README.md`

In this execution environment, creating `.agents` and `.codex` at the repository root is blocked by tool permissions. This directory contains the same files in a writable mirror so the project content is still available for review and later placement in those official paths.

Run `scripts/install-codex-components` in an environment that allows root dot-directories.

The mirror currently includes skills for OpenXML rendering, thesis schemas, DOCX validation, regression testing, template packages, page layout templates, template validation/diff/coverage, quality workbench reports, CI quality gates, onboarding workspaces, privacy guard checks, pilot packages, real-college pilot workflows, DOCX intake extraction, thesis structure mapping, LLM structure review, and the Stage 1 vibe completion pack.

The Stage 1 completion pack lives at `codex-components/vibe-coding/stage1-completion/`. It decomposes the current high-value work into reusable implementation components with owner boundaries, write scopes, and acceptance gates.
