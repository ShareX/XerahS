//
//  Paths.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

/// Paths for settings, history DB, and cache. Call `configure(with:)` from app launch, then `ensureDirectoriesExist()`.
/// Matches C# PathsManager: PersonalFolder = base, Settings = PersonalFolder/Settings, History = PersonalFolder/History/History.db.
enum Paths {
    private static let settingsFolderName = "Settings"
    private static let historyFolderName = "History"
    private static let historyFileName = "History.db"

    private(set) static var settingsFolder: URL?
    private(set) static var historyFolder: URL?
    private(set) static var historyFilePath: String?
    private(set) static var cacheDir: URL?

    /// Call from app startup. Pass applicationSupport and caches URLs from FileManager.
    static func configure(applicationSupport: URL, caches: URL) {
        settingsFolder = applicationSupport.appendingPathComponent(settingsFolderName, isDirectory: true)
        historyFolder = applicationSupport.appendingPathComponent(historyFolderName, isDirectory: true)
        historyFilePath = historyFolder?.appendingPathComponent(historyFileName).path
        cacheDir = caches
    }

    static func ensureDirectoriesExist() {
        if let url = settingsFolder { try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true) }
        if let url = historyFolder { try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true) }
        if let url = cacheDir { try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true) }
    }
}
