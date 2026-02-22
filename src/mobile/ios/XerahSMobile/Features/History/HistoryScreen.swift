//
//  HistoryScreen.swift
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

struct HistoryScreen: View {
    @ObservedObject var viewModel: HistoryViewModel
    var onBack: () -> Void
    var onCopyToClipboard: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Button("Back", action: onBack)
                Spacer()
                Button("Refresh") { viewModel.refresh() }
                    .buttonStyle(.bordered)
                Button("Clear") { viewModel.clearAll() }
                    .buttonStyle(.bordered)
            }
            .padding(.horizontal)

            TextField("Search", text: $viewModel.searchQuery)
                .textFieldStyle(.roundedBorder)
                .padding(.horizontal)

            Text("History")
                .font(.title2)
                .padding(.horizontal)

            List {
                ForEach(viewModel.filteredEntries) { entry in
                    HistoryEntryRow(entry: entry, onCopyUrl: { onCopyToClipboard(entry.url) }, onDelete: { viewModel.deleteEntry(entry.id) })
                }
            }
            .listStyle(.plain)
        }
        .onAppear { viewModel.refresh() }
    }
}

private struct HistoryEntryRow: View {
    let entry: HistoryEntry
    var onCopyUrl: () -> Void
    var onDelete: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(entry.fileName)
                .font(.subheadline.weight(.medium))
            if !entry.url.isEmpty {
                Text(entry.url)
                    .font(.caption)
                    .foregroundStyle(.blue)
                    .lineLimit(2)
                Button("Copy URL", action: onCopyUrl)
                    .buttonStyle(.bordered)
            }
            Button("Delete", role: .destructive, action: onDelete)
                .buttonStyle(.bordered)
        }
        .padding(.vertical, 4)
    }
}

final class HistoryViewModel: ObservableObject {
    @Published var filteredEntries: [HistoryEntry] = []
    @Published var searchQuery: String = "" {
        didSet { applyFilter() }
    }

    private let historyRepository: HistoryRepository
    private var allEntries: [HistoryEntry] = []
    private let maxItems = 100

    init(historyRepository: HistoryRepository) {
        self.historyRepository = historyRepository
    }

    func refresh() {
        allEntries = historyRepository.getRecentEntries(limit: maxItems)
        applyFilter()
    }

    private func applyFilter() {
        let q = searchQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        if q.isEmpty {
            filteredEntries = allEntries
        } else {
            let lower = q.lowercased()
            filteredEntries = allEntries.filter {
                $0.fileName.lowercased().contains(lower) ||
                $0.url.lowercased().contains(lower) ||
                $0.host.lowercased().contains(lower)
            }
        }
    }

    func clearAll() -> Int {
        let count = historyRepository.clearEntries()
        refresh()
        return count
    }

    @discardableResult
    func deleteEntry(_ id: Int64) -> Bool {
        let ok = historyRepository.deleteEntry(id: id)
        if ok { refresh() }
        return ok
    }
}
