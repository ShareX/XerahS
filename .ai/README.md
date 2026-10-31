# Agent configuration

[AGENTS.md](../AGENTS.md) holds always-on repository policy, including the Git wrapper rules. Host compatibility files point there. `.claude/skills/README.md` points Claude Code here.

- `skills/*/SKILL.md`: on-demand repository workflows. Descriptions should identify the trigger, not advertise general competence.
- `skills/*/references/` and `assets/`: supporting material loaded when needed.
- `skills/*/scripts/`: executable helpers; inspect their options and side effects before running.
- `workflows/`: task-specific procedures.

Keep host shims thin. Do not add mandatory reading chains or copy package versions into multiple instruction files. Preserve repository constraints when shortening instructions.

When maintaining skills, merge overlap or remove unused generic guidance after checking callers. Ask before removing a skill referenced by CI, published documentation, or marketplace installation paths.
