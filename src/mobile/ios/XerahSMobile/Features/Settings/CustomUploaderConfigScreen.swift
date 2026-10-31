//
//  CustomUploaderConfigScreen.swift
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

import SwiftUI
import UIKit
import UniformTypeIdentifiers

struct CustomUploaderConfigScreen: View {
    @ObservedObject var viewModel: CustomUploaderConfigViewModel
    @State private var isImportingFile = false

    var body: some View {
        List {
            if let status = viewModel.statusMessage, !status.isEmpty {
                Section {
                    HStack(spacing: 12) {
                        Image(systemName: viewModel.isStatusError ? "exclamationmark.triangle.fill" : "checkmark.circle.fill")
                            .foregroundStyle(viewModel.isStatusError ? .red : .green)

                        Text(status)
                            .foregroundStyle(viewModel.isStatusError ? .red : .primary)

                        Spacer()

                        if viewModel.isStatusError, viewModel.importErrorDetails != nil {
                            Button {
                                viewModel.copyImportErrorToClipboard()
                            } label: {
                                Label("Copy Error", systemImage: "doc.on.doc")
                            }
                            .labelStyle(.iconOnly)
                        }
                    }
                }
            }

            Section {
                // PasteButton reads the clipboard without the iOS paste-permission prompt.
                PasteButton(payloadType: String.self) { strings in
                    guard let text = strings.first else { return }
                    Task { @MainActor in viewModel.importSxcu(text: text, source: "clipboard") }
                }
                .buttonBorderShape(.capsule)

                Button {
                    isImportingFile = true
                } label: {
                    Label("Choose .sxcu File", systemImage: "folder")
                }
            } header: {
                Text("Import")
            } footer: {
                Text("Paste the contents of a ShareX `.sxcu` file, or pick one from Files.")
            }

            Section {
                if viewModel.uploaders.isEmpty {
                    ContentUnavailableView(
                        "No Custom Uploaders",
                        systemImage: "curlybraces",
                        description: Text("Import a `.sxcu` above or create one manually with +.")
                    )
                } else {
                    ForEach(viewModel.uploaders) { entry in
                        CustomUploaderRow(
                            entry: entry,
                            onEdit: { viewModel.edit(entry) },
                            onCopy: { viewModel.copySxcuToClipboard(entry) },
                            onDelete: { viewModel.delete(entry) }
                        )
                    }
                }
            } footer: {
                Text("Custom uploaders use the same request, body, and response shape as desktop.")
            }
        }
        .navigationTitle("Custom Uploader")
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button {
                    viewModel.addNew()
                } label: {
                    Label("New Custom Uploader", systemImage: "plus")
                }
            }
        }
        .onAppear { viewModel.refresh() }
        .alert(item: $viewModel.importAlert) { result in
            Alert(title: Text(result.title), message: Text(result.message))
        }
        .fileImporter(
            isPresented: $isImportingFile,
            allowedContentTypes: CustomUploaderConfigViewModel.importableTypes
        ) { result in
            switch result {
            case .success(let url):
                viewModel.importSxcu(fileURL: url)
            case .failure(let error):
                viewModel.reportImportFailure(error)
            }
        }
        .sheet(item: $viewModel.editingEntry) { entry in
            CustomUploaderEditSheet(
                entry: entry,
                onDismiss: { viewModel.cancelEdit() },
                onSave: { viewModel.saveEdit($0) }
            )
        }
    }
}

private struct CustomUploaderRow: View {
    let entry: CustomUploaderEntry
    var onEdit: () -> Void
    var onCopy: () -> Void
    var onDelete: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: "curlybraces")
                .foregroundStyle(.blue)

            VStack(alignment: .leading, spacing: 4) {
                Text(entry.displayName)
                    .font(.headline)
                Text(entry.requestUrl.isEmpty ? "No URL configured" : entry.requestUrl)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
                Text("\(entry.requestMethod.rawValue) - \(entry.bodyType.rawValue) - \(entry.destinationType)")
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.secondary)
            }

            Spacer()

            Menu {
                Button(action: onEdit) {
                    Label("Edit", systemImage: "pencil")
                }
                Button(action: onCopy) {
                    Label("Copy .sxcu", systemImage: "doc.on.doc")
                }
                Button(role: .destructive, action: onDelete) {
                    Label("Delete", systemImage: "trash")
                }
            } label: {
                Label("Actions", systemImage: "ellipsis.circle")
                    .labelStyle(.iconOnly)
            }
        }
    }
}

