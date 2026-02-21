//
//  QueueRepository.swift
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

private let queueFileName = "MobileUploadQueue.json"

/// Persistent upload queue as JSON (matches C# UploadQueueService snapshot format). Thread-safe via serial queue.
final class QueueRepository {
    private let queue = DispatchQueue(label: "QueueRepository")
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    private var queueFile: URL? {
        Paths.settingsFolder?.appendingPathComponent(queueFileName)
    }

    func enqueue(filePaths: [String]) -> Int {
        queue.sync {
            var items = loadSnapshot()
            let now = ISO8601DateFormatter().string(from: Date())
            var added = 0
            for path in filePaths where !path.isEmpty {
                items.append(UploadQueueItem(filePath: path, enqueuedUtc: now))
                added += 1
            }
            if added > 0 { saveSnapshot(items) }
            return added
        }
    }

    func peek() -> UploadQueueItem? {
        queue.sync { loadSnapshot().first }
    }

    func dequeue() -> UploadQueueItem? {
        queue.sync {
            var items = loadSnapshot()
            guard let first = items.first else { return nil }
            items.removeFirst()
            saveSnapshot(items)
            return first
        }
    }

    func snapshot() -> [UploadQueueItem] {
        queue.sync { loadSnapshot() }
    }

    func pendingCount() -> Int {
        queue.sync { loadSnapshot().count }
    }

    private func loadSnapshot() -> [UploadQueueItem] {
        guard let file = queueFile, FileManager.default.fileExists(atPath: file.path) else {
            return []
        }
        do {
            let data = try Data(contentsOf: file)
            return (try? decoder.decode([UploadQueueItem].self, from: data)) ?? []
        } catch {
            return []
        }
    }

    private func saveSnapshot(_ items: [UploadQueueItem]) {
        guard let file = queueFile else { return }
        Paths.settingsFolder.flatMap { try? FileManager.default.createDirectory(at: $0, withIntermediateDirectories: true) }
        if items.isEmpty {
            try? FileManager.default.removeItem(at: file)
            return
        }
        try? encoder.encode(items).write(to: file)
    }
}
