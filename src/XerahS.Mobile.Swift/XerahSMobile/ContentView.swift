//
//  ContentView.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI

/// Placeholder root; app uses RootView with environmentObject(AppState).
struct ContentView: View {
    var body: some View {
        RootView()
            .environmentObject(AppState(
                settingsRepository: SettingsRepository(),
                historyRepository: HistoryRepository(),
                uploadQueueWorker: UploadQueueWorker(
                    settingsRepository: SettingsRepository(),
                    queueRepository: QueueRepository(),
                    historyRepository: HistoryRepository()
                )
            ))
    }
}

#Preview {
    ContentView()
}
