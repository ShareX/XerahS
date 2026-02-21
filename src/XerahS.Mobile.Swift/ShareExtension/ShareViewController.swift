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

private let appGroupId = "group.com.getsharex.xerahs"
private let pendingPathsKey = "PendingSharedPaths"
private let openAppURLString = "xerahs://share"

final class ShareViewController: UIViewController {

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        handleSharedItems()
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
        let inbox = groupContainer.appendingPathComponent("ShareInbox", isDirectory: true)
        try? FileManager.default.createDirectory(at: inbox, withIntermediateDirectories: true)

        let supportedTypes: [String] = [
            UTType.image.identifier,
            UTType.jpeg.identifier,
            UTType.png.identifier,
            UTType.pdf.identifier,
            UTType.data.identifier,
            "public.file-url",
            "public.url"
        ]
        var savedPaths: [String] = []
        let group = DispatchGroup()
        let lock = NSLock()

        for item in extensionItems {
            guard let attachments = item.attachments else { continue }
            for provider in attachments {
                for typeId in supportedTypes {
                    if provider.hasItemConformingToTypeIdentifier(typeId) {
                        group.enter()
                        provider.loadItem(forTypeIdentifier: typeId, options: nil) { data, _ in
                            defer { group.leave() }
                            guard let data = data else { return }
                            var path: String?
                            if let url = data as? URL {
                                path = self.copyToInbox(url: url, inbox: inbox)
                            } else if let image = data as? UIImage, let d = image.jpegData(compressionQuality: 0.9) {
                                path = self.writeData(d, to: inbox, ext: "jpg")
                            } else if let d = data as? Data {
                                path = self.writeData(d, to: inbox, ext: "bin")
                            }
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

    private func copyToInbox(url: URL, inbox: URL) -> String? {
        let isSecurityScoped = url.startAccessingSecurityScopedResource()
        defer { if isSecurityScoped { url.stopAccessingSecurityScopedResource() } }
        let name = url.lastPathComponent.isEmpty ? "shared_\(UUID().uuidString.prefix(8))" : url.lastPathComponent
        let dest = inbox.appendingPathComponent(name)
        do {
            if FileManager.default.fileExists(atPath: dest.path) { try FileManager.default.removeItem(at: dest) }
            try FileManager.default.copyItem(at: url, to: dest)
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
        let defaults = UserDefaults(suiteName: appGroupId)
        var pending = (defaults?.array(forKey: pendingPathsKey) as? [String]) ?? []
        pending.append(contentsOf: savedPaths)
        defaults?.set(pending, forKey: pendingPathsKey)
        defaults?.synchronize()
        if let url = URL(string: openAppURLString) {
            extensionContext?.open(url, completionHandler: nil)
        }
        extensionContext?.completeRequest(returningItems: nil, completionHandler: nil)
    }

    private func finishWithError() {
        extensionContext?.cancelRequest(withError: NSError(domain: "XerahS.Share", code: -1, userInfo: [NSLocalizedDescriptionKey: "No supported items to share."]))
    }
}
