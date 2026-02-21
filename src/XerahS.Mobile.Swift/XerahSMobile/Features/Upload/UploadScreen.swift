//
//  UploadScreen.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI
import Combine

struct UploadScreen: View {
    @ObservedObject var worker: UploadQueueWorker
    var onOpenHistory: () -> Void
    var onOpenSettings: () -> Void
    var onCopyToClipboard: (String) -> Void
    var initialPaths: [String]?

    @State private var statusText = "Share files to XerahS to upload them."
    @State private var isUploading = false
    @State private var results: [UploadResultItem] = []

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack {
                Text("XerahS")
                    .font(.title2)
                Spacer()
                Button("History", action: onOpenHistory)
                    .buttonStyle(.bordered)
                Button("Settings", action: onOpenSettings)
                    .buttonStyle(.bordered)
            }
            .padding(.horizontal)

            ScrollView {
                VStack(alignment: .center, spacing: 12) {
                    Text("Share & Upload")
                        .font(.headline)
                    Text(statusText)
                        .font(.body)
                        .multilineTextAlignment(.center)
                    if isUploading {
                        ProgressView()
                            .padding(.top, 8)
                    }
                    ForEach(Array(results.enumerated()), id: \.offset) { _, item in
                        ResultCard(item: item, onCopyToClipboard: onCopyToClipboard)
                    }
                }
                .frame(maxWidth: .infinity)
                .padding()
            }
        }
        .onReceive(worker.state.receive(on: DispatchQueue.main)) { state in
            isUploading = state.processing
            statusText = state.processing
                ? "Uploading \(state.pendingCount) file(s)..."
                : state.pendingCount > 0
                    ? "Queued \(state.pendingCount) file(s)."
                    : results.isEmpty ? "Share files to XerahS to upload them." : "Done."
        }
        .onReceive(worker.itemCompleted.receive(on: DispatchQueue.main).compactMap { $0 }) { result in
            results.append(result)
        }
        .onAppear {
            worker.updateState()
            if let paths = initialPaths, !paths.isEmpty {
                _ = worker.enqueueFiles(paths)
            }
        }
        .onChange(of: initialPaths) { _, newValue in
            if let paths = newValue, !paths.isEmpty {
                _ = worker.enqueueFiles(paths)
            }
        }
    }
}

private struct ResultCard: View {
    let item: UploadResultItem
    var onCopyToClipboard: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(item.fileName)
                .font(.subheadline.weight(.semibold))
            if item.hasUrl, let url = item.url {
                Text(url)
                    .font(.caption)
                    .foregroundStyle(.blue)
                    .lineLimit(2)
                Button("Copy URL") { onCopyToClipboard(url) }
                    .buttonStyle(.bordered)
            }
            if !item.success, let err = item.error {
                Text(err)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .lineLimit(3)
                Button("Copy Error") { onCopyToClipboard(err) }
                    .buttonStyle(.bordered)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(Color(.secondarySystemBackground), in: RoundedRectangle(cornerRadius: 10))
    }
}
