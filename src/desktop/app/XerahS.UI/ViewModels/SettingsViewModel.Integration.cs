#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using XerahS.Common;
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;
using XerahS.Core;
using XerahS.Core.SendTo;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        private bool _isLoadingAssistantProvider;

        // Integration Settings
        [ObservableProperty]
        private bool _isPluginExtensionRegistered;

        [ObservableProperty]
        private bool _supportsFileAssociations;

        [ObservableProperty]
        private bool _supportsContextMenuIntegration;

        [ObservableProperty]
        private bool _supportsSendToIntegration;

        public bool HasMcpApiKey => !string.IsNullOrWhiteSpace(SettingsManager.Settings.McpApiKey);

        public bool HasRememberedSendToChoices => SettingsManager.Settings.SendToRememberedChoices.Count > 0;

        public string[] SendToFolderPolicyOptions { get; } = SendToPolicyResolver.FolderPolicyOptions;

        public string[] SendToBatchPolicyOptions { get; } = SendToPolicyResolver.BatchPolicyOptions;

        public int SendToFolderPolicySelectedIndex
        {
            get => SendToPolicyResolver.ToFolderPolicyIndex(SettingsManager.Settings.SendToFolderPolicy);
            set
            {
                SendToFolderPolicy policy = SendToPolicyResolver.FromFolderPolicyIndex(value);
                if (SettingsManager.Settings.SendToFolderPolicy == policy)
                {
                    return;
                }

                SettingsManager.Settings.SendToFolderPolicy = policy;
                SettingsManager.SaveApplicationConfig();
                OnPropertyChanged();
            }
        }

        public int SendToBatchPolicySelectedIndex
        {
            get => SendToPolicyResolver.ToBatchPolicyIndex(SettingsManager.Settings.SendToBatchExecutionPolicy);
            set
            {
                SendToBatchExecutionPolicy policy = SendToPolicyResolver.FromBatchPolicyIndex(value);
                if (SettingsManager.Settings.SendToBatchExecutionPolicy == policy)
                {
                    return;
                }

                SettingsManager.Settings.SendToBatchExecutionPolicy = policy;
                SettingsManager.SaveApplicationConfig();
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSendToBatchThreshold));
            }
        }

        public int SendToBatchConfirmThreshold
        {
            get => SendToPolicyResolver.NormalizeBatchThreshold(SettingsManager.Settings.SendToBatchConfirmThreshold);
            set
            {
                int normalized = SendToPolicyResolver.NormalizeBatchThreshold(value);
                if (SettingsManager.Settings.SendToBatchConfirmThreshold == normalized)
                {
                    return;
                }

                SettingsManager.Settings.SendToBatchConfirmThreshold = normalized;
                SettingsManager.SaveApplicationConfig();
                OnPropertyChanged();
            }
        }

        public bool ShowSendToBatchThreshold =>
            SettingsManager.Settings.SendToBatchExecutionPolicy == SendToBatchExecutionPolicy.ConfirmBeforeOpeningMoreThanThreshold;

        public string RememberedSendToChoicesSummary
        {
            get
            {
                var choices = SettingsManager.Settings.SendToRememberedChoices;
                if (choices.Count == 0)
                {
                    return "No remembered Send-to choices.";
                }

                return string.Join(
                    Environment.NewLine,
                    choices
                        .OrderBy(choice => choice.Scope)
                        .Select(choice =>
                            $"{SendToPolicyResolver.FormatRememberScope(choice.Scope)}: {FormatSendToAction(choice.Action)}, " +
                            $"{SendToPolicyResolver.FormatFolderPolicy(choice.FolderPolicy)}, " +
                            $"{SendToPolicyResolver.FormatBatchPolicy(choice.BatchExecutionPolicy, choice.BatchConfirmThreshold)}"));
            }
        }

        public string McpApiKeyDisplay
        {
            get
            {
                var apiKey = SettingsManager.Settings.McpApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return "Not generated yet";
                }

                var suffixLength = Math.Min(4, apiKey.Length);
                return $"{new string('*', Math.Max(apiKey.Length - suffixLength, 0))}{apiKey[^suffixLength..]}";
            }
        }

        public string McpApiKeyStatusText => HasMcpApiKey
            ? "API key is configured. Use Copy to place the full token on the clipboard."
            : "API key has not been generated yet.";

        public string McpManifestUrl => "https://xerahs.com/.well-known/mcp/manifest.json";

        private string _assistantHotkeyStatusText = "Use the assistant shortcut to open the in-app command overlay.";
        private string _captureCommandPaletteStatusText = "Use the palette shortcut to search configured capture workflows.";

        public ObservableCollection<AssistantProviderOptionViewModel> AssistantProviderOptions { get; } = new(
            AssistantProviderCatalog.GetProviders().Select(provider => new AssistantProviderOptionViewModel(provider.Id, provider.DisplayName)));

        [ObservableProperty]
        private AssistantProviderOptionViewModel? _selectedAssistantProvider;

        [ObservableProperty]
        private string _assistantProviderModelId = string.Empty;

        [ObservableProperty]
        private string _assistantProviderBaseUrl = string.Empty;

        [ObservableProperty]
        private string _assistantProviderApiKey = string.Empty;

        [ObservableProperty]
        private string _assistantProviderStatusText = "Local commands are available without an AI provider.";

        private ObservableCollection<AssistantFeatureOptionViewModel>? _assistantFeatureOptions;

        /// <summary>
        /// One checkbox per place XerahS uses the AI provider (XerahS.Assistant.Ai.AiFeatures).
        /// </summary>
        public ObservableCollection<AssistantFeatureOptionViewModel> AssistantFeatureOptions =>
            _assistantFeatureOptions ??= new(XerahS.Assistant.Ai.AiFeatures.All.Select(feature => new AssistantFeatureOptionViewModel(feature)));

        // Every provider change (saved, validated, key removed) reports through the status text.
        partial void OnAssistantProviderStatusTextChanged(string value) => RefreshAssistantFeatureAvailability();

        private void RefreshAssistantFeatureAvailability()
        {
            foreach (AssistantFeatureOptionViewModel option in AssistantFeatureOptions)
            {
                option.RefreshAvailability();
            }
        }

        public bool AssistantEnabled
        {
            get => SettingsManager.Settings.AssistantEnabled;
            set
            {
                if (SettingsManager.Settings.AssistantEnabled == value)
                {
                    return;
                }

                SettingsManager.Settings.AssistantEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool AssistantPromptHistoryEnabled
        {
            get => SettingsManager.Settings.AssistantPromptHistoryEnabled;
            set
            {
                if (SettingsManager.Settings.AssistantPromptHistoryEnabled == value)
                {
                    return;
                }

                SettingsManager.Settings.AssistantPromptHistoryEnabled = value;
                OnPropertyChanged();
            }
        }

        public string AssistantHotkeyText => SettingsManager.Settings.AssistantHotkey.GetDisplayString();

        public bool CaptureCommandPaletteEnabled
        {
            get => SettingsManager.Settings.CaptureCommandPaletteEnabled;
            set
            {
                if (SettingsManager.Settings.CaptureCommandPaletteEnabled == value)
                {
                    return;
                }

                SettingsManager.Settings.CaptureCommandPaletteEnabled = value;
                OnPropertyChanged();
            }
        }

        public string CaptureCommandPaletteHotkeyText => SettingsManager.Settings.CaptureCommandPaletteHotkey.GetDisplayString();

        public bool AssistantProviderNeedsApiKey => SelectedAssistantProvider?.Id != "ollama";

        public bool AssistantProviderSupportsChatGptSignIn =>
            string.Equals(SelectedAssistantProvider?.Id, "openai", StringComparison.OrdinalIgnoreCase);

        public bool AssistantProviderHasChatGptSession =>
            AssistantProviderSupportsChatGptSignIn && ChatGptSignIn.IsSignedIn;

        public bool AssistantProviderHasApiKey =>
            SelectedAssistantProvider != null && AssistantProviderSecrets.HasApiKey(SelectedAssistantProvider.Id);

        public string AssistantProviderKeyStatus => AssistantProviderHasChatGptSession
            ? string.IsNullOrWhiteSpace(ChatGptSignIn.Email)
                ? "Signed in with ChatGPT. Requests use your ChatGPT plan. An API key is optional."
                : $"Signed in with ChatGPT ({ChatGptSignIn.Email}). Requests use your ChatGPT plan. An API key is optional."
            : AssistantProviderHasApiKey
                ? "API key is stored in the XerahS secret store."
                : AssistantProviderNeedsApiKey
                    ? AssistantProviderSupportsChatGptSignIn
                        ? "Not signed in. Continue with ChatGPT, or save an API key."
                        : "API key is not configured."
                    : "Ollama uses the local daemon and does not need an API key.";

        public string AssistantHotkeyStatusText
        {
            get => _assistantHotkeyStatusText;
            private set => SetProperty(ref _assistantHotkeyStatusText, value);
        }

        public string CaptureCommandPaletteStatusText
        {
            get => _captureCommandPaletteStatusText;
            private set => SetProperty(ref _captureCommandPaletteStatusText, value);
        }

        [RelayCommand]
        private void TestAssistantShortcut()
        {
            AssistantHotkeyStatusText = PlatformServices.IsInitialized
                ? $"Current shortcut: {AssistantHotkeyText}"
                : "Platform services are not initialized yet.";
        }

        [RelayCommand]
        private void TestCaptureCommandPaletteShortcut()
        {
            CaptureCommandPaletteStatusText = PlatformServices.IsInitialized
                ? $"Current shortcut: {CaptureCommandPaletteHotkeyText}"
                : "Platform services are not initialized yet.";
        }

        [RelayCommand]
        private void SaveAssistantProviderSettings()
        {
            if (SelectedAssistantProvider == null)
            {
                AssistantProviderStatusText = "Select a provider first.";
                return;
            }

            SaveSelectedAssistantProviderSettings();
            AssistantProviderStatusText = $"{SelectedAssistantProvider.DisplayName} is the active assistant provider.";
        }

        [RelayCommand]
        private async Task ValidateAssistantProviderAsync()
        {
            if (SelectedAssistantProvider == null)
            {
                AssistantProviderStatusText = "Select a provider first.";
                return;
            }

            SaveSelectedAssistantProviderSettings();
            if (!AssistantProviderSettingsResolver.TryGetActive(out AssistantProviderRuntimeSettings runtimeSettings))
            {
                AssistantProviderStatusText = AssistantProviderSupportsChatGptSignIn
                    ? "Sign in with ChatGPT or save an API key before validating this provider."
                    : AssistantProviderNeedsApiKey
                        ? "Save an API key before validating this provider."
                        : "Provider settings are incomplete.";
                return;
            }

            AssistantProviderStatusText = $"Validating {SelectedAssistantProvider.DisplayName}...";
            IAssistantModelProvider provider = AssistantModelProviderFactory.Create(runtimeSettings);
            AssistantModelResult result = await provider.ValidateAsync(runtimeSettings.ModelId, CancellationToken.None);

            if (result.Kind == AssistantModelResultKind.Text)
            {
                AssistantProviderConfig config = AssistantProviderSettingsResolver.GetOrCreateConfig(runtimeSettings.Metadata.Id);
                config.LastValidatedAt = DateTime.UtcNow;
                SettingsManager.SaveApplicationConfig();
                AssistantProviderStatusText = $"{SelectedAssistantProvider.DisplayName} validated successfully.";
            }
            else
            {
                AssistantProviderStatusText = result.Text ?? "Provider validation failed.";
            }
        }

        [RelayCommand]
        private void RemoveAssistantProviderKey()
        {
            if (SelectedAssistantProvider == null)
            {
                return;
            }

            AssistantProviderSecrets.DeleteApiKey(SelectedAssistantProvider.Id);
            AssistantProviderApiKey = string.Empty;
            AssistantProviderStatusText = $"{SelectedAssistantProvider.DisplayName} API key removed.";
            NotifyAssistantProviderSecretProperties();
        }

        [RelayCommand]
        private async Task SignInWithChatGptAsync()
        {
            if (!AssistantProviderSupportsChatGptSignIn || SelectedAssistantProvider == null)
            {
                return;
            }

            SaveSelectedAssistantProviderSettings();
            AssistantProviderStatusText = "Waiting for ChatGPT in the browser...";
            try
            {
                string email = await ChatGptSignIn.SignInAsync(CancellationToken.None);
                AssistantProviderStatusText = string.IsNullOrWhiteSpace(email)
                    ? "Signed in with ChatGPT."
                    : $"Signed in with ChatGPT as {email}.";
            }
            catch (ChatGptSignInException ex)
            {
                AssistantProviderStatusText = ex.Message;
            }
            catch (Exception)
            {
                AssistantProviderStatusText = "ChatGPT sign-in failed.";
            }

            NotifyAssistantProviderSecretProperties();
        }

        [RelayCommand]
        private void SignOutOfChatGpt()
        {
            ChatGptSignIn.SignOut();
            AssistantProviderStatusText = "Signed out of ChatGPT.";
            NotifyAssistantProviderSecretProperties();
        }

        partial void OnSelectedAssistantProviderChanged(AssistantProviderOptionViewModel? value)
        {
            if (_isLoadingAssistantProvider || value == null)
            {
                return;
            }

            LoadSelectedAssistantProviderSettings(value.Id);
        }

        private void LoadAssistantProviderSettings()
        {
            string providerId = string.IsNullOrWhiteSpace(SettingsManager.Settings.AssistantActiveProviderId)
                ? "openai"
                : SettingsManager.Settings.AssistantActiveProviderId;

            SelectedAssistantProvider = AssistantProviderOptions.FirstOrDefault(provider =>
                string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase)) ?? AssistantProviderOptions.FirstOrDefault();

            if (SelectedAssistantProvider != null)
            {
                LoadSelectedAssistantProviderSettings(SelectedAssistantProvider.Id);
            }
        }

        private void LoadSelectedAssistantProviderSettings(string providerId)
        {
            _isLoadingAssistantProvider = true;

            AssistantProviderMetadata? metadata = AssistantProviderCatalog.Find(providerId);
            AssistantProviderConfig config = AssistantProviderSettingsResolver.GetOrCreateConfig(providerId);
            AssistantProviderModelId = string.IsNullOrWhiteSpace(config.ModelId) ? metadata?.DefaultModelId ?? string.Empty : config.ModelId;
            AssistantProviderBaseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? metadata?.DefaultBaseUrl ?? string.Empty : config.BaseUrl;
            AssistantProviderApiKey = string.Empty;
            AssistantProviderStatusText = string.Equals(SettingsManager.Settings.AssistantActiveProviderId, providerId, StringComparison.OrdinalIgnoreCase)
                ? $"{metadata?.DisplayName ?? providerId} is active."
                : $"{metadata?.DisplayName ?? providerId} is selected. Save to make it active.";

            _isLoadingAssistantProvider = false;
            NotifyAssistantProviderSecretProperties();
        }

        private void SaveSelectedAssistantProviderSettings()
        {
            if (SelectedAssistantProvider == null)
            {
                return;
            }

            AssistantProviderConfig config = AssistantProviderSettingsResolver.GetOrCreateConfig(SelectedAssistantProvider.Id);
            config.ModelId = AssistantProviderModelId.Trim();
            config.BaseUrl = AssistantProviderBaseUrl.Trim().TrimEnd('/');
            SettingsManager.Settings.AssistantActiveProviderId = SelectedAssistantProvider.Id;

            if (AssistantProviderNeedsApiKey && !string.IsNullOrWhiteSpace(AssistantProviderApiKey))
            {
                AssistantProviderSecrets.SetApiKey(SelectedAssistantProvider.Id, AssistantProviderApiKey);
                AssistantProviderApiKey = string.Empty;
            }

            SettingsManager.SaveApplicationConfig();
            NotifyAssistantProviderSecretProperties();
        }

        private void NotifyAssistantProviderSecretProperties()
        {
            OnPropertyChanged(nameof(AssistantProviderNeedsApiKey));
            OnPropertyChanged(nameof(AssistantProviderSupportsChatGptSignIn));
            OnPropertyChanged(nameof(AssistantProviderHasChatGptSession));
            OnPropertyChanged(nameof(AssistantProviderHasApiKey));
            OnPropertyChanged(nameof(AssistantProviderKeyStatus));
        }

        partial void OnIsPluginExtensionRegisteredChanged(bool value)
        {
            if (_isLoading) return; // Don't trigger during initial load

            try
            {
                PlatformServices.ShellIntegration.SetPluginExtensionRegistration(value);
            }
            catch (InvalidOperationException)
            {
                // Shell integration not available on this platform
            }
        }

        [RelayCommand]
        private async Task CopyMcpApiKeyAsync()
        {
            var apiKey = SettingsManager.Settings.McpApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                GenerateMcpApiKey();
                apiKey = SettingsManager.Settings.McpApiKey;
            }

            await PlatformServices.Clipboard.SetTextAsync(apiKey);
        }

        [RelayCommand]
        private async Task CopyMcpManifestUrlAsync()
        {
            await PlatformServices.Clipboard.SetTextAsync(McpManifestUrl);
        }

        [RelayCommand]
        private void GenerateMcpApiKey()
        {
            SettingsManager.Settings.McpApiKey = CreateMcpApiKey();
            SettingsManager.SaveApplicationConfig();
            NotifyMcpApiKeyChanged();
        }

        [RelayCommand]
        private void ClearRememberedSendToChoices()
        {
            SettingsManager.Settings.SendToRememberedChoices.Clear();
            SettingsManager.SaveApplicationConfig();
            NotifyRememberedSendToChoicesChanged();
        }

        private void NotifyMcpApiKeyChanged()
        {
            OnPropertyChanged(nameof(HasMcpApiKey));
            OnPropertyChanged(nameof(McpApiKeyDisplay));
            OnPropertyChanged(nameof(McpApiKeyStatusText));
        }

        private void NotifyRememberedSendToChoicesChanged()
        {
            OnPropertyChanged(nameof(HasRememberedSendToChoices));
            OnPropertyChanged(nameof(RememberedSendToChoicesSummary));
        }

        private static string FormatSendToAction(SendToAction action) => action switch
        {
            SendToAction.UploadNow => "Upload now",
            SendToAction.OpenUploadContent => "Open in Upload Content",
            SendToAction.OpenImageEditor => "Open in Image Editor",
            SendToAction.PinToScreen => "Pin to Screen",
            SendToAction.IndexFolders => "Index folders",
            SendToAction.Cancel => "Cancel",
            _ => action.ToString()
        };

        private static string CreateMcpApiKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(24);
            return Convert.ToBase64String(bytes)[..32];
        }

        private static bool ApplyContextMenuPreference(bool enable)
        {
            try
            {
                if (!PlatformServices.IsInitialized)
                {
                    return false;
                }

                IShellIntegrationService? shellIntegration = PlatformServices.GetShellIntegrationIfAvailable();
                if (shellIntegration == null || !shellIntegration.SupportsContextMenuIntegration)
                {
                    return !enable;
                }

                return shellIntegration.SetContextMenuIntegration(enable);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "SettingsViewModel: ContextMenu platform services not ready.");
                return false;
            }
        }

        private static bool ApplySendToPreference(bool enable)
        {
            try
            {
                if (!PlatformServices.IsInitialized)
                {
                    return false;
                }

                IShellIntegrationService? shellIntegration = PlatformServices.GetShellIntegrationIfAvailable();
                if (shellIntegration == null || !shellIntegration.SupportsSendToIntegration)
                {
                    return !enable;
                }

                return shellIntegration.SetSendToIntegration(enable);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "SettingsViewModel: SendTo platform services not ready.");
                return false;
            }
        }

        private static bool ApplyStartupPreference(bool enable)
        {
            try
            {
                if (!PlatformServices.IsInitialized)
                {
                    return false;
                }

                return PlatformServices.Startup.SetRunAtStartup(enable);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "SettingsViewModel: RunAtStartup platform services not ready.");
                return false;
            }
        }
    }

    public sealed record AssistantProviderOptionViewModel(string Id, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
