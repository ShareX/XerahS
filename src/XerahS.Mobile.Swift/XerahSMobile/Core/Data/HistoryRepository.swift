//
//  HistoryRepository.swift
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
import SQLite3

/// SQLite history using same schema as C# HistoryManagerSQLite for DB compatibility.
/// Table: History (Id, FileName, FilePath, DateTime, Type, Host, URL, ThumbnailURL, DeletionURL, ShortenedURL, Tags).
final class HistoryRepository {
    private let queue = DispatchQueue(label: "HistoryRepository")
    private let decoder = JSONDecoder()
    private let encoder = JSONEncoder()

    private var dbPath: String? { Paths.historyFilePath }

    init() {
        queue.sync { createTableIfNeeded() }
    }

    private func openDb() -> OpaquePointer? {
        guard let path = dbPath else { return nil }
        Paths.historyFolder.flatMap { try? FileManager.default.createDirectory(at: $0, withIntermediateDirectories: true) }
        var db: OpaquePointer?
        guard sqlite3_open(path, &db) == SQLITE_OK else { return nil }
        return db
    }

    private func createTableIfNeeded() {
        guard let db = openDb() else { return }
        defer { sqlite3_close(db) }
        let sql = """
        CREATE TABLE IF NOT EXISTS History (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FileName TEXT,
            FilePath TEXT,
            DateTime TEXT,
            Type TEXT,
            Host TEXT,
            URL TEXT,
            ThumbnailURL TEXT,
            DeletionURL TEXT,
            ShortenedURL TEXT,
            Tags TEXT
        )
        """
        sqlite3_exec(db, sql, nil, nil, nil)
    }

    func getRecentEntries(limit: Int) -> [HistoryEntry] {
        guard limit > 0, let path = dbPath, FileManager.default.fileExists(atPath: path) else { return [] }
        return queue.sync {
            guard let db = openDb() else { return [] }
            defer { sqlite3_close(db) }
            let sql = "SELECT * FROM History ORDER BY DateTime DESC LIMIT ?"
            var stmt: OpaquePointer?
            guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return [] }
            defer { sqlite3_finalize(stmt) }
            sqlite3_bind_int(stmt, 1, Int32(limit))
            var list: [HistoryEntry] = []
            while sqlite3_step(stmt) == SQLITE_ROW {
                let id = sqlite3_column_int64(stmt, 0)
                let fileName = columnText(stmt, 1) ?? ""
                let filePath = columnText(stmt, 2) ?? ""
                let dateTime = columnText(stmt, 3) ?? ""
                let type = columnText(stmt, 4) ?? ""
                let host = columnText(stmt, 5) ?? ""
                let url = columnText(stmt, 6) ?? ""
                let thumb = columnText(stmt, 7)
                let del = columnText(stmt, 8)
                let short = columnText(stmt, 9)
                let tagsJson = columnText(stmt, 10)
                let tags = parseTags(tagsJson)
                list.append(HistoryEntry(
                    id: id,
                    fileName: fileName,
                    filePath: filePath,
                    dateTime: dateTime,
                    type: type,
                    host: host,
                    url: url,
                    thumbnailUrl: thumb ?? "",
                    deletionUrl: del ?? "",
                    shortenedUrl: short ?? "",
                    tags: tags
                ))
            }
            return list
        }
    }

    private func columnText(_ stmt: OpaquePointer?, _ index: Int32) -> String? {
        guard let ptr = sqlite3_column_text(stmt, index) else { return nil }
        return String(cString: ptr)
    }

    private func parseTags(_ json: String?) -> [String: String?] {
        guard let json = json, !json.isEmpty,
              let data = json.data(using: .utf8),
              let dict = try? decoder.decode([String: String?].self, from: data) else {
            return [:]
        }
        return dict
    }

    func deleteEntry(id: Int64) -> Bool {
        queue.sync {
            guard let db = openDb() else { return false }
            defer { sqlite3_close(db) }
            let sql = "DELETE FROM History WHERE Id = ?"
            var stmt: OpaquePointer?
            guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return false }
            defer { sqlite3_finalize(stmt) }
            sqlite3_bind_int64(stmt, 1, id)
            return sqlite3_step(stmt) == SQLITE_DONE && sqlite3_changes(db) > 0
        }
    }

    func clearEntries() -> Int {
        queue.sync {
            guard let db = openDb() else { return 0 }
            defer { sqlite3_close(db) }
            sqlite3_exec(db, "DELETE FROM History", nil, nil, nil)
            return Int(sqlite3_changes(db))
        }
    }

    func insertEntry(
        fileName: String,
        filePath: String,
        type: String,
        host: String,
        url: String,
        thumbnailUrl: String = "",
        deletionUrl: String = "",
        shortenedUrl: String = "",
        tags: [String: String?] = [:]
    ) -> Int64 {
        queue.sync {
            guard let db = openDb() else { return -1 }
            defer { sqlite3_close(db) }
            let dateTime = ISO8601DateFormatter().string(from: Date())
            let tagsJson = (try? encoder.encode(tags)).flatMap { String(data: $0, encoding: .utf8) } ?? "{}"
            let sql = "INSERT INTO History (FileName, FilePath, DateTime, Type, Host, URL, ThumbnailURL, DeletionURL, ShortenedURL, Tags) VALUES (?,?,?,?,?,?,?,?,?,?)"
            var stmt: OpaquePointer?
            guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return -1 }
            defer { sqlite3_finalize(stmt) }
            sqlite3_bind_text(stmt, 1, (fileName as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 2, (filePath as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 3, (dateTime as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 4, (type as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 5, (host as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 6, (url as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 7, (thumbnailUrl as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 8, (deletionUrl as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 9, (shortenedUrl as NSString).utf8String, -1, nil)
            sqlite3_bind_text(stmt, 10, (tagsJson as NSString).utf8String, -1, nil)
            guard sqlite3_step(stmt) == SQLITE_DONE else { return -1 }
            return sqlite3_last_insert_rowid(db)
        }
    }
}
