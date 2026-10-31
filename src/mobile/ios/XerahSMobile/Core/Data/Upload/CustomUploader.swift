//
//  CustomUploader.swift
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

import Foundation

/// Upload a file using an .sxcu-compatible definition.
final class CustomUploader {
    private let session: URLSession = {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 60
        config.timeoutIntervalForResource = 120
        return URLSession(configuration: config)
    }()

    private let supportedFunctions: Set<String> = [
        "base64",
        "filename",
        "header",
        "input",
        "json",
        "regex",
        "response",
        "responseurl"
    ]

    func uploadFile(filePath: String, entry: CustomUploaderEntry) -> UploadOutcome {
        if entry.requestUrl.isEmpty { return .failure(UploadFailure(message: "Request URL is empty")) }
        guard FileManager.default.fileExists(atPath: filePath) else { return .failure(UploadFailure(message: "File not found")) }

        let unsupported = findUnsupportedFunctions(in: entry)
        if !unsupported.isEmpty {
            return .failure(UploadFailure(
                message: "Unsupported .sxcu function(s) on iOS: \(unsupported.sorted().joined(separator: ", "))",
                details: "Supported functions: \(supportedFunctions.sorted().joined(separator: ", "))"
            ))
        }

        let fileUrl = URL(fileURLWithPath: filePath)
        let uploadFileName = UploadFileNameGenerator.uploadFileName(for: filePath)
        guard let fileData = try? Data(contentsOf: fileUrl) else {
            return .failure(UploadFailure(message: "Cannot read file"))
        }

        let definition = entry.toSxcuDefinition()
        let bodyInput = requiresEncodedInput(definition.body) ? fileData.base64EncodedString() : ""
        let requestContext = CustomUploaderTemplateContext(
            input: bodyInput,
            fileName: uploadFileName,
            responseText: nil,
            responseUrl: nil,
            responseHeaders: [:]
        )

        guard let requestUrl = buildRequestURL(definition: definition, context: requestContext),
              let url = URL(string: requestUrl) else {
            return .failure(UploadFailure(message: "Invalid RequestURL"))
        }

        var request = URLRequest(url: url)
        request.httpMethod = definition.requestMethod.rawValue

        for (key, value) in renderDictionary(definition.headers, context: requestContext) where !value.isEmpty {
            request.setValue(value, forHTTPHeaderField: key)
        }

        let resolvedArguments = renderDictionary(entry.mergedArgumentsForExecution(), context: requestContext)

        switch definition.body {
        case .none:
            break
        case .multipartFormData:
            let boundary = "Boundary-\(UUID().uuidString)"
            request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
            request.httpBody = makeMultipartBody(
                boundary: boundary,
                fileData: fileData,
                uploadFileName: uploadFileName,
                fileFormName: definition.fileFormName.isEmpty ? "file" : definition.fileFormName,
                arguments: resolvedArguments
            )
        case .binary:
            if request.value(forHTTPHeaderField: "Content-Type") == nil {
                request.setValue(UploadContentType.forFile(atPath: uploadFileName), forHTTPHeaderField: "Content-Type")
            }
            request.httpBody = fileData
        case .formUrlEncoded:
            if request.value(forHTTPHeaderField: "Content-Type") == nil {
                request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
            }
            request.httpBody = formUrlEncodedData(from: resolvedArguments)
        case .json:
            if request.value(forHTTPHeaderField: "Content-Type") == nil {
                request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            }
            request.httpBody = renderBody(definition.data, context: requestContext, mode: .json).data(using: .utf8)
        case .xml:
            if request.value(forHTTPHeaderField: "Content-Type") == nil {
                request.setValue("application/xml", forHTTPHeaderField: "Content-Type")
            }
            request.httpBody = renderBody(definition.data, context: requestContext, mode: .xml).data(using: .utf8)
        }

        var outcome: UploadOutcome?
        let sem = DispatchSemaphore(value: 0)
        let task = session.dataTask(with: request) { data, response, error in
            let httpResponse = response as? HTTPURLResponse
            let responseBody = data.flatMap { String(data: $0, encoding: .utf8) } ?? ""
            let responseContext = CustomUploaderTemplateContext(
                input: bodyInput,
                fileName: uploadFileName,
                responseText: responseBody,
                responseUrl: response?.url?.absoluteString,
                responseHeaders: Self.lowercasedHeaders(from: httpResponse)
            )

            if let error = error {
                outcome = .failure(self.makeFailure(
                    message: error.localizedDescription,
                    filePath: filePath,
                    fileSize: request.httpBody?.count ?? fileData.count,
                    uploadFileName: uploadFileName,
                    entry: entry,
                    request: request,
                    response: httpResponse,
                    responseBody: data,
                    error: error
                ))
                sem.signal()
                return
            }

            let code = httpResponse?.statusCode ?? 0
            if code < 200 || code >= 300 {
                let parsedError = self.renderTemplate(definition.errorMessage, context: responseContext)
                    .trimmingCharacters(in: .whitespacesAndNewlines)
                let message = parsedError.isEmpty ? "HTTP \(code): \(responseBody.prefix(200))" : parsedError
                outcome = .failure(self.makeFailure(
                    message: message,
                    filePath: filePath,
                    fileSize: request.httpBody?.count ?? fileData.count,
                    uploadFileName: uploadFileName,
                    entry: entry,
                    request: request,
                    response: httpResponse,
                    responseBody: data,
                    error: nil
                ))
                sem.signal()
                return
            }

            let resolvedUrl = self.resolveSuccessURL(entry: entry, definition: definition, responseContext: responseContext)
            if resolvedUrl.isEmpty {
                outcome = .failure(self.makeFailure(
                    message: "No URL in response",
                    filePath: filePath,
                    fileSize: request.httpBody?.count ?? fileData.count,
                    uploadFileName: uploadFileName,
                    entry: entry,
                    request: request,
                    response: httpResponse,
                    responseBody: data,
                    error: nil
                ))
            } else {
                outcome = .success(url: resolvedUrl)
            }
            sem.signal()
        }
        task.resume()
        sem.wait()
        return outcome ?? .failure(UploadFailure(message: "Upload failed"))
    }

