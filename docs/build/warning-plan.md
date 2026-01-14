# Warning Reduction Plan

**Status**: Baseline rebuild recorded on `develop`; warnings are treated as errors by the SDK, so every nullability warning reported by the compiler shows up as a build-breaking error.

## Baseline rebuild (2026-01-14)

- Command: `dotnet build ShareX.Avalonia.sln`
- Result: 184 errors (0 warnings) because TreatWarningsAsErrors is enabled for nullable diagnostics.
- Log: `warnings.log`

### Error counts by code

```
Count Code
----- ----
  62 CS8600
  62 CS8603
  24 CS8602
  16 CS8604
   6 CS8605
   6 CS8618
   2 CS0414
   2 CS8601
   2 CS8625
   2 SYSLIB0013
```

### Error counts by project

```
Count Project
----- -------
 184 ShareX.Avalonia.Common (ShareX.Avalonia.Common.csproj)
```

## Batch plan

1. **CS8618** – ensure all non-nullable fields/properties are initialized (6 errors).
2. **CS8600** – fix nullable-to-non-nullable conversions (62 errors).
3. **CS8603** – guard methods that currently return null (62 errors).
4. **CS8602** – add null checks before dereferencing (24 errors).
5. **CS8604, CS8605, CS8625, CS8601** – address the remaining null inputs, unboxing, and assignments (12 errors).
6. **SYSLIB0013** – replace `Uri.EscapeUriString` usage (2 errors).
7. **CS0414** – remove or use the unused field (2 errors).

Update this plan and counts after each commit that reduces the total error tally; include summary of what was fixed and the new totals.

## Progress log

- 2026-01-14: Baseline recorded (184 errors in ShareX.Avalonia.Common). Next target: CS8618 constructors.
