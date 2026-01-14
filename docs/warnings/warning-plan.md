# Warning Reduction Plan

Baseline rebuild: `dotnet build /t:Rebuild` (Debug) produced **1180 warnings**.

## Baseline counts by code

```
Count Code
----- ----
 682  CS8618
 484  CA1416
 318  CS8625
 264  CS8603
 234  CS8600
  90  CS8601
  72  CS8602
  70  CS8765
  66  CS8604
  16  CS0618
   8  CS0067
   8  CS8605
   6  CS0649
   6  CS0105
   4  CS8767
   4  CS8714
   4  CS0414
   4  CS0465
   2  CS0169
   2  CS0162
   2  CS9191
   2  WFDEV005
   2  CS0108
   2  CA2022
   2  SYSLIB0013
   2  CS8123
   2  CS0219
   2  SYSLIB0060
```

## Batch plan

- Batch 1 - Nullability initialisation (CS8618) in DTOs/settings/viewmodels: **done** (models/settings/viewmodels initialised)
- Batch 2 - Optional parameter nullability (CS8625/CS8604/CS8601): **done** (helpers, crypto, registry, zip)
- Batch 3 - Null flow returns/dereferences (CS8600/CS8602/CS8603): **done** (core call sites tightened)
- Batch 4 - Platform compatibility guards (CA1416): **done** (Windows-only code annotated/guarded)
- Batch 5 - Obsolete APIs and analyser warnings (CS0618/SYSLIB*/CA2022/WFDEV005): **done** (Avalonia drag-drop/storage + native messaging read)
- Batch 6 - Cleanup remaining small sets (CS0067/CS0649/etc): **in progress** (common utilities nullability cleanup)

## Latest rebuild (develop)

- Command: `dotnet build /t:Rebuild /p:TreatWarningsAsErrors=false`
- Warnings: **538**
- Log: `warnings.log`

### Current counts by code

```
Count Code
----- ----
 292  CS8618
 212  CS8600
 176  CS8603
 104  CS8602
  86  CS8601
  82  CS8625
  62  CS8604
  10  CS8605
   8  CS0067
   6  CS0649
   6  CS0105
   4  CS0465
   4  CS8714
   4  CS0414
   2  CS0169
   2  CS0162
   2  CS9191
   2  WFDEV005
   2  CS0108
   2  CS8766
   2  SYSLIB0013
   2  CS8123
   2  CS0219
   2  SYSLIB0060
```
