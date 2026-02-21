//
//  HistoryEntry.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

/// One upload history entry. Matches C# HistoryItem / SQLite History table for compatibility.
struct HistoryEntry: Codable, Identifiable, Equatable {
    let id: Int64
    let fileName: String
    let filePath: String
    let dateTime: String
    let type: String
    let host: String
    let url: String
    var thumbnailUrl: String = ""
    var deletionUrl: String = ""
    var shortenedUrl: String = ""
    var tags: [String: String?] = [:]
}
