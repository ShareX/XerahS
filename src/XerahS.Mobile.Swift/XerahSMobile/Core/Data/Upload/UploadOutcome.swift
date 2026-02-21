//
//  UploadOutcome.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

enum UploadOutcome {
    case success(url: String)
    case failure(error: String)
}
