//
//  UploadQueueWorker.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation
import Combine

/// Processes the persistent upload queue: dequeue one item, resolve destination (S3 or custom),
/// upload, append to history, emit result.
final class UploadQueueWorker: ObservableObject {
    struct QueueState {
        var processing: Bool
        var pendingCount: Int
    }

    private let settingsRepository: SettingsRepository
    private let queueRepository: QueueRepository
    private let historyRepository: HistoryRepository
    private let s3Uploader: S3Uploader
    private let customUploader: CustomUploader
    private let queue = DispatchQueue(label: "UploadQueueWorker")
    private var processing = false

    let state = CurrentValueSubject<QueueState, Never>(QueueState(processing: false, pendingCount: 0))
    let itemCompleted = PassthroughSubject<UploadResultItem?, Never>()

    init(
        settingsRepository: SettingsRepository,
        queueRepository: QueueRepository,
        historyRepository: HistoryRepository,
        s3Uploader: S3Uploader = S3Uploader(),
        customUploader: CustomUploader = CustomUploader()
    ) {
        self.settingsRepository = settingsRepository
        self.queueRepository = queueRepository
        self.historyRepository = historyRepository
        self.s3Uploader = s3Uploader
        self.customUploader = customUploader
    }

    func startProcessing() {
        queue.async { [weak self] in
            guard let self = self, !self.processing else { return }
            self.processing = true
            self.runLoop()
            self.processing = false
        }
    }

    private func runLoop() {
        updateState()
        while let item = queueRepository.dequeue() {
            let fileName = (item.filePath as NSString).lastPathComponent
            let result = uploadOne(filePath: item.filePath)
            if result.success, let url = result.url {
                _ = historyRepository.insertEntry(fileName: fileName, filePath: item.filePath, type: "File", host: "upload", url: url)
            }
            itemCompleted.send(result)
            itemCompleted.send(nil)
        }
        updateState()
    }

    func updateState() {
        let count = queueRepository.pendingCount()
        state.send(QueueState(processing: count > 0, pendingCount: count))
    }

    func enqueueFiles(_ filePaths: [String]) -> Int {
        let valid = filePaths.filter { FileManager.default.fileExists(atPath: $0) }
        let added = queueRepository.enqueue(filePaths: valid)
        if added > 0 {
            updateState()
            startProcessing()
        }
        return added
    }

    private func uploadOne(filePath: String) -> UploadResultItem {
        let fileName = (filePath as NSString).lastPathComponent
        guard FileManager.default.fileExists(atPath: filePath) else {
            return UploadResultItem(fileName: fileName, success: false, url: nil, error: "File not found")
        }
        let config = settingsRepository.load()
        let destId = config.defaultDestinationInstanceId

        if config.s3Config.isConfigured && (destId == nil || destId == "amazons3" || (destId?.hasPrefix("amazons3") ?? false)) {
            switch s3Uploader.uploadFile(filePath: filePath, config: config.s3Config) {
            case .success(let url): return UploadResultItem(fileName: fileName, success: true, url: url, error: nil)
            case .failure(let error): return UploadResultItem(fileName: fileName, success: false, url: nil, error: error)
            }
        }
        if !config.customUploaders.isEmpty {
            let entry = config.customUploaders.first { $0.id == destId } ?? config.customUploaders[0]
            switch customUploader.uploadFile(filePath: filePath, entry: entry) {
            case .success(let url): return UploadResultItem(fileName: fileName, success: true, url: url, error: nil)
            case .failure(let error): return UploadResultItem(fileName: fileName, success: false, url: nil, error: error)
            }
        }
        return UploadResultItem(fileName: fileName, success: false, url: nil, error: "No upload destination configured. Configure S3 or a custom uploader in Settings.")
    }
}
