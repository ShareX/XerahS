//
//  ApplicationConfig.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

/// Minimal app config for mobile: default destination, S3, and custom uploaders.
/// Persisted as JSON in settings folder (ApplicationConfig.json).
struct ApplicationConfig: Codable {
    var defaultDestinationInstanceId: String?
    var s3Config: S3Config = S3Config()
    var customUploaders: [CustomUploaderEntry] = []
}

struct S3Config: Codable, Equatable {
    var accessKeyId: String = ""
    var secretAccessKey: String = ""
    var bucketName: String = ""
    var region: String = ""
    var customEndpoint: String = ""
    var usePathStyle: Bool = false

    var isConfigured: Bool {
        !accessKeyId.isEmpty && !secretAccessKey.isEmpty && !bucketName.isEmpty && !region.isEmpty
    }
}

/// One custom uploader (.sxcu-style). Persisted in config or as separate files.
struct CustomUploaderEntry: Codable, Equatable, Identifiable {
    var id: String = ""
    var name: String = ""
    var requestUrl: String = ""
    var body: String = ""
    var headers: [String: String] = [:]
    var fileFormName: String = "file"
    var urlExpression: String = ""  // regex or template to extract URL from response
}
