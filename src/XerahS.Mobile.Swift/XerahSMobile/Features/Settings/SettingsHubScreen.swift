//
//  SettingsHubScreen.swift
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

struct SettingsHubScreen: View {
    let settingsRepository: SettingsRepository
    var onBack: () -> Void
    var onNavigateToS3: () -> Void
    var onNavigateToCustomUploader: () -> Void

    @State private var convertHeicToPng: Bool = true

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
                        Text("Upload options")
                            .font(.headline)
                        Text("Convert HEIC/HEIF images to PNG before upload so they display in browsers instead of prompting download.")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(16)
                    .background(Color(.secondarySystemBackground), in: RoundedRectangle(cornerRadius: 10))

                    Toggle(isOn: Binding(
                        get: { convertHeicToPng },
                        set: { newValue in
                            convertHeicToPng = newValue
                            settingsRepository.setConvertHeicToPng(newValue)
                        }
                    )) {
                        Text("Convert HEIC/HEIF to PNG before upload")
                            .font(.subheadline)
                    }
                    .padding(16)
                    .background(Color(.secondarySystemBackground), in: RoundedRectangle(cornerRadius: 10))
                    .onAppear { convertHeicToPng = settingsRepository.getConvertHeicToPng() }

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
