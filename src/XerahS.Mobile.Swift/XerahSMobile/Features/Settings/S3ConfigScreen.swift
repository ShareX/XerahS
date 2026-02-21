//
//  S3ConfigScreen.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
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
        settingsRepository.saveS3Config(config)
        return true
    }

    func clearValidationError() { validationError = nil }
}

private extension Array {
    subscript(safe index: Int) -> Element? {
        indices.contains(index) ? self[index] : nil
    }
}
