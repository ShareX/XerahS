//
//  XerahSMobileApp.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI

@main
struct XerahSMobileApp: App {
    @StateObject private var appState: AppState = {
        let fileManager = FileManager.default
        let appSupport = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
            .appendingPathComponent(Bundle.main.bundleIdentifier ?? "XerahSMobile", isDirectory: true)
        let caches = fileManager.urls(for: .cachesDirectory, in: .userDomainMask).first!
        Paths.configure(applicationSupport: appSupport, caches: caches)
        Paths.ensureDirectoriesExist()
        let settingsRepo = SettingsRepository()
        let queueRepo = QueueRepository()
        let historyRepo = HistoryRepository()
        let worker = UploadQueueWorker(
            settingsRepository: settingsRepo,
            queueRepository: queueRepo,
            historyRepository: historyRepo
        )
        return AppState(
            settingsRepository: settingsRepo,
            historyRepository: historyRepo,
            uploadQueueWorker: worker
        )
    }()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(appState)
        }
    }
}
