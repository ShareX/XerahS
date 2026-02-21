//
//  UploadResultItem.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

struct UploadResultItem: Equatable {
    let fileName: String
    let success: Bool
    let url: String?
    let error: String?

    var hasUrl: Bool { !(url ?? "").isEmpty }
}
