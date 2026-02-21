//
//  AppState.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation
import Combine

/// Global app state: repositories and upload worker. Injected via environment.
final class AppState: ObservableObject {
    let settingsRepository: SettingsRepository
    let historyRepository: HistoryRepository
    let uploadQueueWorker: UploadQueueWorker

    /// Paths from share intent to process when Upload screen is ready. Consumed once.
    @Published var pendingSharedPaths: [String] = []

    init(
        settingsRepository: SettingsRepository,
        historyRepository: HistoryRepository,
        uploadQueueWorker: UploadQueueWorker
    ) {
        self.settingsRepository = settingsRepository
        self.historyRepository = historyRepository
        self.uploadQueueWorker = uploadQueueWorker
    }
}
