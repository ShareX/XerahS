# Plugin Development Guide

This guide explains how to create a new uploader plugin for ShareX.Avalonia.

## Prerequisites

*   .NET 10.0 SDK
*   An IDE (Visual Studio, Rider, VS Code)

## 1. Project Setup

Create a new Class Library project (`.NET 10.0`) for your plugin. The naming convention is usually `ShareX.MyPlugin.Plugin`.

### Dependencies

Your plugin needs to reference the ShareX contracts. Add a reference to the `ShareX.Avalonia.Uploaders` project (or DLL).

**Important**: You must mark shared dependencies as `<Private>false</Private>` so they are *not* copied to your plugin output directory. These assemblies are provided by the host application.

### Recommended `.csproj` Configuration

Use the following configuration in your `.csproj` file to ensure correct build output and dependency handling:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    
    <!-- Plugin Metadata -->
    <PluginId>myplugin</PluginId>
    <PluginName>My Custom Uploader</PluginName>
  </PropertyGroup>

  <ItemGroup>
    <!-- Avalonia (if you have UI) -->
    <PackageReference Include="Avalonia" Version="11.2.1" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.1" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

  <ItemGroup>
    <!-- Core Contracts (Shared Dependencies) -->
    <!-- Adjust path if referenced as project, or use NuGet package if available -->
    <ProjectReference Include="..\..\ShareX.Avalonia.Uploaders\ShareX.Avalonia.Uploaders.csproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
     <ProjectReference Include="..\..\ShareX.Avalonia.Common\ShareX.Avalonia.Common.csproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
  </ItemGroup>

  <ItemGroup>
    <None Include="plugin.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

  <!-- Post-build step to copy plugin to the app's Plugins folder for testing -->
  <Target Name="CopyToPluginsDir" AfterTargets="Build">
    <PropertyGroup>
      <PluginOutputDir>$(MSBuildThisFileDirectory)..\..\ShareX.Avalonia.App\bin\$(Configuration)\net10.0-windows\Plugins\$(PluginId)</PluginOutputDir>
    </PropertyGroup>
    <ItemGroup>
      <PluginFiles Include="$(OutputPath)**\*.*" Exclude="$(OutputPath)**\*.pdb;$(OutputPath)**\*.deps.json" />
    </ItemGroup>
    <MakeDir Directories="$(PluginOutputDir)" Condition="!Exists('$(PluginOutputDir)')" />
    <Copy SourceFiles="@(PluginFiles)" DestinationFolder="$(PluginOutputDir)\%(RecursiveDir)" SkipUnchangedFiles="true" />
  </Target>

</Project>
```

## 2. The Manifest (`plugin.json`)

Create a `plugin.json` file in the root of your project. This file tells ShareX how to load your plugin.

```json
{
  "pluginId": "myplugin",
  "name": "My Custom Uploader",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "assembly": "ShareX.MyPlugin.Plugin.dll",
  "entryPoint": "ShareX.MyPlugin.Plugin.MyProvider",
  "description": "Uploads files to MyService.",
  "author": "Your Name",
  "supportedCategories": ["Image", "File"]
}
```

*   **pluginId**: Unique identifier (folder name usually matches this).
*   **apiVersion**: Must match the host's plugin API version (currently 1.0).
*   **assembly**: The name of your plugin DLL.
*   **entryPoint**: The fully qualified name of your provider class.

## 3. Implementing the Provider

Create a class that implements `UploaderProviderBase` (or `IUploaderProvider`). This is the entry point for your plugin logic.

```csharp
using ShareX.Ava.Uploaders;
using ShareX.Ava.Uploaders.PluginSystem;
using Newtonsoft.Json;

namespace ShareX.MyPlugin.Plugin;

public class MyProvider : UploaderProviderBase
{
    public override string ProviderId => "myplugin";
    public override string Name => "My Custom Uploader";
    public override string Description => "Uploads files to MyService";
    public override Version Version => new Version(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.File };

    // This model holds your settings (API keys, etc.)
    public override Type ConfigModelType => typeof(MyConfigModel);

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<MyConfigModel>(settingsJson);
        return new MyUploader(config);
    }
    
    // Create the View for Destination Settings
    public override object? CreateConfigView()
    {
        return new Views.MyConfigView();
    }

    // Create the ViewModel for the View
    public override IUploaderConfigViewModel? CreateConfigViewModel()
    {
        return new ViewModels.MyConfigViewModel();
    }
}
```

## 4. Configuration Model

Define a simple POCO class to hold your settings.

```csharp
public class MyConfigModel
{
    public string ApiKey { get; set; } = "";
    public bool UseValidation { get; set; } = true;
}
```

## 5. Implementing the Uploader

Create a class that inherits from `FileUploader` (or `ImageUploader` / `TextUploader` depending on need).

```csharp
using ShareX.Ava.Uploaders;
using ShareX.Ava.Uploaders.FileUploaders;

public class MyUploader : FileUploader
{
    private readonly MyConfigModel _config;

    public MyUploader(MyConfigModel config)
    {
        _config = config;
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        // Implement your upload logic here
        // Use SendRequest() helper for HTTP calls
        
        // Example:
        // var response = SendRequest(HttpMethod.POST, "https://api.myservice.com/upload", stream, "file", fileName);
        
        if (LastResponseInfo.IsSuccess)
        {
            return new UploadResult 
            { 
                IsSuccess = true, 
                URL = "https://myservice.com/image.png" 
            };
        }
        
        Errors.Add("Upload failed");
        return null; // Return null on failure
    }
}
```

## 6. Creating the Configuration UI

You can include Avalonia UI views in your plugin.

**View (`MyConfigView.axaml`):**
Standard Avalonia UserControl.

**ViewModel (`MyConfigViewModel.cs`):**
Must implement `IUploaderConfigViewModel`.

```csharp
public partial class MyConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    [ObservableProperty] private string _apiKey = "";

    public void LoadFromJson(string json)
    {
        var config = JsonConvert.DeserializeObject<MyConfigModel>(json);
        if (config != null) ApiKey = config.ApiKey;
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(new MyConfigModel { ApiKey = ApiKey });
    }

    public bool Validate()
    {
        if (string.IsNullOrEmpty(ApiKey)) return false;
        return true;
    }
}
```

## 7. Build and Test

1.  Build your project.
2.  If you used the post-build event, the plugin will be copied to `ShareX.Avalonia.App/bin/Debug/net10.0-windows/Plugins/myplugin`.
3.  Run ShareX.Avalonia.
4.  Go to **Destinations -> Destination Settings**.
5.  Click **Add from Catalog**.
6.  Your plugin should appear in the list.

## Folder Structure (Deployed)

Inside `Plugins/`:

```
Plugins/
  └── myplugin/
      ├── plugin.json
      ├── ShareX.MyPlugin.Plugin.dll
      └── (Other dependencies not provided by host)
```
