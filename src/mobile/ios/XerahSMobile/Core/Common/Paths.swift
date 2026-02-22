//
//  Paths.swift
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

/// Paths for settings, history DB, and cache. Call `configure(with:)` from app launch, then `ensureDirectoriesExist()`.
/// Matches C# PathsManager: PersonalFolder = base, Settings = PersonalFolder/Settings, History = PersonalFolder/History/History.db.
/// On iOS, pass appGroupContainer so settings and history live in the App Group and are available when the app is opened from the Share Extension.
enum Paths {
    private static let settingsFolderName = "Settings"
    private static let historyFolderName = "History"
    private static let historyFileName = "History.db"

    private(set) static var settingsFolder: URL?
    private(set) static var historyFolder: URL?
    private(set) static var historyFilePath: String?
    private(set) static var cacheDir: URL?

    /// Call from app startup. Pass applicationSupport and caches URLs from FileManager.
    /// On iOS, pass appGroupContainer (e.g. from FileManager.containerURL(forSecurityApplicationGroupIdentifier:)) so config is in the shared container.
    static func configure(applicationSupport: URL, caches: URL, appGroupContainer: URL? = nil) {
        let base: URL
        if let group = appGroupContainer {
            base = group
        } else {
            base = applicationSupport
        }
        settingsFolder = base.appendingPathComponent(settingsFolderName, isDirectory: true)
        historyFolder = base.appendingPathComponent(historyFolderName, isDirectory: true)
        historyFilePath = historyFolder?.appendingPathComponent(historyFileName).path
        cacheDir = caches
    }

    static func ensureDirectoriesExist() {
        if let url = settingsFolder { try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true) }
        if let url = historyFolder { try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true) }
        if let url = cacheDir { try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true) }
    }
}
