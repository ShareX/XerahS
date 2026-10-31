# Universal agent workflow

This is optional background for agents working across XerahS. Root-wide rules live in `AGENTS.md`; specialized procedures belong in `.ai/skills/`.

## Plan and scope

For work that changes behavior, spans projects, or affects architecture or contributor process, make a short plan covering the goal, files, risks, and verification. Do not wait for approval unless the user requests review-first work or the design is ambiguous or high-risk. Trivial edits can skip a formal plan.

Use the graphify skill when relationship queries or unfamiliar `src/` architecture make it useful. Do not read every repository guide by default.

## Delegation

Use sub-agents when supported and useful for independent work. Assign disjoint write sets and coordinate shared files. The coordinating agent owns integration and verification; see [coordination guidance](../../docs/architecture/MULTI_AGENT_COORDINATION.md).

## Verification

Verify the smallest relevant surface and report concrete results. For normal code/config changes, the standard desktop build is:

```text
dotnet build src/desktop/XerahS.sln
```

`Directory.Build.rsp` already makes it serial (`-m:1 -nr:false`). The Roslyn compiler server stays on; turning it off (`-p:UseSharedCompilation=false`) roughly doubles build time and is only needed for the Windows file-lock workaround in `build-windows-exe`.

Do not stop a build solely because a fixed amount of time elapsed; packaging may take longer. If progress is genuinely stalled, use `.ai/skills/build-common/SKILL.md`. Keep warnings-as-errors, the explicit Windows TFM, and the centrally managed SkiaSharp versions from `AGENTS.md`.

## Lessons and documentation

Record durable lessons in `developers/lessons-learnt/` rather than expanding `AGENTS.md`. Update documentation when behavior or contributor workflow changes. Keep compatibility files such as `CLAUDE.md` and `.ai/instructions.md` thin pointers to `AGENTS.md`.
