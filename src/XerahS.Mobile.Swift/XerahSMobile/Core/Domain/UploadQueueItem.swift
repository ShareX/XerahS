//
//  UploadQueueItem.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

/// One item in the persistent upload queue. Matches C# UploadQueueItem for JSON compatibility.
struct UploadQueueItem: Codable, Equatable {
    let filePath: String
    let enqueuedUtc: String  // ISO 8601
}
