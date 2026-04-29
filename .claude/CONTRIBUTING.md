# Contributing Claude Context to This Repo

Every time you catch Claude making the same mistake twice, explain the same convention in chat, or hand a teammate a mental map they didn't have — that's knowledge worth encoding. This guide covers what belongs in this repo's `.claude/`, where to put it, and how to land it alongside the code it describes.

## When to contribute here vs. elsewhere

Ask: **is this knowledge specific to this codebase, or generic enough to work across repos?**

- **Specific to this codebase** → contribute here, in `.claude/`.
  Example: "how we add a new cipher type," "how our feature-flag system works."
- **Generic, reusable across repos** → [`bitwarden/ai-plugins`](https://github.com/bitwarden/ai-plugins) — persona plugins (e.g., a code-review agent), tool integrations, or shared utilities.

When unsure, keep it here. Promoting up to `ai-plugins` later is easier than pulling it back — see its [CONTRIBUTING.md](https://github.com/bitwarden/ai-plugins/blob/main/CONTRIBUTING.md) when you're ready.

## Choose scope, then shape

### 1. Scope — where does it apply?

This is a monorepo. Claude loads every `CLAUDE.md` and `CLAUDE.local.md` by [walking up from the working directory](https://code.claude.com/docs/en/memory#how-claude-md-files-load) — looking in each ancestor directly, not in a nested `.claude/` subdirectory. Files below the working directory (including nested `.claude/skills/`) are loaded lazily when Claude reads into that subtree. Use that hierarchy:

- **Applies everywhere in this repo** → root `CLAUDE.md` or `.claude/skills/`
- **Applies only within one app, library, utility, or subtree** → nested `CLAUDE.md` or `.claude/skills/` in that directory

Push rules as deep as they'll go — keeping app-specific rules local saves context for everyone else's sessions, not just yours.

For rules that should apply only to certain file types (e.g., all `*Controller.cs` files), use [`.claude/rules/<name>.md` with a `paths:` frontmatter glob](https://code.claude.com/docs/en/memory#organize-rules-with-claude/rules/) instead of a nested `CLAUDE.md`.

### 2. Shape — how should Claude use it?

| You want to…                                            | Use                                                                                              |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| State a rule Claude must always follow in its scope     | `CLAUDE.md`                                                                                      |
| State a rule that applies only to certain file globs    | `.claude/rules/<name>.md` with `paths:` frontmatter                                              |
| Teach a procedure Claude invokes on demand              | `.claude/skills/<name>/SKILL.md`                                                                 |
| Give Claude a specialized subagent with its own context | `.claude/agents/<name>.md` (YAML frontmatter; `name` + `description` required)                   |
| Add a user-invocable slash command                      | `.claude/commands/<name>.md`                                                                     |
| Trigger a shell script on a Claude Code event           | _We have them, but no strict project enforcement yet — register yours in `settings.local.json`._ |

Rule of thumb: **if Claude only needs it sometimes, it's a skill.** Once a `CLAUDE.md` loads, it stays in context for the rest of the session — keep each one lean, especially the root.

## Security conventions

Skills and agents that touch vault data, authentication, or cryptography must use Bitwarden's [Core Vocabulary](https://contributing.bitwarden.com/architecture/security/definitions) (Vault Data, Protected Data, Secure Channel, etc.) and re-state the zero-knowledge invariant inline. **Subagents run in a fresh context** and do not inherit this repo's `CLAUDE.md` — include the relevant definitions directly in the agent's system prompt.

## What good contributions look like

- **Grounded in the code.** Real files, real patterns, real commands.
  If it could apply to any repo, it belongs in `ai-plugins`.
- **Describes the "what" and "why," not the "who."**
  Avoid team-persona framing. Describe the domain and its constraints; the team is an implementation detail.
- **Short and specific.**
  2,000 words of general advice isn't a skill.
- **Active voice, direct language.**
  "Invoke this skill when..." — not "This skill may be invoked when..."
- **Reviewed like code.**
  Teams of domain experts own `.claude/` in their areas — they're the ones shaping how Claude behaves for everyone who works there, so treat changes with the same seriousness as source.

## Anti-patterns

- **Team-persona agents** ("Team ABC engineer").
  If a team's process is unique enough to warrant a persona, that's an SDLC signal to address, not a persona to encode.
- **Root-level rules that only matter in one subtree.**
  If it applies to `util/Seeder` only, put it in `util/Seeder/CLAUDE.md`.
- **Duplicating `ai-plugins` content.**
  Check existing plugin skills before writing a new one.
- **Generic advice disguised as repo-local knowledge.**
  "Write good tests" isn't repo-specific.
  "Our integration tests must hit a real database because…" is.

## Building a contribution

The Claude Code ecosystem moves fast — last session's habits may already be out of date. Here's the workflow we follow.

### 1. Start with the canonical docs

A quick refresh before you begin goes a long way — the rules shift more often than you'd think:

- [How Claude Code Works](https://code.claude.com/docs/en/how-claude-code-works) — the mental model.
- [Best Practices for Claude Code](https://code.claude.com/docs/en/best-practices) — what Anthropic recommends.
- [Extend Claude Code](https://code.claude.com/docs/en/features-overview) — what you can build (skills, agents, commands, hooks).
- [The Complete Guide to Building Skills for Claude](https://resources.anthropic.com/hubfs/The-Complete-Guide-to-Building-Skill-for-Claude.pdf) - a must read for skill building

### 2. Survey the landscape

A quick skim of both goes a long way:

- This repo's [`.claude/`](.) tree.
- [`bitwarden/ai-plugins`](https://github.com/bitwarden/ai-plugins).

Try to match the voice you see. "Invoke when the user asks to X" — not "This skill may be invoked when X." Direct, active, specific. Your contribution should read like the neighbors.

### 3. Build iteratively

When you're authoring a skill, start with `/skill-creator:skill-creator`. It runs an iterative loop — draft → test against evals → review outputs → refine — with benchmark stats and a side-by-side reviewer. You end up with a skill that's been exercised against concrete inputs before you open the PR.

For agents, commands, hooks, and `CLAUDE.md` entries, start from an existing one in the repo and adapt it. No need to invent a new structure when a neighbor already solves the shape problem.

### 4. Validate before you push

- Run a local Bitwarden Claude Code review with `/bitwarden-code-review:code-review-local` — it writes findings to files so you can fix them before pushing, without posting anything to GitHub.
- When you raise the PR, apply the `ai-review` label. Our reusable GitHub workflow watches for it and runs a Claude Code review automatically; without the label, the review doesn't fire.
