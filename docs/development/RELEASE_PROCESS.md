# Versioning & Release Process

## Versioning Rules
1. **Automated Version Bumping**:
   - **PATCH (x.x.X)**: Bug fixes, minor refactors (Complexity ≤ 3).
   - **MINOR (x.X.x)**: New features, significant UI (Complexity 4-7).
   - **MAJOR (X.x.x)**: Breaking changes (Complexity ≥ 8).
2. **How to Bump**: Update `<Version>` in `Directory.Build.props`. Do NOT update individual `.csproj` files.

## Commit Message Standards
- Use prefixes relative to the new version: `[vX.Y.Z] [Type] Message`
- Example: `[v0.1.0] [Fix] Resolve null reference in uploader`
