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
        .onOpenURL { url in
            if url.scheme == "xerahs" {
                appState.pendingSharedPaths = ShareGroup.consumePendingPaths()
            }
        }
    }
}
