#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.Text.Json;
using XerahS.Common;
using XerahS.Core.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.CLI.Services;

internal static class CliUploaderBootstrapper
{
    private static bool _loaded;

    public static BootstrapReport Bootstrap(bool quiet = false)
    {
        EnsureLoaded();
        InstanceManager.Instance.MigrateSecretsIfNeeded();
        var report = new BootstrapReport();

        EnsureInstance("paste2", UploaderCategory.Text, "Paste2 (Text)", report);
        EnsureInstance("custom_img_fish", UploaderCategory.Image, "img.fish (Image)", report);
        EnsureInstance("custom_img_fish", UploaderCategory.File, "img.fish (File)", report);

        RepairDefault(UploaderCategory.Text, report);
        RepairDefault(UploaderCategory.Image, report);
        RepairDefault(UploaderCategory.File, report);
        report.Diagnostics.AddRange(GetDiagnostics());

        if (!quiet) PrintReport(report);
        return report;
    }

    public static void BootstrapUploaders(bool json)
    {
        var report = Bootstrap(quiet: json);
        if (json)
        {
            WriteJson(report);
        }
    }

    public static UploadReadiness CheckUploadReadiness(string fileName, bool uploadAsText)
    {
        var report = Bootstrap(true);
        var categories = GetReadinessCategories(uploadAsText);

        foreach (var category in categories)
        {
            if (HasUsable(category, fileName)) return UploadReadiness.Ready(report, category);
        }

        return UploadReadiness.NotReady(report,
            $"No usable uploader is configured for {Path.GetFileName(fileName)}. Run 'xerahscli doctor uploaders --fix' for details.");
    }

    internal static UploaderCategory[] GetReadinessCategories(bool uploadAsText) => uploadAsText
        ? [UploaderCategory.Text]
        : [UploaderCategory.File];

    public static int DoctorUploaders(bool fix, bool json)
    {
        var report = fix ? Bootstrap(true) : Inspect();
        if (json)
        {
            WriteJson(report);
        }
        else
        {
            PrintReport(report);
        }
        return report.HasBlockingIssues ? 1 : 0;
    }

