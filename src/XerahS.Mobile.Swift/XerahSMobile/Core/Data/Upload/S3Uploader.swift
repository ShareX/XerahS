//
//  S3Uploader.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

/// Upload a file to S3 using AWS Signature V4 and PUT. Returns the object URL on success.
/// For production, consider AWS SDK for Swift; this is a minimal implementation.
final class S3Uploader {
    func uploadFile(filePath: String, config: S3Config) -> UploadOutcome {
        guard config.isConfigured else { return .failure(error: "S3 is not configured") }
        guard FileManager.default.fileExists(atPath: filePath) else { return .failure(error: "File not found") }
        let fileUrl = URL(fileURLWithPath: filePath)
        let key = "uploads/\(fileUrl.lastPathComponent)"
        let bucket = config.bucketName
        let region = config.region

        let host: String
        let urlString: String
        if !config.customEndpoint.isEmpty {
            let base = config.customEndpoint.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
            host = base.replacingOccurrences(of: "https://", with: "").replacingOccurrences(of: "http://", with: "")
            urlString = "https://\(host)/\(bucket)/\(key)"
        } else {
            host = "\(bucket).s3.\(region).amazonaws.com"
            urlString = "https://\(host)/\(key)"
        }

        guard let url = URL(string: urlString) else { return .failure(error: "Invalid URL") }
        guard let data = try? Data(contentsOf: fileUrl) else { return .failure(error: "Cannot read file") }

        let now = Date()
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let amzDate = formatter.string(from: now)
        let dateStamp: String = {
            let cal = Calendar(identifier: .iso8601)
            let comp = cal.dateComponents([.year, .month, .day], from: now)
            return String(format: "%04d%02d%02d", comp.year!, comp.month!, comp.day!)
        }()
        let payloadHash = data.sha256Hex
        let contentType = "application/octet-stream"

        var request = URLRequest(url: url)
        request.httpMethod = "PUT"
        request.httpBody = data
        request.setValue(contentType, forHTTPHeaderField: "Content-Type")
        request.setValue(payloadHash, forHTTPHeaderField: "x-amz-content-sha256")
        request.setValue(amzDate, forHTTPHeaderField: "x-amz-date")

        let signedHeaders = "content-type;host;x-amz-content-sha256;x-amz-date"
        let canonicalRequest = [
            "PUT",
            "/\(key)",
            "",
            "content-type:\(contentType)",
            "host:\(host)",
            "x-amz-content-sha256:\(payloadHash)",
            "x-amz-date:\(amzDate)",
            "",
            signedHeaders,
            payloadHash
        ].joined(separator: "\n")

        let credScope = "\(dateStamp)/\(region)/s3/aws4_request"
        let stringToSign = [
            "AWS4-HMAC-SHA256",
            amzDate,
            credScope,
            canonicalRequest.sha256Hex
        ].joined(separator: "\n")

        let kSecret = "AWS4\(config.secretAccessKey)"
        let kDate = HMAC.sha256(key: kSecret.data(using: .utf8)!, data: dateStamp.data(using: .utf8)!)
        let kRegion = HMAC.sha256(key: kDate, data: region.data(using: .utf8)!)
        let kService = HMAC.sha256(key: kRegion, data: "s3".data(using: .utf8)!)
        let kSigning = HMAC.sha256(key: kService, data: "aws4_request".data(using: .utf8)!)
        let signature = HMAC.sha256Hex(key: kSigning, data: stringToSign.data(using: .utf8)!)

        let auth = "AWS4-HMAC-SHA256 Credential=\(config.accessKeyId)/\(credScope), SignedHeaders=\(signedHeaders), Signature=\(signature)"
        request.setValue(auth, forHTTPHeaderField: "Authorization")

        var outcome: UploadOutcome?
        let sem = DispatchSemaphore(value: 0)
        let task = URLSession.shared.dataTask(with: request) { _, response, error in
            if let error = error {
                outcome = .failure(error: error.localizedDescription)
                sem.signal()
                return
            }
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            if code >= 200 && code < 300 {
                outcome = .success(url: urlString)
            } else {
                outcome = .failure(error: "S3 returned HTTP \(code)")
            }
            sem.signal()
        }
        task.resume()
        sem.wait()
        return outcome ?? .failure(error: "S3 upload failed")
    }
}

import CryptoKit
extension Data {
    var sha256Hex: String {
        let hash = SHA256.hash(data: self)
        return hash.map { String(format: "%02x", $0) }.joined()
    }
}
extension String {
    var sha256Hex: String { Data(utf8).sha256Hex }
}
enum HMAC {
    static func sha256(key: Data, data: Data) -> Data {
        let symKey = SymmetricKey(data: key)
        let signature = HMAC<SHA256>.authenticationCode(for: data, using: symKey)
        return Data(signature)
    }
    static func sha256Hex(key: Data, data: Data) -> String {
        sha256(key: key, data: data).map { String(format: "%02x", $0) }.joined()
    }
}
