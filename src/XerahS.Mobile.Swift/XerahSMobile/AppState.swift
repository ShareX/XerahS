//
//  AppState.swift
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
