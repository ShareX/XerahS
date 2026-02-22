//
//  XerahSMobileApp.swift
//  XerahS Mobile (Swift)
//
//  XerahS - The Avalonia UI implementation of ShareX
//  Copyright (c) 2007-2026 ShareX Team
//
//  This program is free software; you can redistribute it and/or
//  modify it under the terms of the GNU General Public License
//  as published by the Free Software Foundation; either version 2
//  of the License, or (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
//
//  Optionally you can also view the license at <http://www.gnu.org/licenses/>.
//

import SwiftUI

/// One-time migration: when using App Group, copy Settings, queue, and History from legacy Application Support if present.
private func migrateSettingsFromAppSupportIfNeeded(appSupport: URL) {
    let fm = FileManager.default
    if let settingsFolder = Paths.settingsFolder {
        let legacySettings = appSupport.appendingPathComponent("Settings", isDirectory: true)
        for name in ["ApplicationConfig.json", "MobileUploadQueue.json"] {
            let dest = settingsFolder.appendingPathComponent(name)
            guard !fm.fileExists(atPath: dest.path) else { continue }
            let src = legacySettings.appendingPathComponent(name)
            guard fm.fileExists(atPath: src.path) else { continue }
            try? fm.createDirectory(at: settingsFolder, withIntermediateDirectories: true)
            try? fm.copyItem(at: src, to: dest)
        }
    }
    if let historyFolder = Paths.historyFolder {
        let legacyHistory = appSupport.appendingPathComponent("History", isDirectory: true)
        let dbName = "History.db"
        let dest = historyFolder.appendingPathComponent(dbName)
        guard !fm.fileExists(atPath: dest.path) else { return }
        let src = legacyHistory.appendingPathComponent(dbName)
        guard fm.fileExists(atPath: src.path) else { return }
        try? fm.createDirectory(at: historyFolder, withIntermediateDirectories: true)
        try? fm.copyItem(at: src, to: dest)
    }
}

@main
struct XerahSMobileApp: App {
    @StateObject private var appState: AppState = {
        let fileManager = FileManager.default
        let appSupport = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
            .appendingPathComponent(Bundle.main.bundleIdentifier ?? "XerahSMobile", isDirectory: true)
        let caches = fileManager.urls(for: .cachesDirectory, in: .userDomainMask).first!
        let appGroupContainer = fileManager.containerURL(forSecurityApplicationGroupIdentifier: ShareGroup.appGroupId)
        Paths.configure(applicationSupport: appSupport, caches: caches, appGroupContainer: appGroupContainer)
        Paths.ensureDirectoriesExist()
        migrateSettingsFromAppSupportIfNeeded(appSupport: appSupport)
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
                .onOpenURL { url in
                    if url.scheme == "xerahs" {
                        appState.pendingSharedPaths = ShareGroup.consumePendingPaths()
                    }
                }
        }
    }
}
