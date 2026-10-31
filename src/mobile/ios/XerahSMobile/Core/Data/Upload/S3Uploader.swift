//
//  S3Uploader.swift
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

import CryptoKit
import Foundation
import UniformTypeIdentifiers

/// MIME type for an upload, from its file extension (any case). Hosts serve objects with the type they
/// were uploaded with, so application/octet-stream makes browsers download images instead of showing them.
enum UploadContentType {
    static func forFile(atPath path: String) -> String {
        let ext = (path as NSString).pathExtension.lowercased()
        return UTType(filenameExtension: ext)?.preferredMIMEType ?? "application/octet-stream"
    }
}

/// Upload a file to S3 using AWS Signature V4 and PUT. Returns the object URL on success.
/// For production, consider AWS SDK for Swift; this is a minimal implementation.
final class S3Uploader {
    private let session: URLSession = {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 60
        config.timeoutIntervalForResource = 120
        return URLSession(configuration: config)
    }()

    func uploadFile(filePath: String, config: S3Config) -> UploadOutcome {
        guard config.isConfigured else { return .failure(UploadFailure(message: "S3 is not configured")) }
        guard FileManager.default.fileExists(atPath: filePath) else { return .failure(UploadFailure(message: "File not found")) }
        let fileUrl = URL(fileURLWithPath: filePath)
        let uploadName = UploadFileNameGenerator.uploadFileName(for: filePath)
        let key = "uploads/\(uploadName)"
        let region = config.region
        let bucketContainsDots = config.bucketName.contains(".")
        let shouldUsePathStyle = config.usePathStyle || bucketContainsDots

        guard let requestTarget = buildRequestTarget(
            bucket: config.bucketName,
            key: key,
            config: config,
            shouldUsePathStyle: shouldUsePathStyle
        ) else {
            return .failure(UploadFailure(message: "Invalid S3 request URL"))
        }

        // Result URL shown to user: custom domain (CDN) if set, else the request URL.
        let resultUrlString: String
        if config.useCustomDomain && !config.customDomain.isEmpty {
            let base = config.customDomain.trimmingCharacters(in: .whitespacesAndNewlines).trimmingCharacters(in: CharacterSet(charactersIn: "/"))
            let baseUrl = base.hasPrefix("http") ? base : "https://\(base)"
            resultUrlString = "\(baseUrl)/\(key)"
        } else {
            resultUrlString = requestTarget.requestUrlString
        }

        guard let url = URL(string: requestTarget.requestUrlString) else {
            return .failure(UploadFailure(message: "Invalid URL"))
        }
        guard let fileData = try? Data(contentsOf: fileUrl) else {
            return .failure(UploadFailure(message: "Cannot read file"))
        }

        let now = Date()
        let timestampFormatter = DateFormatter()
        timestampFormatter.calendar = Calendar(identifier: .gregorian)
        timestampFormatter.locale = Locale(identifier: "en_US_POSIX")
        timestampFormatter.timeZone = TimeZone(secondsFromGMT: 0)
        timestampFormatter.dateFormat = "yyyyMMdd'T'HHmmss'Z'"
        let amzDate = timestampFormatter.string(from: now)

        let dateStampFormatter = DateFormatter()
        dateStampFormatter.calendar = Calendar(identifier: .gregorian)
        dateStampFormatter.locale = Locale(identifier: "en_US_POSIX")
        dateStampFormatter.timeZone = TimeZone(secondsFromGMT: 0)
        dateStampFormatter.dateFormat = "yyyyMMdd"
        let dateStamp = dateStampFormatter.string(from: now)
        let payloadHash = config.signedPayload ? fileData.sha256Hex : "UNSIGNED-PAYLOAD"
        let contentType = UploadContentType.forFile(atPath: filePath)

        var request = URLRequest(url: url)
        request.httpMethod = "PUT"
        request.httpBody = fileData
        request.setValue(contentType, forHTTPHeaderField: "Content-Type")
        request.setValue(requestTarget.hostHeader, forHTTPHeaderField: "Host")
        request.setValue(payloadHash, forHTTPHeaderField: "x-amz-content-sha256")
        request.setValue(amzDate, forHTTPHeaderField: "x-amz-date")
        if config.setPublicAcl {
            request.setValue("public-read", forHTTPHeaderField: "x-amz-acl")
        }

        let (signedHeaders, canonicalHeaders): (String, String) = if config.setPublicAcl {
            ("content-type;host;x-amz-acl;x-amz-content-sha256;x-amz-date",
             "content-type:\(contentType)\nhost:\(requestTarget.hostHeader)\nx-amz-acl:public-read\nx-amz-content-sha256:\(payloadHash)\nx-amz-date:\(amzDate)")
        } else {
            ("content-type;host;x-amz-content-sha256;x-amz-date",
             "content-type:\(contentType)\nhost:\(requestTarget.hostHeader)\nx-amz-content-sha256:\(payloadHash)\nx-amz-date:\(amzDate)")
        }
        let canonicalRequest = [
            "PUT",
            requestTarget.canonicalURI,
            "",
            canonicalHeaders,
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
        let kDate = HmacUtil.sha256(key: kSecret.data(using: .utf8)!, data: dateStamp.data(using: .utf8)!)
        let kRegion = HmacUtil.sha256(key: kDate, data: region.data(using: .utf8)!)
        let kService = HmacUtil.sha256(key: kRegion, data: "s3".data(using: .utf8)!)
        let kSigning = HmacUtil.sha256(key: kService, data: "aws4_request".data(using: .utf8)!)
        let signature = HmacUtil.sha256Hex(key: kSigning, data: stringToSign.data(using: .utf8)!)

        let auth = "AWS4-HMAC-SHA256 Credential=\(config.accessKeyId)/\(credScope), SignedHeaders=\(signedHeaders), Signature=\(signature)"
        request.setValue(auth, forHTTPHeaderField: "Authorization")

        var outcome: UploadOutcome?
        let sem = DispatchSemaphore(value: 0)
        let task = session.dataTask(with: request) { data, response, error in
            let httpResponse = response as? HTTPURLResponse
            if let error = error {
                outcome = .failure(self.makeFailure(
                    message: error.localizedDescription,
                    filePath: filePath,
                    fileSize: fileData.count,
                    uploadName: uploadName,
                    request: request,
                    requestTarget: requestTarget,
                    resultUrlString: resultUrlString,
                    config: config,
                    response: httpResponse,
                    responseBody: data,
                    error: error
                ))
                sem.signal()
                return
            }
            let code = httpResponse?.statusCode ?? 0
            if code >= 200 && code < 300 {
                outcome = .success(url: resultUrlString)
            } else {
                outcome = .failure(self.makeFailure(
                    message: "S3 returned HTTP \(code)",
                    filePath: filePath,
                    fileSize: fileData.count,
                    uploadName: uploadName,
                    request: request,
                    requestTarget: requestTarget,
                    resultUrlString: resultUrlString,
                    config: config,
                    response: httpResponse,
                    responseBody: data,
                    error: nil
                ))
            }
            sem.signal()
        }
        task.resume()
        sem.wait()
        return outcome ?? .failure(makeFailure(
            message: "S3 upload failed",
            filePath: filePath,
            fileSize: fileData.count,
            uploadName: uploadName,
            request: request,
            requestTarget: requestTarget,
            resultUrlString: resultUrlString,
            config: config,
            response: nil,
            responseBody: nil,
            error: nil
        ))
    }

    private func buildRequestTarget(bucket: String, key: String, config: S3Config, shouldUsePathStyle: Bool) -> S3RequestTarget? {
        let keyPath = normalizedPathComponent(key)

        if !config.customEndpoint.isEmpty {
            let rawEndpoint = config.customEndpoint.trimmingCharacters(in: .whitespacesAndNewlines)
            let normalizedEndpoint = rawEndpoint.contains("://") ? rawEndpoint : "https://\(rawEndpoint)"
            guard let endpointURL = URL(string: normalizedEndpoint), let endpointHost = endpointURL.host else {
                return nil
            }

            let scheme = endpointURL.scheme ?? "https"
            let port = endpointURL.port.map { ":\($0)" } ?? ""
            let baseHost = "\(endpointHost)\(port)"
            let basePath = endpointURL.path

            if shouldUsePathStyle {
                let canonicalURI = joinedPath([basePath, bucket, keyPath])
                return S3RequestTarget(
                    requestUrlString: "\(scheme)://\(baseHost)\(canonicalURI)",
                    hostHeader: baseHost,
                    canonicalURI: canonicalURI,
                    endpointMode: config.usePathStyle ? "custom-path-style" : "custom-path-style-auto-dot-bucket"
                )
            }

            let virtualHost = "\(bucket).\(endpointHost)\(port)"
            let canonicalURI = joinedPath([basePath, keyPath])
            return S3RequestTarget(
                requestUrlString: "\(scheme)://\(virtualHost)\(canonicalURI)",
                hostHeader: virtualHost,
                canonicalURI: canonicalURI,
                endpointMode: "custom-virtual-host"
            )
        }

        if shouldUsePathStyle {
            let host = "s3.\(config.region).amazonaws.com"
            let canonicalURI = joinedPath([bucket, keyPath])
            return S3RequestTarget(
                requestUrlString: "https://\(host)\(canonicalURI)",
                hostHeader: host,
                canonicalURI: canonicalURI,
                endpointMode: config.usePathStyle ? "aws-path-style" : "aws-path-style-auto-dot-bucket"
            )
        }

        let host = "\(bucket).s3.\(config.region).amazonaws.com"
        let canonicalURI = joinedPath([keyPath])
        return S3RequestTarget(
            requestUrlString: "https://\(host)\(canonicalURI)",
            hostHeader: host,
            canonicalURI: canonicalURI,
            endpointMode: "aws-virtual-host"
        )
    }

    private func makeFailure(
        message: String,
        filePath: String,
        fileSize: Int,
        uploadName: String,
        request: URLRequest,
        requestTarget: S3RequestTarget,
        resultUrlString: String,
        config: S3Config,
        response: HTTPURLResponse?,
        responseBody: Data?,
        error: Error?
    ) -> UploadFailure {
        let requestHeaders = UploadDebugTools.formatHeaders(
            UploadDebugTools.sanitizedHeaders(request.allHTTPHeaderFields ?? [:])
        )
        let responseHeaders = UploadDebugTools.formatHeaders(
            UploadDebugTools.sanitizedHeaders(UploadDebugTools.httpHeaders(from: response))
        )
        let responseBodySnippet = UploadDebugTools.responseBodySnippet(responseBody)
        let requestSection = UploadDebugTools.formatKeyValueLines([
            ("Uploader", "Amazon S3"),
            ("Timestamp", ISO8601DateFormatter().string(from: Date())),
            ("File", filePath),
            ("File Size", "\(fileSize) bytes"),
            ("Upload Name", uploadName),
            ("Bucket", config.bucketName),
            ("Region", config.region),
            ("Request URL", request.url?.absoluteString ?? requestTarget.requestUrlString),
            ("Canonical URI", requestTarget.canonicalURI),
            ("Result URL", resultUrlString),
            ("Endpoint Mode", requestTarget.endpointMode),
            ("Custom Endpoint", config.customEndpoint.isEmpty ? nil : config.customEndpoint),
            ("Configured Path Style", config.usePathStyle ? "true" : "false"),
            ("Effective Path Style", requestTarget.endpointMode.contains("path-style") ? "true" : "false"),
            ("Bucket Contains Dots", config.bucketName.contains(".") ? "true" : "false"),
            ("Auto Path Style Reason", (!config.usePathStyle && config.bucketName.contains(".")) ? "Bucket name contains dots; iOS TLS wildcard certificate matching requires path-style URL." : nil),
            ("Use Custom Domain", config.useCustomDomain ? "true" : "false"),
            ("Custom Domain", config.useCustomDomain ? config.customDomain : nil),
            ("Signed Payload", config.signedPayload ? "true" : "false"),
            ("Public ACL", config.setPublicAcl ? "true" : "false"),
            ("HTTP Status", response.map { "\($0.statusCode)" }),
            ("Potential TLS Hint", (!config.usePathStyle && config.bucketName.contains(".")) ? "Dotted bucket names require path-style URLs on iOS to avoid certificate mismatches." : nil)
        ])

        let details = UploadDebugTools.formatSections([
            ("Request", requestSection),
            ("Request Headers", requestHeaders),
            ("Response Headers", responseHeaders),
            ("Response Body", responseBodySnippet),
            ("NSError", error.map { UploadDebugTools.describe(error: $0) })
        ])

        return UploadFailure(message: message, details: details)
    }

    private func joinedPath(_ components: [String]) -> String {
        let normalized = components
            .map(normalizedPathComponent)
            .filter { !$0.isEmpty }
        return "/" + normalized.joined(separator: "/")
    }

    private func normalizedPathComponent(_ component: String) -> String {
        component.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
    }
}

private struct S3RequestTarget {
    let requestUrlString: String
    let hostHeader: String
    let canonicalURI: String
    let endpointMode: String
}

extension Data {
    var sha256Hex: String {
        let hash = SHA256.hash(data: self)
        return hash.map { String(format: "%02x", $0) }.joined()
    }
}
extension String {
    var sha256Hex: String { Data(utf8).sha256Hex }
}
enum HmacUtil {
    static func sha256(key: Data, data: Data) -> Data {
        let symKey = SymmetricKey(data: key)
        let signature = CryptoKit.HMAC<SHA256>.authenticationCode(for: data, using: symKey)
        return Data(signature)
    }
    static func sha256Hex(key: Data, data: Data) -> String {
        sha256(key: key, data: data).map { String(format: "%02x", $0) }.joined()
    }
}
