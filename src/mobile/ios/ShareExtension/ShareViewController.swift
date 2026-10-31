//
//  ShareViewController.swift
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

import UIKit
import UniformTypeIdentifiers

private let appGroupId = AppIdentifiers.appGroupId
private let pendingPathsKey = "PendingSharedPaths"
private let pendingSxcuImportsKey = "PendingSxcuImports"
private let pendingXsdcImportsKey = "PendingXsdcImports"
private let openAppURLString = "xerahs://share"

final class ShareViewController: UIViewController {
    private let uploadService = ShareExtensionUploadService()
    private let statusLabel = UILabel()
    private let spinner = UIActivityIndicatorView(style: .medium)
    private var didStartHandlingItems = false

    override func viewDidLoad() {
        super.viewDidLoad()
        configureStatusView()
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        guard !didStartHandlingItems else { return }
        didStartHandlingItems = true
        handleSharedItems()
    }

    private func configureStatusView() {
        view.backgroundColor = .systemBackground

        let titleLabel = UILabel()
        titleLabel.text = "XerahS"
        titleLabel.font = .preferredFont(forTextStyle: .title2)
        titleLabel.adjustsFontForContentSizeCategory = true
        titleLabel.textAlignment = .center

        statusLabel.text = "Preparing shared item..."
        statusLabel.font = .preferredFont(forTextStyle: .body)
        statusLabel.adjustsFontForContentSizeCategory = true
        statusLabel.textColor = .secondaryLabel
        statusLabel.textAlignment = .center
        statusLabel.numberOfLines = 0

        spinner.startAnimating()

        let stack = UIStackView(arrangedSubviews: [titleLabel, spinner, statusLabel])
        stack.axis = .vertical
        stack.alignment = .center
        stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(stack)

        NSLayoutConstraint.activate([
            stack.centerXAnchor.constraint(equalTo: view.safeAreaLayoutGuide.centerXAnchor),
            stack.centerYAnchor.constraint(equalTo: view.safeAreaLayoutGuide.centerYAnchor),
            stack.leadingAnchor.constraint(greaterThanOrEqualTo: view.safeAreaLayoutGuide.leadingAnchor, constant: 24),
            stack.trailingAnchor.constraint(lessThanOrEqualTo: view.safeAreaLayoutGuide.trailingAnchor, constant: -24)
        ])
    }

    private func updateStatus(_ text: String) {
        DispatchQueue.main.async {
            self.statusLabel.text = text
        }
    }

    private func handleSharedItems() {
        guard let extensionItems = extensionContext?.inputItems as? [NSExtensionItem] else {
            finishWithError()
            return
        }
        guard let groupContainer = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroupId) else {
            finishWithError()
            return
        }
        let caches = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first
            ?? groupContainer.appendingPathComponent("Caches", isDirectory: true)
        Paths.configure(applicationSupport: groupContainer, caches: caches, appGroupContainer: groupContainer)
        Paths.ensureDirectoriesExist()

        let inbox = groupContainer.appendingPathComponent("ShareInbox", isDirectory: true)
        try? FileManager.default.createDirectory(at: inbox, withIntermediateDirectories: true)

        // Order: file-url first (preserves name), then URL, then specific types, then generic data so any file is accepted.
        let supportedTypes: [String] = [
            "public.file-url",
            "public.url",
            UTType.image.identifier,
            UTType.jpeg.identifier,
            UTType.png.identifier,
            UTType.gif.identifier,
            UTType.webP.identifier,
            UTType.heic.identifier,
            UTType.pdf.identifier,
            UTType.movie.identifier,
            UTType.mpeg4Movie.identifier,
            UTType.quickTimeMovie.identifier,
            UTType.avi.identifier,
            UTType.audio.identifier,
            UTType.mp3.identifier,
            UTType.mpeg4Audio.identifier,
            UTType.wav.identifier,
            UTType.plainText.identifier,
            UTType.utf8PlainText.identifier,
            UTType.content.identifier,
            UTType.data.identifier,
            "public.data",
            "public.content"
        ]
        var savedPaths: [String] = []
        let group = DispatchGroup()
        let lock = NSLock()

        updateStatus("Preparing shared item...")

        for item in extensionItems {
            guard let attachments = item.attachments else { continue }
            for provider in attachments {
                for typeId in supportedTypes {
                    if provider.hasItemConformingToTypeIdentifier(typeId) {
                        group.enter()
                        loadProviderItem(provider, typeId: typeId, inbox: inbox) { path in
                            defer { group.leave() }
                            if let p = path {
                                lock.lock()
                                savedPaths.append(p)
                                lock.unlock()
                            }
                        }
                        break
                    }
                }
            }
        }

