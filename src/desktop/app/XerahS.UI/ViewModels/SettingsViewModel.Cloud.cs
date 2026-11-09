#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core.Cloud;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        private readonly IXerahSCloudClient? _cloudClient;
        private readonly IXerahSCloudOAuthCoordinator? _cloudOAuthCoordinator;
        private Uri? _cloudSettingsUrl;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignInToCloudCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignOutOfCloudCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshCloudStatusCommand))]
        private bool _isCloudBusy;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignInToCloudCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignOutOfCloudCommand))]
        private bool _isCloudSignedIn;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignInToCloudCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshCloudStatusCommand))]
        private bool _isCloudConfigured;

        [ObservableProperty]
        private string _cloudStatusText = "XerahS Cloud is not configured for this build.";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenCloudProfileCommand))]
        [NotifyCanExecuteChangedFor(nameof(CopyCloudProfileUrlCommand))]
        private string _cloudProfileUrl = string.Empty;

        private bool CanSignInToCloud() => IsCloudConfigured && !IsCloudBusy && !IsCloudSignedIn;
        private bool CanSignOutOfCloud() => !IsCloudBusy && IsCloudSignedIn;
        private bool CanRefreshCloudStatus() => IsCloudConfigured && !IsCloudBusy;
        private bool HasCloudProfile() => Uri.TryCreate(CloudProfileUrl, UriKind.Absolute, out _);
        private bool HasCloudSettings() => _cloudSettingsUrl != null;

        private void InitializeCloudStatus()
        {
            IsCloudConfigured = _cloudClient?.IsConfigured == true;
            IsCloudSignedIn = _cloudClient?.HasSessionCredential == true;
            CloudStatusText = IsCloudConfigured
                ? IsCloudSignedIn
                    ? "A saved Cloud session is available. Refresh to verify account status."
                    : "Sign in or create an account to publish from History. Staging Cloud is enabled for this build."
                : "XerahS Cloud is disabled until the production OAuth configuration passes its launch gate.";
        }

        [RelayCommand(CanExecute = nameof(CanSignInToCloud))]
        private async Task SignInToCloudAsync()
        {
            if (_cloudClient == null || _cloudOAuthCoordinator == null)
            {
                return;
            }

            IsCloudBusy = true;
            CloudStatusText = "Opening secure sign-in in your browser...";
            try
            {
                XerahSCloudOAuthAttempt attempt = _cloudOAuthCoordinator.Begin();
                if (!PlatformServices.System.OpenUrl(attempt.AuthorizationUri.AbsoluteUri))
                {
                    CloudStatusText = "The system browser could not be opened.";
                    return;
                }

                XerahSCloudOAuthCompletion completion = await _cloudOAuthCoordinator
                    .WaitForCompletionAsync(attempt.State)
                    .ConfigureAwait(true);
                if (completion != XerahSCloudOAuthCompletion.Accepted)
                {
                    CloudStatusText = completion switch
                    {
                        XerahSCloudOAuthCompletion.Denied => "Authorization was denied.",
                        XerahSCloudOAuthCompletion.Expired => "The sign-in request expired. Try again.",
                        XerahSCloudOAuthCompletion.TokenRejected =>
                            "The returned session did not pass XerahS Cloud security checks.",
                        XerahSCloudOAuthCompletion.InvalidCallback => "The sign-in callback was invalid.",
                        _ => "The sign-in callback was unknown or had already been used."
                    };
                    return;
                }

                await LoadCloudAccountAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is XerahSCloudException or HttpRequestException)
            {
                CloudStatusText = ex.Message;
            }
            finally
            {
                IsCloudBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRefreshCloudStatus))]
        private async Task RefreshCloudStatusAsync()
        {
            if (_cloudClient == null)
            {
                return;
            }

            IsCloudBusy = true;
            try
            {
                await LoadCloudAccountAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is XerahSCloudException or HttpRequestException)
            {
                IsCloudSignedIn = false;
                CloudStatusText = ex.Message;
            }
            finally
            {
                IsCloudBusy = false;
            }
        }

        private async Task LoadCloudAccountAsync()
        {
            // Stay on the UI thread: the properties below are bound to buttons, and changing them from the
            // thread pool crashed the app right after a successful sign-in.
            XerahSCloudAccountSummary account = await _cloudClient!.GetAccountAsync().ConfigureAwait(true);
            IsCloudSignedIn = true;
            CloudProfileUrl = account.ProfileUrl.AbsoluteUri;
            _cloudSettingsUrl = account.SettingsUrl;
            string entitlement = account.CanPublish
                ? account.SubscriptionStatus ?? account.TrialStatus
                : account.DisputeSuspended
                    ? "suspended by a payment dispute"
                    : account.SubscriptionStatus ?? account.TrialStatus;
            CloudStatusText = $"Signed in as {account.Slug}. Publishing: {(account.CanPublish ? "enabled" : "disabled")} ({entitlement}).";
            OpenCloudSettingsCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanSignOutOfCloud))]
        private void SignOutOfCloud()
        {
            _cloudClient?.SignOut();
            IsCloudSignedIn = false;
            CloudProfileUrl = string.Empty;
            _cloudSettingsUrl = null;
            CloudStatusText = "Signed out on this device. Use web settings to revoke other sessions.";
            OpenCloudSettingsCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(HasCloudProfile))]
        private void OpenCloudProfile()
        {
            if (Uri.TryCreate(CloudProfileUrl, UriKind.Absolute, out Uri? profileUrl))
            {
                PlatformServices.System.OpenUrl(profileUrl.AbsoluteUri);
            }
        }

        [RelayCommand(CanExecute = nameof(HasCloudSettings))]
        private void OpenCloudSettings()
        {
            if (_cloudSettingsUrl != null)
            {
                PlatformServices.System.OpenUrl(_cloudSettingsUrl.AbsoluteUri);
            }
        }

        [RelayCommand(CanExecute = nameof(HasCloudProfile))]
        private Task CopyCloudProfileUrlAsync() =>
            PlatformServices.Clipboard.SetTextAsync(CloudProfileUrl);
    }
}
