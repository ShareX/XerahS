//
//  S3ConfigScreen.swift
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

struct S3RegionOption: Identifiable {
    let id: String
    let displayName: String
}

private let s3Regions: [S3RegionOption] = [
    .init(id: "us-east-1", displayName: "US East (N. Virginia)"),
    .init(id: "us-east-2", displayName: "US East (Ohio)"),
    .init(id: "us-west-1", displayName: "US West (N. California)"),
    .init(id: "us-west-2", displayName: "US West (Oregon)"),
    .init(id: "ap-south-1", displayName: "Asia Pacific (Mumbai)"),
    .init(id: "ap-southeast-1", displayName: "Asia Pacific (Singapore)"),
    .init(id: "ap-southeast-2", displayName: "Asia Pacific (Sydney)"),
    .init(id: "ap-northeast-1", displayName: "Asia Pacific (Tokyo)"),
    .init(id: "eu-central-1", displayName: "Europe (Frankfurt)"),
    .init(id: "eu-west-1", displayName: "Europe (Ireland)"),
    .init(id: "eu-west-2", displayName: "Europe (London)"),
    .init(id: "ca-central-1", displayName: "Canada (Central)"),
]

struct S3ConfigScreen: View {
    @ObservedObject var viewModel: S3ConfigViewModel
    var onBack: () -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Button("Back", action: onBack)

                Text("Amazon S3")
                    .font(.title2)

                if let err = viewModel.validationError {
                    Text(err)
                        .font(.caption)
                        .foregroundStyle(.red)
                }

                TextField("Access Key ID", text: $viewModel.accessKeyId)
                    .textFieldStyle(.roundedBorder)
                    .autocapitalization(.none)
                    .onChange(of: viewModel.accessKeyId) { _, _ in viewModel.clearValidationError() }

                SecureField("Secret Access Key", text: $viewModel.secretAccessKey)
                    .textFieldStyle(.roundedBorder)
                    .onChange(of: viewModel.secretAccessKey) { _, _ in viewModel.clearValidationError() }

                TextField("Bucket Name", text: $viewModel.bucketName)
                    .textFieldStyle(.roundedBorder)
                    .autocapitalization(.none)
                    .onChange(of: viewModel.bucketName) { _, _ in viewModel.clearValidationError() }

                Picker("Region", selection: $viewModel.regionIndex) {
                    ForEach(Array(s3Regions.enumerated()), id: \.offset) { index, option in
                        Text(option.displayName).tag(index)
                    }
                }
                .pickerStyle(.menu)

                TextField("Custom Endpoint (optional)", text: $viewModel.customEndpoint)
                    .textFieldStyle(.roundedBorder)
                    .autocapitalization(.none)
                Text("Override S3 API endpoint for MinIO or other S3-compatible storage. Leave blank for AWS.")
                    .font(.caption)
                    .foregroundStyle(.secondary)

                Toggle("Use Custom Domain (CDN)", isOn: $viewModel.useCustomDomain)
                if viewModel.useCustomDomain {
                    TextField("Custom domain URL", text: $viewModel.customDomain, prompt: Text("https://cdn.example.com"))
                        .textFieldStyle(.roundedBorder)
                        .autocapitalization(.none)
                }

                Toggle("Signed payload", isOn: $viewModel.signedPayload)
                Text("Recommended: sign request body (avoids 403 when bucket blocks public ACLs).")
                    .font(.caption)
                    .foregroundStyle(.secondary)

                Toggle("Make uploads public (public-read ACL)", isOn: $viewModel.setPublicAcl)

                Button("Save") {
                    if viewModel.save() { onBack() }
                }
                .buttonStyle(.borderedProminent)
            }
            .padding()
        }
        .onAppear { viewModel.load() }
    }
}

final class S3ConfigViewModel: ObservableObject {
    @Published var accessKeyId: String = ""
    @Published var secretAccessKey: String = ""
    @Published var bucketName: String = ""
    @Published var regionIndex: Int = 0
    @Published var customEndpoint: String = ""
    @Published var useCustomDomain: Bool = false
    @Published var customDomain: String = ""
    @Published var signedPayload: Bool = true
    @Published var setPublicAcl: Bool = false
    @Published var validationError: String?

    private let settingsRepository: SettingsRepository

    init(settingsRepository: SettingsRepository) {
        self.settingsRepository = settingsRepository
    }

    func load() {
        let config = settingsRepository.loadS3Config()
        accessKeyId = config.accessKeyId
        secretAccessKey = config.secretAccessKey
        bucketName = config.bucketName
        customEndpoint = config.customEndpoint
        useCustomDomain = config.useCustomDomain
        customDomain = config.customDomain
        signedPayload = config.signedPayload
        setPublicAcl = config.setPublicAcl
        regionIndex = s3Regions.firstIndex(where: { $0.id == config.region }) ?? 0
    }

    func save() -> Bool {
        let accessKey = accessKeyId.trimmingCharacters(in: .whitespacesAndNewlines)
        let secret = secretAccessKey.trimmingCharacters(in: .whitespacesAndNewlines)
        let bucket = bucketName.trimmingCharacters(in: .whitespacesAndNewlines)
        let region = s3Regions[safe: regionIndex]?.id ?? ""
        if accessKey.isEmpty { validationError = "Access Key is required"; return false }
        if secret.isEmpty { validationError = "Secret Key is required"; return false }
        if bucket.isEmpty { validationError = "Bucket name is required"; return false }
        if region.isEmpty { validationError = "Region is required"; return false }
        validationError = nil
        var config = S3Config()
        config.accessKeyId = accessKey
        config.secretAccessKey = secret
        config.bucketName = bucket
        config.region = region
        config.customEndpoint = customEndpoint.trimmingCharacters(in: .whitespacesAndNewlines)
        config.useCustomDomain = useCustomDomain
        config.customDomain = customDomain.trimmingCharacters(in: .whitespacesAndNewlines)
        config.signedPayload = signedPayload
        config.setPublicAcl = setPublicAcl
        settingsRepository.saveS3Config(config)
        if settingsRepository.getDefaultDestinationInstanceId() == nil {
            settingsRepository.setDefaultDestinationInstanceId(kAmazonS3DestinationId)
        }
        return true
    }

    func clearValidationError() { validationError = nil }
}

private extension Array {
    subscript(safe index: Int) -> Element? {
        indices.contains(index) ? self[index] : nil
    }
}
