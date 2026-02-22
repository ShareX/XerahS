//
//  ApplicationConfig.swift
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

/// Minimal app config for mobile: default destination, S3, and custom uploaders.
/// Persisted as JSON in settings folder (ApplicationConfig.json).
struct ApplicationConfig: Codable {
    var defaultDestinationInstanceId: String?
    var s3Config: S3Config = S3Config()
    var customUploaders: [CustomUploaderEntry] = []
    /// Convert HEIC/HEIF images to PNG before upload (global; applies to S3 and custom uploaders). Default true.
    var convertHeicToPng: Bool = true
}

struct S3Config: Codable, Equatable {
    var accessKeyId: String = ""
    var secretAccessKey: String = ""
    var bucketName: String = ""
    var region: String = ""
    var customEndpoint: String = ""
    var usePathStyle: Bool = false
    /// Use custom domain (CDN) for result URLs.
    var useCustomDomain: Bool = false
    var customDomain: String = ""
    /// Sign request body; recommended when bucket blocks public ACLs. Default true.
    var signedPayload: Bool = true
    /// Set public-read ACL on uploaded objects. Default false.
    var setPublicAcl: Bool = false

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

// MARK: - Active upload destination

/// Stable id for the built-in S3 destination. Used as defaultDestinationInstanceId when user selects Amazon S3.
let kAmazonS3DestinationId = "amazons3"

extension ApplicationConfig {
    /// Human-readable label for the currently selected upload destination, or nil if none configured/selected.
    func activeDestinationDisplayName() -> String? {
        let id = defaultDestinationInstanceId
        if id == kAmazonS3DestinationId || (id?.hasPrefix("amazons3") ?? false) {
            return s3Config.isConfigured ? "Amazon S3" : nil
        }
        if let id = id, let custom = customUploaders.first(where: { $0.id == id }) {
            return custom.name.isEmpty ? custom.id : custom.name
        }
        if s3Config.isConfigured { return "Amazon S3" }
        if let first = customUploaders.first { return first.name.isEmpty ? first.id : first.name }
        return nil
    }

    /// All selectable destinations: (displayName, instanceId). Order: S3 first (if configured), then custom uploaders.
    func selectableDestinations() -> [(displayName: String, instanceId: String)] {
        var list: [(String, String)] = []
        if s3Config.isConfigured { list.append(("Amazon S3", kAmazonS3DestinationId)) }
        for entry in customUploaders where !entry.id.isEmpty {
            list.append((entry.name.isEmpty ? entry.id : entry.name, entry.id))
        }
        return list
    }
}
