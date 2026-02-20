---
description: Automatically sync git submodules to the latest commit on their tracked branch
---

This workflow ensures that all git submodules (specifically ImageEditor) are updated to the latest commit on their remote branch, preventing detached head states or outdated references.

1.  **Check Submodule Status**
    Run `git submodule status` to see current commits.

2.  **Update Submodules to Remote HEAD**
    // turbo
    Run the following command to fetch and checkout the latest commit for each submodule:
    ```bash
    git submodule update --remote --merge
    ```

3.  **Check for Changes**
    Run `git status` in the root repository.

4.  **Commit Updates (If any)**
    If there are changes in the submodule references:
    ```bash
    git add .
    git commit -m "chore: auto-sync submodules to latest remote HEAD"
    ```