private struct CustomUploaderEditSheet: View {
    let entry: CustomUploaderEntry
    var onDismiss: () -> Void
    var onSave: (CustomUploaderEntry) -> Void

    @State private var name: String = ""
    @State private var destinationType: String = ""
    @State private var requestMethod: CustomUploaderRequestMethod = .POST
    @State private var requestUrl: String = ""
    @State private var bodyType: CustomUploaderBodyType = .multipartFormData
    @State private var fileFormName: String = "file"
    @State private var parametersText: String = ""
    @State private var headersText: String = ""
    @State private var argumentsText: String = ""
    @State private var dataText: String = ""
    @State private var urlText: String = ""
    @State private var deletionUrlText: String = ""
    @State private var errorMessageText: String = ""

    var body: some View {
        NavigationStack {
            Form {
                Section("Basic") {
                    TextField("Name", text: $name)
                    TextField("Destination Type", text: $destinationType)
                        .textInputAutocapitalization(.never)
                    Picker("Request Method", selection: $requestMethod) {
                        ForEach(CustomUploaderRequestMethod.allCases, id: \.self) { method in
                            Text(method.rawValue).tag(method)
                        }
                    }
                    TextField("Request URL", text: $requestUrl, axis: .vertical)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.URL)
                        .lineLimit(2...4)
                    Picker("Body Type", selection: $bodyType) {
                        ForEach(CustomUploaderBodyType.allCases, id: \.self) { type in
                            Text(type.rawValue).tag(type)
                        }
                    }
                    TextField("File Form Name", text: $fileFormName)
                        .textInputAutocapitalization(.never)
                }

                Section("Parameters") {
                    KeyValueTextEditor(text: $parametersText, prompt: "key=value")
                }

                Section("Headers") {
                    KeyValueTextEditor(text: $headersText, prompt: "Header=Value")
                }

                Section("Arguments") {
                    KeyValueTextEditor(text: $argumentsText, prompt: "field=value")
                }

                Section("Body Data") {
                    TextEditor(text: $dataText)
                        .frame(minHeight: 120)
                        .font(.system(.body, design: .monospaced))
                }

                Section("Response Parsing") {
                    TextField("URL template", text: $urlText, axis: .vertical)
                        .textInputAutocapitalization(.never)
                        .lineLimit(2...4)
                    TextField("Deletion URL template", text: $deletionUrlText, axis: .vertical)
                        .textInputAutocapitalization(.never)
                        .lineLimit(2...4)
                    TextField("Error message template", text: $errorMessageText, axis: .vertical)
                        .textInputAutocapitalization(.never)
                        .lineLimit(2...4)
                }
            }
            .navigationTitle(entry.id.isEmpty ? "New .sxcu" : "Edit .sxcu")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel", action: onDismiss)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        var updated = entry
                        updated.name = name.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.destinationType = destinationType.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.requestMethod = requestMethod
                        updated.requestUrl = requestUrl.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.bodyType = bodyType
                        updated.fileFormName = fileFormName.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.parameters = KeyValueCodec.decode(parametersText)
                        updated.headers = KeyValueCodec.decode(headersText)
                        updated.arguments = KeyValueCodec.decode(argumentsText)
                        updated.data = dataText
                        updated.url = urlText.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.deletionUrl = deletionUrlText.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.errorMessage = errorMessageText.trimmingCharacters(in: .whitespacesAndNewlines)
                        onSave(updated)
                    }
                }
            }
            .onAppear {
                name = entry.name
                destinationType = entry.destinationType
                requestMethod = entry.requestMethod
                requestUrl = entry.requestUrl
                bodyType = entry.bodyType
                fileFormName = entry.fileFormName
                parametersText = KeyValueCodec.encode(entry.parameters)
                headersText = KeyValueCodec.encode(entry.headers)
                argumentsText = KeyValueCodec.encode(entry.arguments)
                dataText = entry.data
                urlText = entry.url.isEmpty ? entry.legacyUrlExpression : entry.url
                deletionUrlText = entry.deletionUrl
                errorMessageText = entry.errorMessage
            }
        }
    }
}

