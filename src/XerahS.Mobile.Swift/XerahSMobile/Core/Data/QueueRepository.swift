//
//  QueueRepository.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
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
