# PluginExporter CLI

Create a `.sxadp` plugin package from a plugin project folder.

## Usage

```bash
dotnet run --project src/ShareX.Avalonia.PluginExporter -- <pluginDirectory> [outputPath]
dotnet run --project src/ShareX.Avalonia.PluginExporter -- <pluginDirectory> -o <outputPath>
```

## Arguments

- `pluginDirectory`: Path to the plugin folder that contains `plugin.json`.
- `outputPath`: Optional path for the output `.sxadp` file.
  - If a directory is provided, the file is created inside it using the folder name.
  - If a file path is provided without an extension, `.sxadp` is appended.
  - If omitted, the package is created in the current directory.

## Examples

```bash
dotnet run --project src/ShareX.Avalonia.PluginExporter -- "C:\Path\To\ShareX.AmazonS3.Plugin"
```

```bash
dotnet run --project src/ShareX.Avalonia.PluginExporter -- "C:\Path\To\ShareX.AmazonS3.Plugin" -o "C:\Output\ShareX.AmazonS3.Plugin.sxadp"
```

```bash
dotnet run --project src/ShareX.Avalonia.PluginExporter -- "C:\Path\To\ShareX.AmazonS3.Plugin" -o "C:\Output"
```

## Notes

- Packaging fails if `plugin.json` is missing or invalid.
- Packages larger than 100 MB are rejected.