        group.notify(queue: .main) { [weak self] in
            self?.finalizeShare(savedPaths: savedPaths)
        }
    }

    private func loadProviderItem(
        _ provider: NSItemProvider,
        typeId: String,
        inbox: URL,
        completion: @escaping (String?) -> Void
    ) {
        if shouldPreferFileRepresentation(typeId) {
            provider.loadFileRepresentation(forTypeIdentifier: typeId) { [weak self] url, _ in
                guard let self else {
                    completion(nil)
                    return
                }
                if let url, let path = self.copyToInbox(url: url, inbox: inbox, preferredExtension: self.preferredExtension(for: typeId)) {
                    completion(path)
                    return
                }
                self.loadInMemoryItem(provider, typeId: typeId, inbox: inbox, completion: completion)
            }
            return
        }

        loadInMemoryItem(provider, typeId: typeId, inbox: inbox, completion: completion)
    }

    private func loadInMemoryItem(
        _ provider: NSItemProvider,
        typeId: String,
        inbox: URL,
        completion: @escaping (String?) -> Void
    ) {
        provider.loadItem(forTypeIdentifier: typeId, options: nil) { [weak self] data, _ in
            guard let self, let data else {
                completion(nil)
                return
            }

            if let url = data as? URL {
                completion(self.copyToInbox(url: url, inbox: inbox, preferredExtension: self.preferredExtension(for: typeId)))
            } else if let image = data as? UIImage, let d = image.jpegData(compressionQuality: 0.9) {
                completion(self.writeData(d, to: inbox, ext: "jpg"))
            } else if let d = data as? Data {
                completion(self.writeData(d, to: inbox, ext: self.preferredExtension(for: typeId) ?? "bin"))
            } else {
                completion(nil)
            }
        }
    }

    private func shouldPreferFileRepresentation(_ typeId: String) -> Bool {
        guard typeId != "public.url" else { return false }
        return true
    }

    private func preferredExtension(for typeId: String) -> String? {
        if typeId == UTType.jpeg.identifier || typeId == UTType.image.identifier {
            return "jpg"
        }
        return UTType(typeId)?.preferredFilenameExtension
    }

    private func copyToInbox(url: URL, inbox: URL, preferredExtension: String? = nil) -> String? {
        let isSecurityScoped = url.startAccessingSecurityScopedResource()
        defer { if isSecurityScoped { url.stopAccessingSecurityScopedResource() } }

        let rawName = url.deletingPathExtension().lastPathComponent
        let baseName = rawName.isEmpty ? "shared" : rawName
        let ext = url.pathExtension.isEmpty ? preferredExtension : url.pathExtension
        let uniqueName = "\(baseName)_\(UUID().uuidString.prefix(8))"
        let fileName = ext.map { "\(uniqueName).\($0)" } ?? uniqueName
        let dest = inbox.appendingPathComponent(fileName)

        do {
            if FileManager.default.fileExists(atPath: dest.path) { try FileManager.default.removeItem(at: dest) }
            if url.isFileURL {
                try FileManager.default.copyItem(at: url, to: dest)
            } else {
                try Data(contentsOf: url).write(to: dest)
            }
            return dest.path
        } catch {
            return nil
        }
    }

    private func writeData(_ data: Data, to inbox: URL, ext: String) -> String? {
        let name = "shared_\(UUID().uuidString.prefix(8)).\(ext)"
        let dest = inbox.appendingPathComponent(name)
        do {
            try data.write(to: dest)
            return dest.path
        } catch {
            return nil
        }
    }

    private func finalizeShare(savedPaths: [String]) {
        if savedPaths.isEmpty {
            finishWithError()
            return
        }

        // Apps often share a .sxcu or .xsdc as generic JSON/text/data without its name, so recognise it by content.
        let savedPaths = savedPaths.map(Self.renamingImportFileByContent)
        let sxcuPaths = savedPaths.filter { ($0 as NSString).pathExtension.lowercased() == "sxcu" }
        let xsdcPaths = savedPaths.filter { ($0 as NSString).pathExtension.lowercased() == "xsdc" }
        let uploadPaths = savedPaths.filter {
            let ext = ($0 as NSString).pathExtension.lowercased()
            return ext != "sxcu" && ext != "xsdc"
        }

        if !sxcuPaths.isEmpty {
            enqueueForMainApp(paths: sxcuPaths, key: pendingSxcuImportsKey)
        }

        if !xsdcPaths.isEmpty {
            enqueueForMainApp(paths: xsdcPaths, key: pendingXsdcImportsKey)
        }

        if uploadPaths.isEmpty {
            // Share extensions usually cannot open their app, so say where the import continues.
            let names = sxcuPaths.compactMap(Self.sxcuUploaderName)
            if !names.isEmpty {
                updateStatus("Open XerahS to add the \(names.joined(separator: ", ")) uploader.")
            } else if !xsdcPaths.isEmpty {
                updateStatus("Open XerahS and enter the passphrase to import the destination config.")
            } else {
                updateStatus("Open XerahS to finish importing.")
            }
            openContainingApp()
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
                self?.extensionContext?.completeRequest(returningItems: nil, completionHandler: nil)
            }
            return
        }

        updateStatus("Uploading...")
        Task { [weak self] in
            guard let self else { return }
            let results = await self.uploadService.uploadFiles(uploadPaths)
            await MainActor.run { self.finishUploadShare(uploadResults: results, hasPendingImport: !sxcuPaths.isEmpty || !xsdcPaths.isEmpty) }
        }
    }

    private func finishUploadShare(uploadResults: [ShareExtensionUploadResult], hasPendingImport: Bool) {
        let uploadedUrls = uploadResults.compactMap(\.url)
        let failedPaths = uploadResults.filter { !$0.succeeded }.map(\.filePath)

        if !uploadedUrls.isEmpty {
            UIPasteboard.general.string = uploadedUrls.joined(separator: "\n")
            removeUploadedInboxFiles(uploadResults)
        }

        if !failedPaths.isEmpty {
            enqueueForMainApp(paths: failedPaths, key: pendingPathsKey)
        }

        if hasPendingImport || !failedPaths.isEmpty {
            openContainingApp()
        }

        let cloudFailures = uploadResults.compactMap(\.cloudError)
        if !cloudFailures.isEmpty {
            updateStatus("Link copied. \(cloudFailures[0]) Cloud publishing is online-only; retry by sharing the file again.")
        } else if uploadedUrls.count == uploadResults.count {
            let status = uploadedUrls.count == 1
                ? "Link copied to Clipboard."
                : "\(uploadedUrls.count) links copied to Clipboard."
            updateStatus(status)
        } else if !uploadedUrls.isEmpty {
            updateStatus("\(uploadedUrls.count) link(s) copied. \(failedPaths.count) file(s) queued for XerahS.")
        } else {
            let firstError = uploadResults.compactMap(\.error).first ?? "Open XerahS to finish uploading."
            updateStatus(firstError)
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) { [weak self] in
            self?.extensionContext?.completeRequest(returningItems: nil, completionHandler: nil)
        }
    }

    /// A small JSON object with a RequestURL is a ShareX custom uploader, and one whose Format is
    /// XerahS.DestinationConfig is an encrypted destination config, whatever they were shared as;
    /// give them their extension so they are imported instead of uploaded.
    private static func renamingImportFileByContent(_ path: String) -> String {
        let ext = (path as NSString).pathExtension.lowercased()
        guard ["", "json", "txt", "text", "bin", "data"].contains(ext),
              let size = (try? FileManager.default.attributesOfItem(atPath: path)[.size]) as? Int,
              size <= 64 * 1024 else { return path }
        let targetExt: String
        if sxcuDefinition(atPath: path) != nil {
            targetExt = "sxcu"
        } else if isDestinationConfig(atPath: path) {
            targetExt = "xsdc"
        } else {
            return path
        }
        let base = ext.isEmpty ? path : (path as NSString).deletingPathExtension
        let renamed = base + "." + targetExt
        return (try? FileManager.default.moveItem(atPath: path, toPath: renamed)) != nil ? renamed : path
    }

    private static func isDestinationConfig(atPath path: String) -> Bool {
        guard let data = FileManager.default.contents(atPath: path),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return false }
        return object["Format"] as? String == "XerahS.DestinationConfig"
    }

    private static func sxcuDefinition(atPath path: String) -> SxcuDefinition? {
        guard let data = FileManager.default.contents(atPath: path),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let requestURL = object["RequestURL"] as? String, !requestURL.isEmpty else { return nil }
        return try? JSONDecoder().decode(SxcuDefinition.self, from: data)
    }

    private static func sxcuUploaderName(_ path: String) -> String? {
        sxcuDefinition(atPath: path).map { CustomUploaderEntry.from(sxcu: $0).displayName }
    }

    private func enqueueForMainApp(paths: [String], key: String) {
        guard !paths.isEmpty else { return }
        let defaults = UserDefaults(suiteName: appGroupId)
        var pending = (defaults?.array(forKey: key) as? [String]) ?? []
        pending.append(contentsOf: paths)
        defaults?.set(pending, forKey: key)
        defaults?.synchronize()
    }

    private func removeUploadedInboxFiles(_ results: [ShareExtensionUploadResult]) {
        for result in results where result.succeeded {
            try? FileManager.default.removeItem(atPath: result.filePath)
        }
    }

    private func openContainingApp() {
        if let url = URL(string: openAppURLString) {
            extensionContext?.open(url, completionHandler: nil)
        }
    }

    private func finishWithError() {
        updateStatus("No supported item was shared.")
        extensionContext?.cancelRequest(withError: NSError(domain: "XerahS.Share", code: -1, userInfo: [NSLocalizedDescriptionKey: "No supported items to share."]))
    }
}
