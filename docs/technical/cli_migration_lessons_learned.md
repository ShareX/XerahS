# XerahS CLI Migration: System.CommandLine 2.0.1

**Date:** 2026-01-12
**Component:** XerahS.CLI (ShareX.Avalonia)
**Target:** System.CommandLine 2.0.1 (Stable)

## Overview

This document details the migration of the XerahS CLI from a legacy/beta version of `System.CommandLine` to the stable 2.0.1 release. The migration involved removing a reflection-based compatibility layer and refactoring all command definitions and handler bindings.

## Key Changes

| Feature | Legacy / Beta API | System.CommandLine 2.0.1 |
| :--- | :--- | :--- |
| **Command Registration** | `AddCommand(...)` / `AddOption(...)` | `Add(...)` (Unified method) |
| **Handler Binding** | `SetHandler(...)` with delegate parameters | `SetAction(parseResult => ...)` |
| **Value Retrieval** | Auto-binding to delegate arguments | Manual retrieval via `parseResult.GetValue(Option)` |
| **Invocation** | `rootCommand.InvokeAsync(args)` | `rootCommand.Parse(args).InvokeAsync()` |

## Lessons Learned & pitfalls

### 1. `SetHandler` does not exist in 2.0.1
Unlike the beta versions where `SetHandler` automatically bound command line options to delegate parameters, version 2.0.1 uses `SetAction`.

**Incorrect (Beta pattern):**
```csharp
command.SetHandler((string output) => { ... }, outputOption);
```

**Correct (2.0.1 pattern):**
```csharp
command.SetAction((parseResult) =>
{
    var output = parseResult.GetValue(outputOption);
    // ... logic ...
});
```

### 2. Manual Value Retrieval
The convenience of automatic dependency injection for handlers is not present in the same form in the core `SetAction` API. We must manually retrieve values explicitly using the `Option` or `Argument` instances.

> **Note:** We initially tried `GetValueForOption` and `ValueForOption`, but these methods were not available or failed compilation. `GetValue(Symbol)` proved to be the reliable method.

### 3. Unified `Add` Method
Separate methods like `AddCommand`, `AddOption`, and `AddArgument` have been consolidated into a single `Add` method that accepts any `Symbol`.

### 4. Invocation Pipeline
Simply calling `rootCommand.InvokeAsync(args)` proved unreliable or missing in some contexts. The robust pattern discovered is:

```csharp
// Parse first, then invoke the result
return await rootCommand.Parse(args).InvokeAsync();
```

## Migration Example

**Before (Legacy Compat Layer):**
```csharp
var screenCommand = new Command("screen", "Capture full screen");
screenCommand.AddOptionCompat(outputOption);
screenCommand.SetHandlerCompat(async (string? output) => {
    await CaptureScreenAsync(output);
}, outputOption);
```

**After (System.CommandLine 2.0.1):**
```csharp
var screenCommand = new Command("screen", "Capture full screen");
screenCommand.Add(outputOption);
screenCommand.SetAction((parseResult) => {
    var output = parseResult.GetValue(outputOption);
    Environment.ExitCode = CaptureScreenAsync(output).GetAwaiter().GetResult();
});
```