    private func resolveSuccessURL(
        entry: CustomUploaderEntry,
        definition: SxcuDefinition,
        responseContext: CustomUploaderTemplateContext
    ) -> String {
        let rendered = renderTemplate(definition.url, context: responseContext)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        if !rendered.isEmpty {
            return rendered
        }

        let legacy = extractRegexMatch(
            input: responseContext.responseText ?? "",
            expression: entry.legacyUrlExpression,
            group: nil
        )
        if let legacy, !legacy.isEmpty {
            return legacy
        }

        return (responseContext.responseText ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func buildRequestURL(definition: SxcuDefinition, context: CustomUploaderTemplateContext) -> String? {
        let renderedUrl = renderTemplate(definition.requestURL, context: context, urlEncodeInput: true)
        guard var components = URLComponents(string: renderedUrl) else {
            return nil
        }

        let queryItems = renderDictionary(definition.parameters, context: context)
            .map { URLQueryItem(name: $0.key, value: $0.value) }
        if !queryItems.isEmpty {
            components.queryItems = (components.queryItems ?? []) + queryItems
        }
        return components.url?.absoluteString ?? components.string
    }

    private func renderDictionary(
        _ source: [String: String],
        context: CustomUploaderTemplateContext
    ) -> [String: String] {
        var output: [String: String] = [:]
        for key in source.keys.sorted() {
            output[key] = renderTemplate(source[key] ?? "", context: context)
        }
        return output
    }

    private func renderBody(
        _ template: String,
        context: CustomUploaderTemplateContext,
        mode: CustomUploaderBodyType
    ) -> String {
        let adjusted = CustomUploaderTemplateContext(
            input: encodeBodyInput(context.input, mode: mode),
            fileName: encodeBodyInput(context.fileName, mode: mode),
            responseText: context.responseText,
            responseUrl: context.responseUrl,
            responseHeaders: context.responseHeaders
        )
        return renderTemplate(template, context: adjusted)
    }

    private func encodeBodyInput(_ value: String, mode: CustomUploaderBodyType) -> String {
        switch mode {
        case .json:
            return jsonEscaped(value)
        case .xml:
            return xmlEscaped(value)
        default:
            return value
        }
    }

    private func requiresEncodedInput(_ bodyType: CustomUploaderBodyType) -> Bool {
        switch bodyType {
        case .formUrlEncoded, .json, .xml:
            return true
        case .none, .multipartFormData, .binary:
            return false
        }
    }

    private func makeMultipartBody(
        boundary: String,
        fileData: Data,
        uploadFileName: String,
        fileFormName: String,
        arguments: [String: String]
    ) -> Data {
        var body = Data()

        for key in arguments.keys.sorted() {
            let value = arguments[key] ?? ""
            body.append("--\(boundary)\r\n".utf8Data)
            body.append("Content-Disposition: form-data; name=\"\(key)\"\r\n\r\n".utf8Data)
            body.append(value.utf8Data)
            body.append("\r\n".utf8Data)
        }

        body.append("--\(boundary)\r\n".utf8Data)
        body.append("Content-Disposition: form-data; name=\"\(fileFormName)\"; filename=\"\(uploadFileName)\"\r\n".utf8Data)
        body.append("Content-Type: \(UploadContentType.forFile(atPath: uploadFileName))\r\n\r\n".utf8Data)
        body.append(fileData)
        body.append("\r\n--\(boundary)--\r\n".utf8Data)
        return body
    }

    private func formUrlEncodedData(from arguments: [String: String]) -> Data? {
        let encoded = arguments.keys.sorted().map { key in
            let value = arguments[key] ?? ""
            return "\(strictUrlEncode(key))=\(strictUrlEncode(value))"
        }.joined(separator: "&")
        return encoded.data(using: .utf8)
    }

    private func renderTemplate(
        _ template: String,
        context: CustomUploaderTemplateContext,
        urlEncodeInput: Bool = false
    ) -> String {
        guard !template.isEmpty else { return "" }
        guard let regex = try? NSRegularExpression(pattern: #"\{([A-Za-z][^{}]*)\}"#) else { return template }

        var result = template
        for _ in 0..<12 {
            let range = NSRange(result.startIndex..., in: result)
            let matches = regex.matches(in: result, options: [], range: range)
            if matches.isEmpty { break }

            var updated = result
            for match in matches.reversed() {
                guard
                    let tokenRange = Range(match.range(at: 0), in: updated),
                    let contentRange = Range(match.range(at: 1), in: updated)
                else { continue }

                let token = String(updated[contentRange])
                let replacement = evaluateToken(token, context: context, urlEncodeInput: urlEncodeInput)
                updated.replaceSubrange(tokenRange, with: replacement)
            }

            if updated == result { break }
            result = updated
        }

        return result
    }

    private func evaluateToken(
        _ token: String,
        context: CustomUploaderTemplateContext,
        urlEncodeInput: Bool
    ) -> String {
        let parts = token.split(separator: ":", maxSplits: 1, omittingEmptySubsequences: false).map(String.init)
        let function = parts[0].lowercased()
        let arguments = parts.count > 1 ? parts[1].split(separator: "|", omittingEmptySubsequences: false).map(String.init) : []

        switch function {
        case "input":
            return urlEncodeInput ? strictUrlEncode(context.input) : context.input
        case "filename":
            return urlEncodeInput ? strictUrlEncode(context.fileName) : context.fileName
        case "response":
            return context.responseText ?? ""
        case "responseurl":
            return context.responseUrl ?? ""
        case "header":
            guard let name = arguments.first?.lowercased() else { return "" }
            return context.responseHeaders[name] ?? ""
        case "json":
            let input: String
            let path: String
            if arguments.count > 1 {
                input = arguments[0]
                path = arguments[1]
            } else {
                input = context.responseText ?? ""
                path = arguments.first ?? ""
            }
            return jsonValue(from: input, path: path)
        case "regex":
            if arguments.count > 2 {
                return extractRegexMatch(input: arguments[0], expression: arguments[1], group: arguments[2]) ?? ""
            }
            if arguments.count > 1 {
                return extractRegexMatch(input: context.responseText ?? "", expression: arguments[0], group: arguments[1]) ?? ""
            }
            return extractRegexMatch(input: context.responseText ?? "", expression: arguments.first ?? "", group: nil) ?? ""
        case "base64":
            guard let value = arguments.first else { return "" }
            return Data(value.utf8).base64EncodedString()
        default:
            return "{\(token)}"
        }
    }

    private func extractRegexMatch(input: String, expression: String, group: String?) -> String? {
        guard !input.isEmpty, !expression.isEmpty else { return nil }
        guard let regex = try? NSRegularExpression(pattern: expression) else { return nil }
        let range = NSRange(input.startIndex..., in: input)
        guard let match = regex.firstMatch(in: input, options: [], range: range) else { return nil }

        if let group, !group.isEmpty {
            if let number = Int(group), number < match.numberOfRanges,
               let capture = Range(match.range(at: number), in: input) {
                return String(input[capture])
            }
            return nil
        }

        if match.numberOfRanges > 1, let capture = Range(match.range(at: 1), in: input) {
            return String(input[capture])
        }
        if let capture = Range(match.range(at: 0), in: input) {
            return String(input[capture])
        }
        return nil
    }

    private func jsonValue(from input: String, path: String) -> String {
        guard !input.isEmpty, !path.isEmpty else { return "" }
        guard
            let data = input.data(using: .utf8),
            let root = try? JSONSerialization.jsonObject(with: data)
        else {
            return ""
        }

        let normalized = path.hasPrefix("$.") ? String(path.dropFirst(2)) : path
        guard !normalized.isEmpty else { return "" }

        var current: Any = root
        for component in normalized.split(separator: ".", omittingEmptySubsequences: true).map(String.init) {
            guard let next = descendJson(value: current, component: component) else { return "" }
            current = next
        }
        return stringifyJsonValue(current)
    }

    private func descendJson(value: Any, component: String) -> Any? {
        let pattern = #"[^\[\]]+|\[\d+\]"#
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return nil }
        let range = NSRange(component.startIndex..., in: component)
        let parts = regex.matches(in: component, options: [], range: range)
        if parts.isEmpty { return nil }

        var current: Any? = value
        for part in parts {
            guard let tokenRange = Range(part.range(at: 0), in: component) else { return nil }
            let token = String(component[tokenRange])

            if token.hasPrefix("[") && token.hasSuffix("]") {
                guard
                    let index = Int(token.dropFirst().dropLast()),
                    let array = current as? [Any],
                    index >= 0,
                    index < array.count
                else {
                    return nil
                }
                current = array[index]
            } else {
                guard let dict = current as? [String: Any] else { return nil }
                current = dict[token]
            }
        }

        return current
    }

    private func stringifyJsonValue(_ value: Any) -> String {
        switch value {
        case let string as String:
            return string
        case let number as NSNumber:
            return number.stringValue
        case let dict as [String: Any]:
            guard let data = try? JSONSerialization.data(withJSONObject: dict),
                  let string = String(data: data, encoding: .utf8) else { return "" }
            return string
        case let array as [Any]:
            guard let data = try? JSONSerialization.data(withJSONObject: array),
                  let string = String(data: data, encoding: .utf8) else { return "" }
            return string
        default:
            return "\(value)"
        }
    }

    private func findUnsupportedFunctions(in entry: CustomUploaderEntry) -> Set<String> {
        let templates = [
            entry.requestUrl,
            entry.data,
            entry.url,
            entry.thumbnailUrl,
            entry.deletionUrl,
            entry.errorMessage,
            entry.legacyUrlExpression
        ] + entry.parameters.values + entry.headers.values + entry.arguments.values

        guard let regex = try? NSRegularExpression(pattern: #"\{([A-Za-z][^:{}|}]*)"#) else { return [] }

        var unsupported: Set<String> = []
        for template in templates where !template.isEmpty {
            let range = NSRange(template.startIndex..., in: template)
            for match in regex.matches(in: template, options: [], range: range) {
                guard let functionRange = Range(match.range(at: 1), in: template) else { continue }
                let function = template[functionRange].lowercased()
                if !supportedFunctions.contains(function) {
                    unsupported.insert(function)
                }
            }
        }
        return unsupported
    }

    private func strictUrlEncode(_ value: String) -> String {
        let allowed = CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~")
        return value.addingPercentEncoding(withAllowedCharacters: allowed) ?? value
    }

    private func jsonEscaped(_ value: String) -> String {
        value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")
            .replacingOccurrences(of: "\n", with: "\\n")
            .replacingOccurrences(of: "\r", with: "\\r")
            .replacingOccurrences(of: "\t", with: "\\t")
    }

    private func xmlEscaped(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
            .replacingOccurrences(of: "'", with: "&apos;")
    }

    private func makeFailure(
        message: String,
        filePath: String,
        fileSize: Int,
        uploadFileName: String,
        entry: CustomUploaderEntry,
        request: URLRequest,
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
            ("Uploader", "Custom Uploader (.sxcu)"),
            ("Timestamp", ISO8601DateFormatter().string(from: Date())),
            ("File", filePath),
            ("Request Body Size", "\(fileSize) bytes"),
            ("Upload Name", uploadFileName),
            ("Request Method", request.httpMethod),
            ("Request URL", request.url?.absoluteString),
            ("Destination Type", entry.destinationType),
            ("Body Type", entry.bodyType.rawValue),
            ("File Form Name", entry.fileFormName),
            ("Configured Parameters", "\(entry.parameters.count)"),
            ("Configured Headers", "\(entry.headers.count)"),
            ("Configured Arguments", "\(entry.arguments.count)")
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

    private static func lowercasedHeaders(from response: HTTPURLResponse?) -> [String: String] {
        guard let response else { return [:] }
        var headers: [String: String] = [:]
        for (key, value) in response.allHeaderFields {
            headers["\(key)".lowercased()] = "\(value)"
        }
        return headers
    }
}

private struct CustomUploaderTemplateContext {
    let input: String
    let fileName: String
    let responseText: String?
    let responseUrl: String?
    let responseHeaders: [String: String]
}

private extension String {
    var utf8Data: Data { Data(utf8) }
}
