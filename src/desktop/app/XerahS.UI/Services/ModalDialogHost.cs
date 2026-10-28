#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ShareX.ImageEditor.Presentation.ViewModels;
using XerahS.Common;

namespace XerahS.UI.Services;

/// <summary>
/// Hosts dialog view-models in <see cref="MainViewModel.ModalContent"/> (Add-from-Catalog pattern).
/// Completes awaiters on VM close or overlay dismiss (backdrop / Escape / CloseModal).
/// </summary>
public static class ModalDialogHost
{
    public static Task<T> ShowAsync<T>(
        object viewModel,
        Action<Action<T>> assignCloseCallback,
        T dismissResult,
        string debugSource)
    {
        var mainVm = ModalOpenService.ResolveHostViewModel();
        if (mainVm == null)
        {
            DebugHelper.WriteLine($"[{debugSource}] ModalDialogHost: host MainViewModel is null");
            return Task.FromResult(dismissResult);
        }

        EnsureMainWindowVisible();

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        // A modal opened from another modal (e.g. the effect browser from the workflow editor)
        // hands the overlay back to it on close instead of closing the whole overlay.
        var parent = mainVm.ModalContent;

        void Complete(T result)
        {
            mainVm.PropertyChanged -= OnModalPropertyChanged;
            if (mainVm.ModalContent == viewModel)
            {
                if (parent != null)
                    mainVm.ModalContent = parent;
                else
                    mainVm.CloseModalCommand.Execute(null);
            }
            tcs.TrySetResult(result);
        }

        void OnModalPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsModalOpen) &&
                !mainVm.IsModalOpen &&
                !tcs.Task.IsCompleted)
            {
                mainVm.PropertyChanged -= OnModalPropertyChanged;
                tcs.TrySetResult(dismissResult);
            }
        }

        assignCloseCallback(Complete);
        mainVm.PropertyChanged += OnModalPropertyChanged;
        ModalOpenService.Open(mainVm, viewModel, debugSource);
        return tcs.Task;
    }

    public static Task ShowUntilClosedAsync(
        object viewModel,
        Action<Action> assignCloseCallback,
        string debugSource)
    {
        return ShowAsync(
            viewModel,
            set => assignCloseCallback(() => set(true)),
            dismissResult: true,
            debugSource);
    }

    public static void EnsureMainWindowVisible()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var main = desktop.MainWindow;
        if (main == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!main.IsVisible)
                main.Show();
            if (main.WindowState == WindowState.Minimized)
                main.WindowState = WindowState.Normal;
            main.Activate();
        }, DispatcherPriority.Send);
    }
}
