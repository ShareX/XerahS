---
name: xerahs-release
description: Apply XerahS bug-fix release workflow with patch-version increments and git delivery steps. Use when fixing bugs or preparing a commit/push that requires version updates.
---

# XerahS Release Workflow

1. Increment the patch version for every bug fix using the `0.0.z` patch rule (increase only `z`, keep major/minor unchanged for the current release line).
2. Update `<Version>` in every `Directory.Build.props` file in the repository so all version values match exactly.
3. Run `dotnet build XerahS.sln` and require 0 errors before committing.
4. Stage changes with `git add .`.
5. Commit with format `[vX.Y.Z] [Fix] <concise description>`.
6. Push the current branch.
