# Plugin System Implementation Plan

**Status**: ✅ Implemented
**Namespace**: `ShareX.Avalonia.Uploaders.PluginSystem`

## 1. Objective
Create a modular, extensible plugin system for ShareX.Avalonia that allows adding new uploader providers (and potentially other components) without modifying the core application. The system must support dynamic loading, versioning, and isolation of dependencies.

## 2. Architecture Overview

```mermaid
graph TD
    A[App Startup] --> B[ProviderCatalog]
    B --> C[PluginDiscovery]
    C -->|Scan Directory| D[plugin.json Files]
    
    subgraph Plugin Loading
    E[PluginLoader] -->|Create Context| F[PluginLoadContext]
    F -->|Load Assembly| G[Plugin Assembly (DLL)]
    G -->|Instantiate| H[IUploaderProvider]
    end
    
    C -->|List Metadata| E
    H -->|Register| B
    
    I[Application] -->|Request Provider| B
    B -->|Return Instance| H
```

## 3. Core Components

### A. Core Interfaces
*   **`IUploaderPlugin`**: The base interface that all plugins must implement. Defines metadata (Id, Name, Version) and lifecycle methods.
*   **`IUploaderProvider`**: Specialized interface for uploader plugins. Handles configuration and uploader instance creation.

### B. System Components
1.  **`ProviderCatalog`** (`ProviderCatalog.cs`)
    *   **Role**: Central registry and entry point.
    *   **Function**: Orchestrates discovery and loading. Maintains a thread-safe dictionary of available providers.
    *   **API**: `GetProvider(id)`, `GetAllProviders()`, `LoadPlugins(path)`.

2.  **`PluginDiscovery`** (`PluginDiscovery.cs`)
    *   **Role**: Scanner and Validator.
    *   **Function**: Scans a specific directory (recursive) for `plugin.json` manifest files.
    *   **Validation**: Checks API version compatibility and manifest integrity before attempting to load code.

3.  **`PluginLoader`** (`PluginLoader.cs`)
    *   **Role**: Assembly Loader.
    *   **Function**: Loads plugin assemblies into isolated `PluginLoadContext` (ALC) to prevent dependency conflicts between plugins.
    *   **Instantiation**: Uses reflection to find the entry point defined in `plugin.json` and creates the provider instance.

4.  **`PluginLoadContext`** (`PluginLoadContext.cs`)
    *   **Role**: Runtime Isolation.
    *   **Technique**: Inherits from `.NET AssemblyLoadContext`.
    *   **Benefit**: Allows plugins to use different versions of the same library (e.g., Newtonsoft.Json) without crashing the main app.

## 4. Plugin Structure

A valid plugin consists of a folder containing at least two files:

1.  **`plugin.json`** (Manifest)
    ```json
    {
      "id": "ShareX.Uploader.Imgur",
      "name": "Imgur Uploader",
      "version": "1.0.0",
      "apiVersion": "1.0",
      "assembly": "ShareX.Uploader.Imgur.dll",
      "entryPoint": "ShareX.Uploader.Imgur.ImgurPlugin",
      "description": "Upload images to Imgur anonymously or with account."
    }
    ```

2.  **`ShareX.Uploader.Imgur.dll`** (Assembly)
    *   Must reference `ShareX.Avalonia.Uploaders` (for interfaces).
    *   Must contain a class implementing `IUploaderProvider` matching the `entryPoint`.

## 5. Implementation Details

### Discovery Process
1.  System looks for `Plugins/` directory in app root.
2.  Iterates all subdirectories.
3.  Parses `plugin.json`.
4.  Validates `apiVersion` matches host application.

### Loading Process
1.  `PluginLoader` creates a new `PluginLoadContext` for the plugin path.
2.  Loads the assembly specified in `assembly` field.
3.  Scans exported types to find the `entryPoint`.
4.  Verifies the type implements `IUploaderProvider`.
5.  Instantiates the type via `Activator.CreateInstance`.
6.  Registers the instance in `ProviderCatalog`.

### Dependencies
*   Plugins can ship their own dependencies (DLLs) in their folder.
*   `PluginLoadContext` resolves dependencies from the plugin folder first.
*   Shared types (like `IUploaderProvider`) are loaded from the Host Context to ensure type compatibility.

## 6. Future Improvements (Roadmap)
*   [ ] **Unloading**: Support for unloading plugins at runtime (experimental support exists in `PluginLoader`).
*   [ ] **Updates**: Mechanism to update plugins from an online repository.
*   [ ] **Configuration UI**: Auto-generate settings UI from `IUploaderProvider` configuration definitions.
*   [ ] **Sandboxing**: stricter security controls (currently plugins run with full trust).
