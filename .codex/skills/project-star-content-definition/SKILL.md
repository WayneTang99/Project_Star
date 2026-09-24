---
name: project-star-content-definition
description: Define or implement new Project Star gameplay content such as cards, skills, encounters, monsters, heroes, or card sets. Use when a request adds or changes formal content data and its reusable mechanics, documentation, and verification; do not use for unrelated engine, UI, Git, or bug-fix work.
---

# Project Star Content Definition

Turn a content request into a complete, reviewable Project Star definition: formal data, the smallest reusable mechanics needed to express it, content code, and focused verification.

## Before editing

1. Read `AGENTS.md`, `docs/design/GLOSSARY.md`, and the relevant formal data table.
2. Read the matching reference in this skill:
   - Cards or card sets: [references/cards.md](references/cards.md)
   - Skills: [references/skills.md](references/skills.md)
   - Encounters: [references/encounters.md](references/encounters.md)
   - Monsters or heroes: [references/actors.md](references/actors.md)
3. Inspect one or two existing definitions with similar behavior and the domain definitions they compose. Existing content is a pattern, not authority over the design documents.
4. State material ambiguities before writing. Resolve harmless formatting or naming details from project conventions, but ask when a choice changes gameplay semantics.

## Shared implementation rules

- Use the request's Chinese name as display text. Derive concise English `StringName` keys using the established prefixes: `card.`, `ability.`, `skill.`, `encounter.`, `monster.`, `hero.`, or `set.`.
- Treat slash-separated values according to the relevant data table. For cards, values start at the initial level and continue through level 4 unless the request explicitly says otherwise.
- Reuse existing `AbilityDefinition`, `EffectDefinition`, encounter effects, and other domain primitives. Add a new primitive only when composition cannot represent the requested behavior, and make that primitive reusable across multiple content definitions.
- Never put card-specific branching in the simulator, services, or UI. Domain mechanics must be driven by generic definition data.
- Keep identity in the read-only identity attributes, identifiers as `Godot.StringName`, and display text as `string`.
- Put C# and matching `.cs.uid` files under the correct `scripts/Content/` directory. Do not manually register public parameterless definitions when `DefinitionRegistry.Scan` can discover them.
- Update the matching file under `docs/design/` in the same change. Add new terminology or tags to `GLOSSARY.md` and their code-side key/display-name mappings when needed.
- Add a focused check to `scripts/Presentation/PhaseOneVerification.cs`. Verify identity and level data plus at least one observable gameplay outcome for behavioral content.
- If implementation exposes an existing defect, record it in `docs/quality/BUG_LOG.md`; do not log the absence of a newly requested feature as a bug.

## Validation

Run, in order:

1. `dotnet build Project_Star.csproj`
2. `git diff --check`
3. The project headless Godot verification command when a compatible .NET-enabled Godot editor is available.

Report which validations passed and any environment limitation precisely. Do not claim the Godot checks passed when the scene did not load or the verification entry did not execute.

## Scope control

Implement only the requested content and the minimum generic support it requires. Do not add speculative content, art, localization systems, editor tooling, balancing changes, or refactors. Git commit or push is separate work and requires an explicit user request.
