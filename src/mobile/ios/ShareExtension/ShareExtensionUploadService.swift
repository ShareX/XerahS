//
//  ShareExtensionUploadService.swift
//  XerahS Share Extension
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

private let shareExtensionConfigFileName = "ApplicationConfig.json"

struct ShareExtensionUploadResult {
    let filePath: String
    let url: String?
    let error: String?
    let cloudError: String?

    var succeeded: Bool { url != nil }
}

final class ShareExtensionUploadService {
    private let s3Uploader = S3Uploader()
    private let customUploader = CustomUploader()
    private let decoder = JSONDecoder()
    private let cloudClient = XerahSCloudClient()
    private let historyRepository = HistoryRepository()

    func uploadFiles(_ filePaths: [String]) async -> [ShareExtensionUploadResult] {
        guard var config = loadConfig() else {
            return filePaths.map {
                ShareExtensionUploadResult(
                    filePath: $0,
                    url: nil,
                    error: "No upload destination configured. Open XerahS and configure S3 or a custom uploader in Settings.",
                    cloudError: nil
                )
            }
        }

        config = hydrateSecrets(in: config)
        var results: [ShareExtensionUploadResult] = []
        for path in filePaths {
            let uploaded = uploadFile(filePath: path, config: config)
            let fileName = (path as NSString).lastPathComponent
            guard uploaded.succeeded, let rawURL = uploaded.url else {
                results.append(uploaded)
                continue
            }
            // Same History entry and Cloud tags as the app's upload queue, so shared uploads show up
            // in History and a failed Cloud publish can be retried there.
            var tags: [String: String?] = [:]
            defer {
                _ = historyRepository.insertEntry(fileName: fileName, filePath: path, type: "File", host: "upload", url: rawURL, tags: tags)
            }
            guard XerahSCloudSettings.automaticallyPublishesEligibleUploads,
                  cloudClient.hasStoredCredential,
                  let expectedOwner = cloudClient.currentOwnerSubject, !expectedOwner.isEmpty,
                  let url = URL(string: rawURL),
                  XerahSCloudClient.eligibleMetadata(fileName: fileName) != nil else {
                results.append(uploaded)
                continue
            }
            let clientItemID = XerahSCloudClient.stableClientItemID(filePath: path)
            tags["XerahSCloudClientItemId"] = clientItemID.uuidString.lowercased()
            tags["XerahSCloudOwnerSubject"] = expectedOwner
            do {
                try await cloudClient.publish(clientItemID: clientItemID, publicURL: url, fileName: fileName, expectedOwnerSubject: expectedOwner)
                tags["XerahSCloudPublishState"] = "published"
                results.append(uploaded)
            } catch {
                tags["XerahSCloudPublishState"] = "failed"
                results.append(ShareExtensionUploadResult(
                    filePath: uploaded.filePath,
                    url: uploaded.url,
                    error: uploaded.error,
                    cloudError: "Upload succeeded, but Cloud publish failed: \(error.localizedDescription)"
                ))
            }
        }
        return results
    }

    private func uploadFile(filePath: String, config: ApplicationConfig) -> ShareExtensionUploadResult {
        guard FileManager.default.fileExists(atPath: filePath) else {
            return ShareExtensionUploadResult(filePath: filePath, url: nil, error: "File not found.", cloudError: nil)
        }

        let pathToUpload = convertHeicToPngIfNeeded(filePath: filePath, convertEnabled: config.convertHeicToPng)
        let destId = config.defaultDestinationInstanceId

        if config.s3Config.isConfigured && (destId == nil || destId == kAmazonS3DestinationId || (destId?.hasPrefix(kAmazonS3DestinationId) ?? false)) {
            return makeResult(filePath: filePath, outcome: s3Uploader.uploadFile(filePath: pathToUpload, config: config.s3Config))
        }

        if !config.customUploaders.isEmpty {
            let entry = config.customUploaders.first { $0.id == destId } ?? config.customUploaders[0]
            return makeResult(filePath: filePath, outcome: customUploader.uploadFile(filePath: pathToUpload, entry: entry))
        }

        return ShareExtensionUploadResult(
            filePath: filePath,
            url: nil,
            error: "No upload destination configured. Open XerahS and configure S3 or a custom uploader in Settings.",
            cloudError: nil
        )
    }

    private func makeResult(filePath: String, outcome: UploadOutcome) -> ShareExtensionUploadResult {
        switch outcome {
        case .success(let url):
            return ShareExtensionUploadResult(filePath: filePath, url: url, error: nil, cloudError: nil)
        case .failure(let failure):
            return ShareExtensionUploadResult(filePath: filePath, url: nil, error: failure.message, cloudError: nil)
        }
    }

    private func loadConfig() -> ApplicationConfig? {
        guard
            let settingsFolder = Paths.settingsFolder,
            FileManager.default.fileExists(atPath: settingsFolder.appendingPathComponent(shareExtensionConfigFileName).path)
        else {
            return nil
        }

        do {
            let file = settingsFolder.appendingPathComponent(shareExtensionConfigFileName)
            return try decoder.decode(ApplicationConfig.self, from: Data(contentsOf: file))
        } catch {
            return nil
        }
    }

    private func hydrateSecrets(in config: ApplicationConfig) -> ApplicationConfig {
        var hydrated = config
        hydrateKeychainMarker(&hydrated.s3Config.secretAccessKey)

        for index in hydrated.customUploaders.indices {
            hydrateDictionary(&hydrated.customUploaders[index].parameters)
            hydrateDictionary(&hydrated.customUploaders[index].headers)
            hydrateDictionary(&hydrated.customUploaders[index].arguments)
        }

        return hydrated
    }

    private func hydrateDictionary(_ dictionary: inout [String: String]) {
        for key in dictionary.keys {
            guard var value = dictionary[key] else { continue }
            hydrateKeychainMarker(&value)
            dictionary[key] = value
        }
    }

    private func hydrateKeychainMarker(_ value: inout String) {
        guard
            let account = keychainAccount(fromMarker: value),
            let stored = KeychainStore.readString(account: account)
        else { return }
        value = stored
    }

    private func keychainAccount(fromMarker value: String) -> String? {
        let prefix = "__xerahs_keychain__:"
        guard value.hasPrefix(prefix) else { return nil }
        return String(value.dropFirst(prefix.count))
    }
}