    internal static void WriteJson(BootstrapReport report)
    {
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static BootstrapReport Inspect()
    {
        EnsureLoaded();
        var report = new BootstrapReport();
        report.Diagnostics.AddRange(GetDiagnostics());
        return report;
    }

    private static void EnsureLoaded()
    {
        if (_loaded && ProviderCatalog.ArePluginsLoaded()) return;
        ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders();
        ProviderCatalog.LoadPlugins(PathsManager.GetPluginDirectories());
        _loaded = true;
    }

    private static void EnsureInstance(string providerId, UploaderCategory category, string displayName, BootstrapReport report)
    {
        var provider = ProviderCatalog.GetProvider(providerId);
        if (provider == null || !provider.SupportedCategories.Contains(category))
        {
            report.Skipped.Add($"{displayName}: provider not available");
            return;
        }

        string settingsJson = provider.GetDefaultSettings(category);
        if (!provider.ValidateSettings(settingsJson))
        {
            report.Skipped.Add($"{displayName}: default settings are not valid");
            return;
        }

        var manager = InstanceManager.Instance;
        var existing = manager.GetInstancesByCategory(category)
            .FirstOrDefault(i => string.Equals(i.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            var instance = new UploaderInstance
            {
                ProviderId = providerId,
                Category = category,
                DisplayName = displayName,
                SettingsJson = settingsJson,
                IsAvailable = true,
                FileTypeRouting = new FileTypeScope { AllFileTypes = true }
            };
            manager.AddInstance(instance);
            report.Created.Add($"{displayName} ({instance.InstanceId})");
            return;
        }

        bool changed = false;
        if (!existing.IsAvailable) { existing.IsAvailable = true; changed = true; }
        if (existing.FileTypeRouting == null || !existing.FileTypeRouting.AllFileTypes)
        {
            existing.FileTypeRouting = new FileTypeScope { AllFileTypes = true };
            changed = true;
        }
        if (changed)
        {
            manager.UpdateInstance(existing);
            report.Repaired.Add($"{existing.DisplayName} ({existing.InstanceId})");
        }
    }

    private static void RepairDefault(UploaderCategory category, BootstrapReport report)
    {
        var manager = InstanceManager.Instance;
        var preferred = manager.GetInstancesByCategory(category)
            .Where(IsUsable)
            .OrderByDescending(i => IsPreferred(i.ProviderId, category))
            .ThenByDescending(i => i.CreatedAt)
            .FirstOrDefault();
        if (preferred == null) return;

        var current = manager.GetDefaultInstance(category);
        if (!ShouldReplaceDefault(current != null, current != null && IsUsable(current),
                current != null && InstanceManager.IsAutoProvider(current.ProviderId))) return;

        manager.SetDefaultInstance(category, preferred.InstanceId);
        report.Repaired.Add($"Default {category} uploader -> {preferred.DisplayName}");
    }

    /// <summary>
    /// Only fills a missing, unusable or Auto default. A working default the user chose (for example
    /// KovaForge Docs for text) is kept; replacing it with the built-in preference silently sent
    /// uploads to a public paste site.
    /// </summary>
    internal static bool ShouldReplaceDefault(bool hasDefault, bool defaultIsUsable, bool defaultIsAuto) =>
        !hasDefault || !defaultIsUsable || defaultIsAuto;

    private static bool IsPreferred(string providerId, UploaderCategory category) => category switch
    {
        UploaderCategory.Text => string.Equals(providerId, "paste2", StringComparison.OrdinalIgnoreCase),
        UploaderCategory.Image or UploaderCategory.File => string.Equals(providerId, "custom_img_fish", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static bool HasUsable(UploaderCategory category, string fileName)
    {
        var manager = InstanceManager.Instance;
        string ext = Path.GetExtension(fileName);
        return new[] { string.IsNullOrWhiteSpace(ext) ? null : manager.GetDestinationForFile(category, ext), manager.GetDefaultInstance(category) }
            .Where(i => i != null).Cast<UploaderInstance>().Any(IsUsable);
    }

    private static bool IsUsable(UploaderInstance instance)
    {
        var provider = ProviderCatalog.GetProvider(instance.ProviderId);
        return instance.IsAvailable && provider != null && !InstanceManager.IsAutoProvider(instance.ProviderId) && provider.ValidateSettings(instance.SettingsJson);
    }

    private static IEnumerable<BootstrapDiagnostic> GetDiagnostics()
    {
        foreach (var category in new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File })
        {
            var usable = InstanceManager.Instance.GetInstancesByCategory(category).Where(IsUsable).ToArray();
            var def = InstanceManager.Instance.GetDefaultInstance(category);
            yield return new BootstrapDiagnostic(category.ToString(), usable.Length > 0 ? "ok" : "missing", def?.DisplayName,
                usable.Select(i => i.DisplayName).ToArray(), usable.Length > 0
                    ? $"{category}: {usable.Length} usable uploader(s). Default: {def?.DisplayName ?? "(none)"}."
                    : $"{category}: no usable uploader configured.");
        }

        if (ProviderCatalog.GetProvider("pastebin") != null)
        {
            yield return new BootstrapDiagnostic("Text", "info", null, [],
                "Pastebin is available but requires a user API key, so CLI bootstrap does not auto-configure it.");
        }
    }

    private static void PrintReport(BootstrapReport report)
    {
        Console.WriteLine("Uploader bootstrap/doctor");
        foreach (var item in report.Created) Console.WriteLine($"  created: {item}");
        foreach (var item in report.Repaired) Console.WriteLine($"  repaired: {item}");
        foreach (var item in report.Skipped) Console.WriteLine($"  skipped: {item}");
        foreach (var item in report.Diagnostics) Console.WriteLine($"  [{item.Status}] {item.Message}");
    }
}

internal sealed record BootstrapDiagnostic(string Category, string Status, string? DefaultInstance, string[] UsableInstances, string Message);

internal sealed class BootstrapReport
{
    public List<string> Created { get; } = [];
    public List<string> Repaired { get; } = [];
    public List<string> Skipped { get; } = [];
    public List<BootstrapDiagnostic> Diagnostics { get; } = [];
    public bool HasBlockingIssues => Diagnostics.Any(d => d.Status == "missing");
}

internal sealed record UploadReadiness(bool IsReady, BootstrapReport Report, UploaderCategory? Category, string? ErrorMessage)
{
    public static UploadReadiness Ready(BootstrapReport report, UploaderCategory category) => new(true, report, category, null);
    public static UploadReadiness NotReady(BootstrapReport report, string errorMessage) => new(false, report, null, errorMessage);
}
