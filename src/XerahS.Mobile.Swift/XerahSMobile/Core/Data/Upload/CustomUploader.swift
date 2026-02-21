//
//  CustomUploader.swift
//  XerahS Mobile (Swift)
//
//  Copyright (c) 2007-2026 ShareX Team.
//

import Foundation

/// Upload a file via custom .sxcu-style config: POST to RequestURL with multipart.
final class CustomUploader {
    private let session: URLSession = {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 60
        config.timeoutIntervalForResource = 120
        return URLSession(configuration: config)
    }()

    func uploadFile(filePath: String, entry: CustomUploaderEntry) -> UploadOutcome {
        if entry.requestUrl.isEmpty { return .failure(error: "Request URL is empty") }
        guard FileManager.default.fileExists(atPath: filePath) else { return .failure(error: "File not found") }
        guard let url = URL(string: entry.requestUrl) else { return .failure(error: "Invalid URL") }
        let fileUrl = URL(fileURLWithPath: filePath)
        let formName = entry.fileFormName.isEmpty ? "file" : entry.fileFormName

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        let boundary = "Boundary-\(UUID().uuidString)"
        request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
        for (k, v) in entry.headers where !v.isEmpty { request.setValue(v, forHTTPHeaderField: k) }

        var body = Data()
        body.append("--\(boundary)\r\n".data(using: .utf8)!)
        body.append("Content-Disposition: form-data; name=\"\(formName)\"; filename=\"\(fileUrl.lastPathComponent)\"\r\n\r\n".data(using: .utf8)!)
        if let fileData = try? Data(contentsOf: fileUrl) { body.append(fileData) }
        body.append("\r\n".data(using: .utf8)!)
        if !entry.body.isEmpty {
            body.append("--\(boundary)\r\n".data(using: .utf8)!)
            body.append("Content-Disposition: form-data; name=\"body\"\r\n\r\n".data(using: .utf8)!)
            body.append(entry.body.data(using: .utf8)!)
            body.append("\r\n".data(using: .utf8)!)
        }
        body.append("--\(boundary)--\r\n".data(using: .utf8)!)
        request.httpBody = body

        var outcome: UploadOutcome?
        let sem = DispatchSemaphore(value: 0)
        let task = session.dataTask(with: request) { data, response, error in
            if let error = error {
                outcome = .failure(error: error.localizedDescription)
                sem.signal()
                return
            }
            let code = (response as? HTTPURLResponse)?.statusCode ?? 0
            let bodyStr = data.flatMap { String(data: $0, encoding: .utf8) } ?? ""
            if code < 200 || code >= 300 {
                outcome = .failure(error: "HTTP \(code): \(bodyStr.prefix(200))")
                sem.signal()
                return
            }
            let extracted = self.extractUrl(from: bodyStr, expression: entry.urlExpression)
            let urlResult = extracted ?? bodyStr.trimmingCharacters(in: .whitespacesAndNewlines).prefix(500).description
            outcome = urlResult.isEmpty ? .failure(error: "No URL in response") : .success(url: urlResult)
            sem.signal()
        }
        task.resume()
        sem.wait()
        return outcome ?? .failure(error: "Upload failed")
    }

    private func extractUrl(from responseBody: String, expression: String) -> String? {
        guard !expression.isEmpty else { return nil }
        guard let regex = try? NSRegularExpression(pattern: expression) else { return nil }
        let range = NSRange(responseBody.startIndex..., in: responseBody)
        guard let match = regex.firstMatch(in: responseBody, options: [], range: range) else { return nil }
        if match.numberOfRanges > 1, let r = Range(match.range(at: 1), in: responseBody) {
            return String(responseBody[r])
        }
        if let r = Range(match.range, in: responseBody) { return String(responseBody[r]) }
        return nil
    }
}