private struct KeyValueTextEditor: View {
    @Binding var text: String
    let prompt: String

    var body: some View {
        ZStack(alignment: .topLeading) {
            if text.isEmpty {
                Text(prompt)
                    .foregroundStyle(.tertiary)
                    .padding(.top, 8)
                    .padding(.leading, 4)
            }

            TextEditor(text: $text)
                .frame(minHeight: 110)
                .font(.system(.body, design: .monospaced))
                .textInputAutocapitalization(.never)
        }
    }
}

final class CustomUploaderConfigViewModel: ObservableObject {
    @Published var uploaders: [CustomUploaderEntry] = []
    @Published var editingEntry: CustomUploaderEntry?
    @Published var statusMessage: String?
    @Published var isStatusError: Bool = false
    @Published var importErrorDetails: String?
    @Published var importAlert: ImportResultAlert?

    private let settingsRepository: SettingsRepository
    private let decoder = JSONDecoder()
    private let encoder = JSONEncoder()

    init(settingsRepository: SettingsRepository) {
        self.settingsRepository = settingsRepository
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
    }

    func refresh() {
        uploaders = settingsRepository.loadCustomUploaders()
    }

    func addNew() {
        editingEntry = CustomUploaderEntry(
            id: "custom_\(UUID().uuidString.prefix(8))",
            name: "New Uploader",
            destinationType: "FileUploader",
            requestMethod: .POST,
            requestUrl: "",
            bodyType: .multipartFormData,
            fileFormName: "file"
        )
    }

    func edit(_ entry: CustomUploaderEntry) {
        editingEntry = entry
    }

    func saveEdit(_ entry: CustomUploaderEntry) {
        var list = uploaders
        if let idx = list.firstIndex(where: { $0.id == entry.id }) {
            list[idx] = entry
        } else {
            list.append(entry)
        }
        settingsRepository.saveCustomUploaders(list)
        uploaders = list
        editingEntry = nil
        setStatus("Saved \(entry.displayName).")
    }

    func cancelEdit() {
        editingEntry = nil
    }

    func delete(_ entry: CustomUploaderEntry) {
        uploaders = uploaders.filter { $0.id != entry.id }
        settingsRepository.saveCustomUploaders(uploaders)
        setStatus("Deleted \(entry.displayName).")
    }

    static let importableTypes: [UTType] = [
        UTType("com.xerahs.xerahs.sxcu") ?? .json, .json, .plainText, .data
    ]

    func importSxcu(text: String, source: String) {
        let cleaned = stripMarkdownCodeFence(from: text.trimmingCharacters(in: .whitespacesAndNewlines))
        importSxcu(payload: Data(cleaned.utf8), source: source, sourceText: text)
    }

    func importSxcu(fileURL: URL) {
        let accessing = fileURL.startAccessingSecurityScopedResource()
        defer { if accessing { fileURL.stopAccessingSecurityScopedResource() } }
        do {
            importSxcu(payload: try Data(contentsOf: fileURL), source: fileURL.lastPathComponent, sourceText: nil)
        } catch {
            reportImportFailure(error)
        }
    }

    func reportImportFailure(_ error: Error) {
        setStatus("Could not read the .sxcu file: \(error.localizedDescription)", isError: true)
    }

    private func importSxcu(payload: Data, source: String, sourceText: String?) {
        do {
            let definition = try decoder.decode(SxcuDefinition.self, from: payload)
            guard !definition.requestURL.trimmingCharacters(in: .whitespaces).isEmpty else {
                throw CocoaError(.coderValueNotFound, userInfo: [NSLocalizedDescriptionKey: "The .sxcu has no RequestURL."])
            }
            var entry = CustomUploaderEntry.from(sxcu: definition)
            var list = uploaders
            // Re-importing the same uploader (same name and request URL) updates it instead of adding a copy.
            let existingIndex = list.firstIndex {
                $0.requestUrl.caseInsensitiveCompare(entry.requestUrl) == .orderedSame &&
                    $0.name.caseInsensitiveCompare(entry.name) == .orderedSame
            }
            if let existingIndex {
                entry.id = list[existingIndex].id
                list[existingIndex] = entry
            } else {
                list.append(entry)
            }
            settingsRepository.saveCustomUploaders(list)
            if settingsRepository.getDefaultDestinationInstanceId() == nil {
                settingsRepository.setDefaultDestinationInstanceId(entry.id)
            }
            uploaders = list
            let message = existingIndex == nil
                ? "Added \(entry.displayName) from \(source)."
                : "Updated \(entry.displayName) from \(source)."
            setStatus(message)
            importAlert = ImportResultAlert(succeeded: true, message: message)
        } catch {
            setStatus(
                "Failed to import .sxcu from \(source): \(error.localizedDescription)",
                isError: true,
                details: clipboardDiagnostics(
                    payload: payload,
                    cleanedString: sourceText?.trimmingCharacters(in: .whitespacesAndNewlines),
                    error: error
                )
            )
        }
    }

