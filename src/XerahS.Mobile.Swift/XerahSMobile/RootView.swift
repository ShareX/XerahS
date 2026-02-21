//
//  RootView.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI

private enum AppPhase {
    case loading
    case main
}

struct RootView: View {
    @EnvironmentObject var appState: AppState
    @State private var phase: AppPhase = .loading
    @State private var navPath: [Screen] = []
    @State private var copyFeedback = false

    private func copyToClipboard(_ text: String) {
        UIPasteboard.general.string = text
        copyFeedback = true
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { copyFeedback = false }
    }

    var body: some View {
        Group {
            if phase == .loading {
                LoadingScreen { phase = .main }
            } else {
                mainNav
            }
        }
        .overlay {
            if copyFeedback {
                Text("Copied")
                    .padding(.horizontal, 16)
                    .padding(.vertical, 8)
                    .background(.ultraThinMaterial, in: Capsule())
                    .transition(.opacity)
            }
        }
        .animation(.easeInOut(duration: 0.2), value: copyFeedback)
    }

    private var mainNav: some View {
        NavigationStack(path: $navPath) {
            uploadRoot
                .navigationDestination(for: Screen.self) { screen in
                    destination(for: screen)
                }
        }
    }

    private var uploadRoot: some View {
        let pending = appState.pendingSharedPaths
        return UploadScreen(
            worker: appState.uploadQueueWorker,
            onOpenHistory: { navPath.append(.history) },
            onOpenSettings: { navPath.append(.settings) },
            onCopyToClipboard: copyToClipboard,
            initialPaths: pending.isEmpty ? nil : pending
        )
        .onAppear {
            if !pending.isEmpty {
                appState.pendingSharedPaths = []
            }
        }
    }

    @ViewBuilder
    private func destination(for screen: Screen) -> some View {
        switch screen {
        case .loading:
            EmptyView()
        case .upload:
            uploadRoot
        case .history:
            HistoryScreen(
                viewModel: HistoryViewModel(historyRepository: appState.historyRepository),
                onBack: { _ = navPath.popLast() },
                onCopyToClipboard: copyToClipboard
            )
        case .settings:
            SettingsHubScreen(
                settingsRepository: appState.settingsRepository,
                onBack: { _ = navPath.popLast() },
                onNavigateToS3: { navPath.append(.s3Config) },
                onNavigateToCustomUploader: { navPath.append(.customUploaderConfig) }
            )
        case .s3Config:
            S3ConfigScreen(
                viewModel: S3ConfigViewModel(settingsRepository: appState.settingsRepository),
                onBack: { _ = navPath.popLast() }
            )
        case .customUploaderConfig:
            CustomUploaderConfigScreen(
                viewModel: CustomUploaderConfigViewModel(settingsRepository: appState.settingsRepository),
                onBack: { _ = navPath.popLast() }
            )
        }
    }
}
