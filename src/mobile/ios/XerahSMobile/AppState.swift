//
//  AppState.swift
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

import Combine
import CommonCrypto
import CryptoKit
import Foundation

/// Result of an .sxcu or .xsdc import, shown as an alert so it is not missed (a toast raised while
/// the passphrase sheet closes is easy to overlook, which led to importing twice).
struct ImportResultAlert: Identifiable {
    let id = UUID()
    let succeeded: Bool
    let message: String

    var title: String { succeeded ? "Import complete" : "Import failed" }
}

struct PendingDestinationConfigImport: Identifiable {
    let id = UUID()
    let data: Data
    let sourceLabel: String
}

/// Global app state: repositories and upload worker. Injected via environment.
final class AppState: ObservableObject {
    let settingsRepository: SettingsRepository
    let historyRepository: HistoryRepository
    let uploadQueueWorker: UploadQueueWorker
    let cloudClient: XerahSCloudClient

    /// Paths from share intent to process when Upload screen is ready. Consumed once.
    @Published var pendingSharedPaths: [String] = []
    @Published var pendingNavigation: Screen?
    @Published var pendingDestinationConfigImport: PendingDestinationConfigImport?
    @Published var importResultAlert: ImportResultAlert?

    init(
        settingsRepository: SettingsRepository,
        historyRepository: HistoryRepository,
        uploadQueueWorker: UploadQueueWorker,
        cloudClient: XerahSCloudClient
    ) {
        self.settingsRepository = settingsRepository
        self.historyRepository = historyRepository
        self.uploadQueueWorker = uploadQueueWorker
        self.cloudClient = cloudClient
        importPendingSxcuFiles()
        importPendingXsdcFiles()
    }

    func handleIncomingURL(_ url: URL) {
        if url.isFileURL && url.pathExtension.lowercased() == "sxcu" {
            importSxcuFile(from: url)
            return
        }
        if url.isFileURL && url.pathExtension.lowercased() == "xsdc" {
            queueXsdcFile(from: url)
            return
        }

        guard url.scheme == "xerahs" else { return }

        let normalizedPath = url.path.trimmingCharacters(in: CharacterSet(charactersIn: "/")).lowercased()
        let normalizedHost = (url.host ?? "").lowercased()
        if normalizedHost == "import-sxcu" || normalizedPath == "import-sxcu" {
            importRemoteSxcu(from: url)
            return
        }

        consumePendingShareItems()
    }

    /// Picks up imports and failed uploads queued by the Share Extension. Share extensions cannot
    /// open the app themselves, so this also runs whenever the app becomes active.
    func consumePendingShareItems() {
        importPendingSxcuFiles()
        importPendingXsdcFiles()
        let paths = ShareGroup.consumePendingPaths()
        if !paths.isEmpty {
            pendingSharedPaths += paths
        }
    }

    private func importPendingSxcuFiles() {
        let pendingFiles = ShareGroup.consumePendingSxcuImports()
        for path in pendingFiles where !path.isEmpty {
            importSxcuFile(from: URL(fileURLWithPath: path))
        }
    }

    private func importPendingXsdcFiles() {
        let pendingFiles = ShareGroup.consumePendingXsdcImports()
        for path in pendingFiles where !path.isEmpty {
            queueXsdcFile(from: URL(fileURLWithPath: path))
        }
    }

    private func importSxcuFile(from url: URL) {
        let accessing = url.startAccessingSecurityScopedResource()
        defer {
            if accessing {
                url.stopAccessingSecurityScopedResource()
            }
        }

        guard let data = try? Data(contentsOf: url) else {
            publishImportResult(message: "Failed to read .sxcu file.", navigate: false, isError: true)
            return
        }

        importSxcuData(data, sourceLabel: url.lastPathComponent)
    }

    /// Starts importing a destination config picked in the app; asks for its passphrase.
    func importDestinationConfig(from url: URL) {
        queueXsdcFile(from: url)
    }

    private func queueXsdcFile(from url: URL) {
        let accessing = url.startAccessingSecurityScopedResource()
        defer {
            if accessing {
                url.stopAccessingSecurityScopedResource()
            }
        }

        guard let data = try? Data(contentsOf: url) else {
            publishImportResult(message: "Failed to read .xsdc file.", navigate: false, isError: true)
            return
        }

        DispatchQueue.main.async {
            self.pendingDestinationConfigImport = PendingDestinationConfigImport(data: data, sourceLabel: url.lastPathComponent)
        }
    }

