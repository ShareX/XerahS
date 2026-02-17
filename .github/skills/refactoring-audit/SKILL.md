---
name: Refactoring Audit Workflow
description: A systematic workflow for identifying code pain points, planning refactoring, and creating GitHub issues.
---

# Refactoring Audit Workflow

This skill guides the agent through the process of auditing the codebase for refactoring opportunities, validating findings, drafting implementation plans, and creating GitHub issues.

## 1. Analyze Codebase

**Goal:** Identify high-value refactoring targets by looking for specific code smells.

-   **God Classes:** Look for files > 1000 lines or classes with too many responsibilities (e.g., `App.axaml.cs` handling UI, Startup, and Logic).
-   **Low Cohesion:** Look for "Utils" or "Helpers" classes that contain unrelated methods (e.g., String manipulation mixed with API calls).
-   **OCP Violations:** Look for giant `switch` statements or massive configuration classes that require modification for every new feature type (e.g., `UploadersConfig`).

**Actions:**
-   Use `list_dir` to explore `src` and subdirectories.
-   Use `view_file` to inspect largest files.
-   Use `grep_search` to find "Helper" or "Manager" classes.

## 2. Verify Findings

**Goal:** Ensure the identified "pain point" isn't already solved or constrained by design.

-   **Check for existing solutions:** Search for terms like "Bootstrapper", "Factory", "Service" to see if a better pattern already exists but isn't used.
-   **Check for constraints:** Ensure the potential refactor doesn't violate specific project architectures (e.g., Avalonia specific patterns).

## 3. Draft Implementation Plans

**Goal:** Create a structured plan for each refactoring before creating issues.

**Format:**
For each identified issue, draft a markdown section with:
1.  **Title:** `[Refactor] <Title>`
2.  **Goal Description:** What is the problem and why fix it?
3.  **Proposed Changes:** Specific classes/interfaces to create or modify.
4.  **Implementation Plan:** Checklist of steps.
5.  **Verification Plan:** How to verify the refactor (Manual/Automated).

**Action:**
-   Save these drafts to a temporary file (e.g., `refactoring_plans.md`) or the `task.md` artifact for user review.

## 4. Create GitHub Issues

**Goal:** proper tracking of the work.

**Action:**
-   Use `gh issue create` to submit the vetted plans.
-   Use the `--title` and `--body-file` arguments.
-   **Clean up:** Delete temporary body files after creation.

**Example Command:**
```bash
gh issue create --title "[Refactor] Split GeneralHelpers.cs" --body-file "c:/path/to/body.md"
```
