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

struct CustomUploaderConfigScreen: View {
    @ObservedObject var viewModel: CustomUploaderConfigViewModel
    var onBack: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Button("Back", action: onBack)
                .padding(.horizontal)

            Text("Custom Uploader")
                .font(.title2)
                .padding(.horizontal)

            List {
                ForEach(viewModel.uploaders) { entry in
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(entry.name.isEmpty ? "Unnamed" : entry.name)
                                .font(.subheadline.weight(.medium))
                            Text(entry.requestUrl.isEmpty ? "No URL" : entry.requestUrl)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                        Spacer()
                        Button("Edit") { viewModel.edit(entry) }
                            .buttonStyle(.bordered)
                        Button("Delete", role: .destructive) { viewModel.delete(entry) }
                            .buttonStyle(.bordered)
                    }
                    .padding(.vertical, 4)
                }
            }
            .listStyle(.plain)

            Spacer(minLength: 16)

            HStack {
                Spacer()
                Button(action: { viewModel.addNew() }) {
                    Image(systemName: "plus.circle.fill")
                        .font(.largeTitle)
                }
                .padding()
            }
        }
        .onAppear { viewModel.refresh() }
        .sheet(item: $viewModel.editingEntry) { entry in
            CustomUploaderEditSheet(entry: entry, onDismiss: { viewModel.cancelEdit() }, onSave: { viewModel.saveEdit($0) })
        }
    }
}

private struct CustomUploaderEditSheet: View {
    let entry: CustomUploaderEntry
    var onDismiss: () -> Void
    var onSave: (CustomUploaderEntry) -> Void

    @State private var name: String = ""
    @State private var requestUrl: String = ""
    @State private var fileFormName: String = "file"
    @State private var bodyText: String = ""

    var body: some View {
        NavigationStack {
            Form {
                TextField("Name", text: $name)
                TextField("Request URL", text: $requestUrl)
                    .keyboardType(.URL)
                    .autocapitalization(.none)
                TextField("File form name", text: $fileFormName)
                TextField("Body (optional)", text: $bodyText, axis: .vertical)
                    .lineLimit(3...6)
            }
            .navigationTitle(entry.id.isEmpty ? "New Uploader" : "Edit Uploader")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel", action: onDismiss)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        var updated = entry
                        updated.name = name.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.requestUrl = requestUrl.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.fileFormName = fileFormName.isEmpty ? "file" : fileFormName.trimmingCharacters(in: .whitespacesAndNewlines)
                        updated.body = bodyText.trimmingCharacters(in: .whitespacesAndNewlines)
                        onSave(updated)
                    }
                }
            }
            .onAppear {
                name = entry.name
                requestUrl = entry.requestUrl
                fileFormName = entry.fileFormName
                bodyText = entry.body
            }
        }
    }
}

final class CustomUploaderConfigViewModel: ObservableObject {
    @Published var uploaders: [CustomUploaderEntry] = []
    @Published var editingEntry: CustomUploaderEntry?

    private let settingsRepository: SettingsRepository

    init(settingsRepository: SettingsRepository) {
        self.settingsRepository = settingsRepository
    }

    func refresh() {
        uploaders = settingsRepository.loadCustomUploaders()
    }

    func addNew() {
        editingEntry = CustomUploaderEntry(
            id: "custom_\(UUID().uuidString.prefix(8))",
            name: "New Uploader",
            requestUrl: "",
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
    }

    func cancelEdit() {
        editingEntry = nil
    }

    func delete(_ entry: CustomUploaderEntry) {
        uploaders = uploaders.filter { $0.id != entry.id }
        settingsRepository.saveCustomUploaders(uploaders)
    }
}