    func copyImportErrorToClipboard() {
        guard let importErrorDetails else { return }
        UIPasteboard.general.string = importErrorDetails
        setStatus("Copied import error details to clipboard.")
    }

    func copySxcuToClipboard(_ entry: CustomUploaderEntry) {
        do {
            let data = try encoder.encode(entry.toSxcuDefinition())
            UIPasteboard.general.string = String(decoding: data, as: UTF8.self)
            setStatus("Copied \(entry.displayName) as .sxcu JSON.")
        } catch {
            setStatus("Failed to encode .sxcu JSON: \(error.localizedDescription)", isError: true)
        }
    }

    private func stripMarkdownCodeFence(from input: String) -> String {
        guard input.hasPrefix("```") else { return input }
        let lines = input.components(separatedBy: .newlines)
        guard lines.count >= 3 else { return input }
        return lines.dropFirst().dropLast().joined(separator: "\n")
    }

    private func clipboardDiagnostics(payload: Data?, cleanedString: String?, error: Error? = nil) -> String {
        let cleaned = cleanedString.map(stripMarkdownCodeFence(from:))?.trimmingCharacters(in: .whitespacesAndNewlines)
        let preview = cleaned.map { String($0.prefix(400)) }
        let payloadText = payload.flatMap { String(data: $0, encoding: .utf8) }
        let decodedJsonKeys: String? = {
            guard
                let payload,
                let object = try? JSONSerialization.jsonObject(with: payload) as? [String: Any]
            else { return nil }
            return object.keys.sorted().joined(separator: ", ")
        }()

        var lines: [String] = [
            "SXCU import failed",
            "Timestamp: \(ISO8601DateFormatter().string(from: Date()))",
            "Payload bytes: \(payload?.count ?? 0)"
        ]

        if let error {
            lines.append("Error: \(String(describing: error))")
        }
        if let decodedJsonKeys, !decodedJsonKeys.isEmpty {
            lines.append("Decoded top-level keys: \(decodedJsonKeys)")
        }
        if let preview, !preview.isEmpty {
            lines.append("Input preview:")
            lines.append(preview)
        } else if let payloadText, !payloadText.isEmpty {
            lines.append("Payload preview:")
            lines.append(String(payloadText.prefix(400)))
        }

        return lines.joined(separator: "\n")
    }

    private func setStatus(_ message: String, isError: Bool = false, details: String? = nil) {
        statusMessage = message
        isStatusError = isError
        importErrorDetails = isError ? details : nil
    }
}

private enum KeyValueCodec {
    static func encode(_ value: [String: String]) -> String {
        value.keys.sorted().map { key in
            "\(key)=\(value[key] ?? "")"
        }.joined(separator: "\n")
    }

    static func decode(_ text: String) -> [String: String] {
        var result: [String: String] = [:]

        for rawLine in text.components(separatedBy: .newlines) {
            let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !line.isEmpty else { continue }

            if let equals = line.firstIndex(of: "=") {
                let key = String(line[..<equals]).trimmingCharacters(in: .whitespacesAndNewlines)
                let value = String(line[line.index(after: equals)...]).trimmingCharacters(in: .whitespacesAndNewlines)
                if !key.isEmpty { result[key] = value }
                continue
            }

            if let colon = line.firstIndex(of: ":") {
                let key = String(line[..<colon]).trimmingCharacters(in: .whitespacesAndNewlines)
                let value = String(line[line.index(after: colon)...]).trimmingCharacters(in: .whitespacesAndNewlines)
                if !key.isEmpty { result[key] = value }
            }
        }

        return result
    }
}
