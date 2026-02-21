//
//  SettingsHubScreen.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI

struct SettingsHubScreen: View {
    let settingsRepository: SettingsRepository
    var onBack: () -> Void
    var onNavigateToS3: () -> Void
    var onNavigateToCustomUploader: () -> Void

    private var config: ApplicationConfig { settingsRepository.load() }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack {
                Button("Back", action: onBack)
                Spacer()
            }
            .padding(.horizontal)

            Text("Settings")
                .font(.title2)
                .padding(.horizontal)

            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Upload Destinations")
                            .font(.headline)
                        Text("Configure where your files will be uploaded. Amazon S3 and custom uploaders are supported.")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(16)
                    .background(Color(.secondarySystemBackground), in: RoundedRectangle(cornerRadius: 10))

                    Button(action: onNavigateToS3) {
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("Amazon S3")
                                    .font(.subheadline.weight(.medium))
                                Text(config.s3Config.isConfigured ? "Bucket: \(config.s3Config.bucketName)" : "Not configured - tap to set up")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            Spacer()
                        }
                        .padding(16)
                        .background(Color(.secondarySystemBackground), in: RoundedRectangle(cornerRadius: 10))
                    }
                    .buttonStyle(.plain)

                    Button(action: onNavigateToCustomUploader) {
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("Custom Uploader")
                                    .font(.subheadline.weight(.medium))
                                Text(config.customUploaders.isEmpty ? "Not configured - tap to add" : "\(config.customUploaders.count) uploader(s)")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            Spacer()
                        }
                        .padding(16)
                        .background(Color(.secondarySystemBackground), in: RoundedRectangle(cornerRadius: 10))
                    }
                    .buttonStyle(.plain)
                }
                .padding(.horizontal)
            }
        }
    }
}