    func importPendingDestinationConfig(passphrase: String) {
        guard let pending = pendingDestinationConfigImport else { return }

        do {
            let payload = try XsdcImporter.decrypt(data: pending.data, passphrase: passphrase)
            let imported = try XsdcImporter.importPayload(payload, settingsRepository: settingsRepository)
            pendingDestinationConfigImport = nil
            publishImportResult(message: "Imported destination config: \(imported)", navigate: true, screen: .s3Config)
        } catch {
            publishImportResult(message: "Failed to import \(pending.sourceLabel): \(error.localizedDescription)", navigate: false, isError: true)
        }
    }

    func cancelPendingDestinationConfigImport() {
        pendingDestinationConfigImport = nil
    }

    private func importRemoteSxcu(from deepLink: URL) {
        guard
            let components = URLComponents(url: deepLink, resolvingAgainstBaseURL: false),
            let rawTarget = components.queryItems?.first(where: { $0.name == "url" })?.value,
            let targetUrl = URL(string: rawTarget),
            ["https", "http"].contains(targetUrl.scheme?.lowercased() ?? "")
        else {
            publishImportResult(message: "Invalid import link. Missing or invalid remote .sxcu URL.", navigate: false, isError: true)
            return
        }

        Task {
            do {
                let (data, _) = try await URLSession.shared.data(from: targetUrl)
                importSxcuData(data, sourceLabel: targetUrl.lastPathComponent.isEmpty ? targetUrl.absoluteString : targetUrl.lastPathComponent)
            } catch {
                publishImportResult(message: "Failed to download .sxcu: \(error.localizedDescription)", navigate: false, isError: true)
            }
        }
    }

    private func importSxcuData(_ data: Data, sourceLabel: String) {
        let decoder = JSONDecoder()

        guard let definition = try? decoder.decode(SxcuDefinition.self, from: data) else {
            publishImportResult(message: "The file \(sourceLabel) is not a valid .sxcu definition.", navigate: false, isError: true)
            return
        }

        var imported = CustomUploaderEntry.from(sxcu: definition)
        var list = settingsRepository.loadCustomUploaders()

        if let existingIndex = list.firstIndex(where: {
            $0.requestUrl.caseInsensitiveCompare(imported.requestUrl) == .orderedSame &&
            $0.name.caseInsensitiveCompare(imported.name) == .orderedSame
        }) {
            imported.id = list[existingIndex].id
            list[existingIndex] = imported
            settingsRepository.saveCustomUploaders(list)
            publishImportResult(message: "Updated custom uploader: \(imported.displayName)", navigate: true)
            return
        }

        list.append(imported)
        settingsRepository.saveCustomUploaders(list)
        publishImportResult(message: "Imported custom uploader: \(imported.displayName)", navigate: true)
    }

    private func publishImportResult(message: String, navigate: Bool, screen: Screen = .customUploaderConfig, isError: Bool = false) {
        DispatchQueue.main.async {
            if navigate {
                self.pendingNavigation = screen
            }
        }
        // After any sheet (such as the passphrase sheet) has finished dismissing.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) {
            self.importResultAlert = ImportResultAlert(succeeded: !isError, message: message)
        }
    }
}

private enum XsdcImportError: LocalizedError {
    case invalidEnvelope
    case unsupportedEncryption
    case decryptionFailed
    case noSupportedDestinations

    var errorDescription: String? {
        switch self {
        case .invalidEnvelope:
            return "The .xsdc file is not a valid XerahS destination config."
        case .unsupportedEncryption:
            return "This .xsdc encryption method is not supported."
        case .decryptionFailed:
            return "The passphrase is incorrect or the file is damaged."
        case .noSupportedDestinations:
            return "No mobile-compatible destination was found in this .xsdc file."
        }
    }
}

private struct XsdcEnvelope: Decodable {
    let format: String
    let formatVersion: Int
    let encryption: XsdcEncryption
    let payload: String

    enum CodingKeys: String, CodingKey {
        case format = "Format"
        case formatVersion = "FormatVersion"
        case encryption = "Encryption"
        case payload = "Payload"
    }
}

private struct XsdcEncryption: Decodable {
    let method: String
    let kdf: String
    let iterations: Int
    let salt: String
    let cipher: String
    let nonce: String
    let tag: String

    enum CodingKeys: String, CodingKey {
        case method = "Method"
        case kdf = "Kdf"
        case iterations = "Iterations"
        case salt = "Salt"
        case cipher = "Cipher"
        case nonce = "Nonce"
        case tag = "Tag"
    }
}

private struct XsdcPayload: Decodable {
    let destinations: [XsdcDestination]

    enum CodingKeys: String, CodingKey {
        case destinations = "Destinations"
    }
}

private struct XsdcDestination: Decodable {
    let providerId: String
    let displayName: String
    let isDefault: Bool
    let config: XsdcS3Config

    enum CodingKeys: String, CodingKey {
        case providerId = "ProviderId"
        case displayName = "DisplayName"
        case isDefault = "IsDefault"
        case config = "Config"
    }
}

