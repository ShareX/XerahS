namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Interface for uploader-specific configuration ViewModels
/// </summary>
public interface IUploaderConfigViewModel
{
    void LoadFromJson(string json);
    string ToJson();
    bool Validate();
}
