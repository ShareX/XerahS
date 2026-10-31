# Claude compatibility shim

Use the repository rules in [AGENTS.md](AGENTS.md). Skills in `.ai/skills/` are exposed through the `.claude/skills` symlink. If they are not listed (for example a Windows checkout without symlink support), run `grep -m1 -H '^description:' .ai/skills/*/SKILL.md` once and load only the matching skill.