private struct XsdcS3Config: Decodable {
    let authMode: String
    let accessKeyId: String
    let secretAccessKey: String
    let bucketName: String
    let region: String
    let endpoint: String
    let usePathStyle: Bool
    let useCustomDomain: Bool
    let customDomain: String
    let signedPayload: Bool
    let setPublicAcl: Bool

    enum CodingKeys: String, CodingKey {
        case authMode = "AuthMode"
        case accessKeyId = "AccessKeyId"
        case secretAccessKey = "SecretAccessKey"
        case bucketName = "BucketName"
        case region = "Region"
        case endpoint = "Endpoint"
        case usePathStyle = "UsePathStyle"
        case useCustomDomain = "UseCustomDomain"
        case customDomain = "CustomDomain"
        case signedPayload = "SignedPayload"
        case setPublicAcl = "SetPublicAcl"
    }
}

private enum XsdcImporter {
    static func decrypt(data: Data, passphrase: String) throws -> XsdcPayload {
        let envelope = try JSONDecoder().decode(XsdcEnvelope.self, from: data)
        guard envelope.format == "XerahS.DestinationConfig", envelope.formatVersion == 1 else {
            throw XsdcImportError.invalidEnvelope
        }

        let encryption = envelope.encryption
        guard encryption.method == "Passphrase",
              encryption.kdf == "PBKDF2-HMAC-SHA256",
              encryption.cipher == "AES-256-GCM",
              encryption.iterations > 0,
              let salt = Data(base64Encoded: encryption.salt),
              let nonce = Data(base64Encoded: encryption.nonce),
              let tag = Data(base64Encoded: encryption.tag),
              let cipherText = Data(base64Encoded: envelope.payload)
        else {
            throw XsdcImportError.unsupportedEncryption
        }

        let key = try deriveKey(passphrase: passphrase, salt: salt, iterations: encryption.iterations)
        do {
            let sealedBox = try AES.GCM.SealedBox(
                nonce: AES.GCM.Nonce(data: nonce),
                ciphertext: cipherText,
                tag: tag
            )
            let plainText = try AES.GCM.open(sealedBox, using: key)
            return try JSONDecoder().decode(XsdcPayload.self, from: plainText)
        } catch {
            throw XsdcImportError.decryptionFailed
        }
    }

    static func importPayload(_ payload: XsdcPayload, settingsRepository: SettingsRepository) throws -> String {
        guard let destination = payload.destinations.first(where: {
            $0.providerId.caseInsensitiveCompare(kAmazonS3DestinationId) == .orderedSame &&
            $0.config.authMode.caseInsensitiveCompare("AccessKeys") == .orderedSame
        }) else {
            throw XsdcImportError.noSupportedDestinations
        }

        let imported = destination.config
        let config = S3Config(
            accessKeyId: imported.accessKeyId,
            secretAccessKey: imported.secretAccessKey,
            bucketName: imported.bucketName,
            region: imported.region,
            customEndpoint: imported.endpoint,
            usePathStyle: imported.usePathStyle,
            useCustomDomain: imported.useCustomDomain,
            customDomain: imported.customDomain,
            signedPayload: imported.signedPayload,
            setPublicAcl: imported.setPublicAcl
        )

        settingsRepository.saveS3Config(config)
        if destination.isDefault || settingsRepository.getDefaultDestinationInstanceId() == nil {
            settingsRepository.setDefaultDestinationInstanceId(kAmazonS3DestinationId)
        }

        return destination.displayName.isEmpty ? "Amazon S3" : destination.displayName
    }

    private static func deriveKey(passphrase: String, salt: Data, iterations: Int) throws -> SymmetricKey {
        guard let passphraseData = passphrase.data(using: .utf8) else {
            throw XsdcImportError.decryptionFailed
        }

        let keyLength = 32
        var keyData = Data(count: keyLength)
        let result = keyData.withUnsafeMutableBytes { keyBytes in
            salt.withUnsafeBytes { saltBytes in
                passphraseData.withUnsafeBytes { passphraseBytes in
                    CCKeyDerivationPBKDF(
                        CCPBKDFAlgorithm(kCCPBKDF2),
                        passphraseBytes.bindMemory(to: Int8.self).baseAddress,
                        passphraseData.count,
                        saltBytes.bindMemory(to: UInt8.self).baseAddress,
                        salt.count,
                        CCPseudoRandomAlgorithm(kCCPRFHmacAlgSHA256),
                        UInt32(iterations),
                        keyBytes.bindMemory(to: UInt8.self).baseAddress,
                        keyLength
                    )
                }
            }
        }

        guard result == kCCSuccess else {
            throw XsdcImportError.decryptionFailed
        }

        return SymmetricKey(data: keyData)
    }
}
