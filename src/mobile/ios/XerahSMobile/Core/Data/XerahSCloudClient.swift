//
//  XerahSCloudClient.swift
//  XerahS Mobile (Swift)
//
//  XerahS - The Avalonia UI implementation of ShareX
//  Copyright (c) 2007-2026 ShareX Team.
//
//  This program is free software; you can redistribute it and/or
//  modify it under the terms of the GNU General Public License
//  as published by the Free Software Foundation; either version 2
//  of the License, or (at your option) any later version.
//

import CryptoKit
import Foundation
import Security

enum XerahSCloudConfiguration {
    static let apiBaseURL = URL(string: "https://cloud.xerahs.com/")!
    // The Cloud site is its own OAuth server (Better Auth under /api/auth), like the desktop app.
    static let oauthAuthority = URL(string: "https://cloud.xerahs.com/")!
    static let authorizePath = "api/auth/oauth2/authorize"
    static let tokenPath = "api/auth/oauth2/token"
    static let jwksPath = "api/auth/jwks"
    static let scopes = "openid email profile offline_access"
    /// Token issuer: the authority's origin, without a path.
    static let issuer = "https://cloud.xerahs.com"
    /// Audience (RFC 8707 resource) of access tokens: the owner API.
    static let apiAudience = "https://cloud.xerahs.com/api/v1"
    static let clientID = "8d8adf92-86c4-4036-a4c9-09901230f2c4"
    static let oauthRedirectURL = URL(string: "https://cloud.xerahs.com/auth/desktop/callback")!
    static let callbackScheme = "xerahs"
    static let settingsURL = URL(string: "https://cloud.xerahs.com/settings")!
}

enum XerahSCloudSettings {
    private static let autoPublishKey = "XerahSCloud.AutoPublish"
    private static let appGroupID = AppIdentifiers.appGroupId

    static var automaticallyPublishesEligibleUploads: Bool {
        get { UserDefaults(suiteName: appGroupID)?.bool(forKey: autoPublishKey) ?? false }
        set { UserDefaults(suiteName: appGroupID)?.set(newValue, forKey: autoPublishKey) }
    }
}

struct XerahSCloudSession {
    let accessToken: String
    let refreshToken: String
    let ownerSubject: String
    let expiresAt: Date
}

struct XerahSCloudOAuthAttempt {
    let authorizationURL: URL
    let state: String
    let nonce: String
    let codeVerifier: String
    let expiresAt: Date
}

struct XerahSCloudAccount: Decodable {
    let slug: String
    let timeZone: String?
    let strongAuth: Bool
    let trialStatus: String?
    let trialEndsAt: String?
    let subscriptionStatus: String?
    let paidThrough: String?
    let canPublish: Bool
    let disputeSuspended: Bool

    var profileURL: URL { XerahSCloudConfiguration.apiBaseURL.appendingPathComponent(slug) }
}

struct XerahSCloudGalleryPage: Decodable {
    let items: [XerahSCloudGalleryItem]
    let nextCursor: String?
}

struct XerahSCloudGalleryItem: Decodable, Identifiable, Equatable {
    let id: String
    let clientItemId: String
    let url: String
    let thumbnailUrl: String?
    let kind: String
    let fileName: String
    let title: String
    let capturedAt: String
    let publishedAt: String
    let host: String?
    let contentType: String?
}

struct XerahSCloudPublishRequest: Encodable {
    let url: String
    let thumbnailUrl: String?
    let kind: String
    let fileName: String
    let capturedAt: String
    let host: String?
    let contentType: String?
}

private struct XerahSCloudPublishResponse: Decodable {
    struct Item: Decodable { let id: String }
    let item: Item
}

struct XerahSCloudAPIErrorEnvelope: Decodable {
    struct Detail: Decodable {
        let code: String?
        let message: String?
        let correlationId: String?
    }

    let error: Detail?
}

struct XerahSCloudError: LocalizedError {
    let operation: String
    let statusCode: Int?
    let code: String?
    let detail: String
    let correlationID: String?

    var errorDescription: String? {
        var value = "\(operation) failed"
        if let statusCode { value += " (HTTP \(statusCode))" }
        if let code, !code.isEmpty { value += " [\(code)]" }
        if !detail.isEmpty { value += ": \(detail)" }
        if let correlationID, !correlationID.isEmpty { value += " Correlation ID: \(correlationID)" }
        return value
    }
}

enum XerahSCloudCredentialStore {
    private static let service = "com.xerahs.xerahs.mobile.cloud"
    private static let accessGroup = AppIdentifiers.keychainAccessGroup
    private static let refreshAccount = "oauth.refresh-token"
    private static let subjectAccount = "oauth.owner-subject"

    static func read() throws -> (subject: String, refreshToken: String)? {
        let subject = try readItem(account: subjectAccount)
        let refresh = try readItem(account: refreshAccount)
        switch (subject, refresh) {
        case (.missing, .missing):
            return nil
        case (.value(let subjectValue), .value(let refreshValue)) where !subjectValue.isEmpty && !refreshValue.isEmpty:
            return (subjectValue, refreshValue)
        default:
            clear()
            throw XerahSCloudError(operation: "Keychain", statusCode: nil, code: "security_error", detail: "The stored Cloud credential was incomplete or corrupt and has been cleared.", correlationID: nil)
        }
    }

    static func write(subject: String, refreshToken: String) throws {
        guard !subject.isEmpty, !refreshToken.isEmpty else {
            clear()
            throw XerahSCloudError(operation: "Keychain", statusCode: nil, code: "security_error", detail: "An empty Cloud credential was rejected.", correlationID: nil)
        }
        guard try writeItem(refreshToken, account: refreshAccount) else {
            clear()
            throw XerahSCloudError(operation: "Keychain", statusCode: nil, code: "security_error", detail: "The refresh credential could not be stored securely.", correlationID: nil)
        }
        guard try writeItem(subject, account: subjectAccount) else {
            delete(account: refreshAccount)
            delete(account: subjectAccount)
            throw XerahSCloudError(operation: "Keychain", statusCode: nil, code: "security_error", detail: "The account binding could not be stored securely.", correlationID: nil)
        }
    }

    static func clear() {
        delete(account: refreshAccount)
        delete(account: subjectAccount)
    }

    private static func query(account: String) -> [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecAttrAccessGroup as String: accessGroup
        ]
    }

    private enum ReadResult { case missing, value(String) }

    private static func readItem(account: String) throws -> ReadResult {
        var request = query(account: account)
        request[kSecMatchLimit as String] = kSecMatchLimitOne
        request[kSecReturnData as String] = true
        var result: CFTypeRef?
        let status = SecItemCopyMatching(request as CFDictionary, &result)
        if status == errSecItemNotFound { return .missing }
        guard status == errSecSuccess, let data = result as? Data,
              let value = String(data: data, encoding: .utf8), !value.isEmpty else {
            clear()
            throw XerahSCloudError(operation: "Keychain", statusCode: nil, code: "security_error", detail: "The secure Cloud credential could not be read and has been cleared.", correlationID: nil)
        }
        return .value(value)
    }

    private static func writeItem(_ value: String, account: String) throws -> Bool {
        guard let data = value.data(using: .utf8) else { return false }
        delete(account: account)
        var request = query(account: account)
        request[kSecValueData as String] = data
        request[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        let status = SecItemAdd(request as CFDictionary, nil)
        guard status == errSecSuccess else {
            clear()
            throw XerahSCloudError(operation: "Keychain", statusCode: nil, code: "security_error", detail: "Keychain rejected the Cloud credential and all Cloud credentials were cleared.", correlationID: nil)
        }
        return true
    }

    private static func delete(account: String) {
        SecItemDelete(query(account: account) as CFDictionary)
    }
}

final class XerahSCloudClient {
    private let urlSession: URLSession
    private let lock = NSLock()
    private var currentSession: XerahSCloudSession?
    private var activeRefresh: (id: UUID, task: Task<XerahSCloudSession, Error>)?
    private var sessionGeneration: UInt64 = 0
    private var pendingOAuthStates: [String: Date] = [:]
    private var consumedOAuthStates: Set<String> = []

    init(urlSession: URLSession = .shared) {
        self.urlSession = urlSession
    }

    var hasStoredCredential: Bool {
        do { return try XerahSCloudCredentialStore.read() != nil }
        catch { return false }
    }
    var currentOwnerSubject: String? {
        lock.lock()
        defer { lock.unlock() }
        if let owner = currentSession?.ownerSubject { return owner }
        return try? XerahSCloudCredentialStore.read()?.subject
    }

    func beginOAuth() -> XerahSCloudOAuthAttempt {
        let state = randomBase64URL(byteCount: 32)
        let nonce = randomBase64URL(byteCount: 32)
        let verifier = randomBase64URL(byteCount: 64)
        let challenge = Data(SHA256.hash(data: Data(verifier.utf8))).base64URLEncodedString()
        var components = URLComponents(url: XerahSCloudConfiguration.oauthAuthority.appendingPathComponent(XerahSCloudConfiguration.authorizePath), resolvingAgainstBaseURL: false)!
        components.queryItems = [
            URLQueryItem(name: "client_id", value: XerahSCloudConfiguration.clientID),
            URLQueryItem(name: "redirect_uri", value: XerahSCloudConfiguration.oauthRedirectURL.absoluteString),
            URLQueryItem(name: "response_type", value: "code"),
            URLQueryItem(name: "scope", value: XerahSCloudConfiguration.scopes),
            URLQueryItem(name: "state", value: state),
            URLQueryItem(name: "nonce", value: nonce),
            URLQueryItem(name: "code_challenge", value: challenge),
            URLQueryItem(name: "code_challenge_method", value: "S256"),
            // JWT access tokens for the owner API, and always show the consent page:
            // approving creates the revocable app grant.
            URLQueryItem(name: "resource", value: XerahSCloudConfiguration.apiAudience),
            URLQueryItem(name: "prompt", value: "consent")
        ]
        let expiresAt = Date().addingTimeInterval(600)
        lock.withLock {
            pendingOAuthStates = pendingOAuthStates.filter { $0.value > Date() }
            pendingOAuthStates[state] = expiresAt
        }
        return XerahSCloudOAuthAttempt(authorizationURL: components.url!, state: state, nonce: nonce, codeVerifier: verifier, expiresAt: expiresAt)
    }

    func completeOAuth(callbackURL: URL, attempt: XerahSCloudOAuthAttempt) async throws -> XerahSCloudAccount {
        guard callbackURL.scheme?.lowercased() == XerahSCloudConfiguration.callbackScheme,
              callbackURL.host?.lowercased() == "oauth",
              callbackURL.path.lowercased() == "/callback",
              callbackURL.user == nil, callbackURL.password == nil, callbackURL.fragment == nil,
              let values = URLComponents(url: callbackURL, resolvingAgainstBaseURL: false)?.queryItems,
              values.count == 2,
              !values.contains(where: { ["access_token", "refresh_token", "id_token"].contains($0.name.lowercased()) }),
              Set(values.map(\.name)).count == 2,
              values.filter({ $0.name == "state" }).count == 1,
              values.first(where: { $0.name == "state" })?.value == attempt.state,
              values.filter({ $0.name == "code" }).count + values.filter({ $0.name == "error" }).count == 1 else {
            throw cloudSecurityError("Sign in", "The OAuth callback was invalid or did not match this sign-in attempt.")
        }
        let acceptedAttempt = lock.withLock { () -> Bool in
            guard !consumedOAuthStates.contains(attempt.state),
                  let registeredExpiry = pendingOAuthStates.removeValue(forKey: attempt.state),
                  registeredExpiry == attempt.expiresAt,
                  registeredExpiry > Date() else { return false }
            consumedOAuthStates.insert(attempt.state)
            return true
        }
        guard acceptedAttempt else { throw cloudSecurityError("Sign in", "The OAuth attempt expired or its state was already used.") }
        if let denied = values.first(where: { $0.name == "error" })?.value {
            throw cloudSecurityError("Sign in", "Authorization was denied (\(sanitize(denied, maximum: 64) ?? "unknown_error")).")
        }
        guard let code = values.first(where: { $0.name == "code" })?.value, !code.isEmpty else {
            throw cloudSecurityError("Sign in", "The OAuth callback did not contain an authorization code.")
        }
        let session = try await exchange(code: code, verifier: attempt.codeVerifier, expectedNonce: attempt.nonce)
        try acceptInteractiveSession(session)
        return try await account()
    }

    func restoreSession() async throws -> XerahSCloudAccount? {
        guard hasStoredCredential else { return nil }
        do { return try await account() }
        catch let error as XerahSCloudError where error.statusCode == 401 {
            signOut()
            return nil
        }
    }

    func signOut() {
        lock.withLock {
            currentSession = nil
            activeRefresh?.task.cancel()
            activeRefresh = nil
            sessionGeneration &+= 1
        }
        XerahSCloudCredentialStore.clear()
    }

    func account() async throws -> XerahSCloudAccount {
        let (data, _) = try await authorizedRequest(operation: "Account", method: "GET", path: "api/v1/me")
        let account = try decode(XerahSCloudAccount.self, from: data, operation: "Account")
        guard account.strongAuth, account.slug.range(of: "^[a-z0-9-]{1,30}$", options: .regularExpression) != nil else {
            throw cloudSecurityError("Account", "The account response did not pass security checks.")
        }
        return account
    }

    func history(cursor: String? = nil, limit: Int = 25) async throws -> XerahSCloudGalleryPage {
        var components = URLComponents(url: XerahSCloudConfiguration.apiBaseURL.appendingPathComponent("api/v1/items"), resolvingAgainstBaseURL: false)!
        var query = [URLQueryItem(name: "limit", value: String(min(max(limit, 1), 50)))]
        if let cursor, !cursor.isEmpty { query.append(URLQueryItem(name: "cursor", value: cursor)) }
        components.queryItems = query
        let path = components.url!.absoluteString.replacingOccurrences(of: XerahSCloudConfiguration.apiBaseURL.absoluteString, with: "")
        let (data, _) = try await authorizedRequest(operation: "Cloud history", method: "GET", path: path)
        return try decode(XerahSCloudGalleryPage.self, from: data, operation: "Cloud history")
    }

    func publish(clientItemID: UUID, publicURL: URL, fileName: String, expectedOwnerSubject: String, capturedAt: Date = Date()) async throws {
        guard !expectedOwnerSubject.isEmpty else {
            throw cloudSecurityError("Publish", "The expected Cloud account is required.")
        }
        guard publicURL.scheme?.lowercased() == "https", publicURL.user == nil, publicURL.password == nil else {
            throw cloudSecurityError("Publish", "Only credential-free HTTPS destination URLs can be published.")
        }
        guard let metadata = Self.eligibleMetadata(fileName: publicURL.lastPathComponent) ?? Self.eligibleMetadata(fileName: fileName) else {
            throw cloudSecurityError("Publish", "Only image and video uploads are eligible for XerahS Cloud.")
        }
        let body = XerahSCloudPublishRequest(
            url: publicURL.absoluteString,
            thumbnailUrl: nil,
            kind: metadata.kind,
            fileName: fileName,
            capturedAt: ISO8601DateFormatter().string(from: capturedAt),
            host: publicURL.host,
            contentType: metadata.contentType
        )
        let payload = try JSONEncoder().encode(body)
        let (responseData, _) = try await authorizedRequest(
            operation: "Publish",
            method: "PUT",
            path: "api/v1/items/\(clientItemID.uuidString.lowercased())",
            body: payload,
            idempotencyKey: clientItemID.uuidString.lowercased(),
            expectedOwnerSubject: expectedOwnerSubject
        )
        let response = try decode(XerahSCloudPublishResponse.self, from: responseData, operation: "Publish")
        guard !response.item.id.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw cloudSecurityError("Publish", "The server returned an invalid published item.")
        }
    }

    func unpublish(clientItemID: String) async throws {
        guard UUID(uuidString: clientItemID) != nil else {
            throw cloudSecurityError("Unpublish", "The Cloud item identifier is invalid.")
        }
        _ = try await authorizedRequest(
            operation: "Unpublish",
            method: "DELETE",
            path: "api/v1/items/\(clientItemID)",
            idempotencyKey: "unpublish:\(clientItemID)",
            acceptedStatusCodes: [200, 202, 204, 404]
        )
    }

    static func eligibleMetadata(fileName: String) -> (kind: String, contentType: String)? {
        switch (fileName as NSString).pathExtension.lowercased() {
        case "jpg", "jpeg": return ("screenshot", "image/jpeg")
        case "png": return ("screenshot", "image/png")
        case "gif": return ("screenshot", "image/gif")
        case "webp": return ("screenshot", "image/webp")
        case "heic", "heif": return ("screenshot", "image/heic")
        case "mp4": return ("screencast", "video/mp4")
        case "mov": return ("screencast", "video/quicktime")
        case "webm": return ("screencast", "video/webm")
        default: return nil
        }
    }

    static func stableClientItemID(filePath: String) -> UUID {
        var hash = SHA256()
        hash.update(data: Data((filePath as NSString).lastPathComponent.utf8))
        if let handle = FileHandle(forReadingAtPath: filePath) {
            defer { try? handle.close() }
            while true {
                let data = handle.readData(ofLength: 1_048_576)
                if data.isEmpty { break }
                hash.update(data: data)
            }
        } else {
            hash.update(data: Data(filePath.utf8))
        }
        var bytes = Array(hash.finalize().prefix(16))
        bytes[6] = (bytes[6] & 0x0f) | 0x50
        bytes[8] = (bytes[8] & 0x3f) | 0x80
        let tuple: uuid_t = (bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7], bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15])
        return UUID(uuid: tuple)
    }

    private func exchange(code: String, verifier: String, expectedNonce: String) async throws -> XerahSCloudSession {
        try await tokenRequest(fields: [
            "grant_type": "authorization_code", "code": code,
            "client_id": XerahSCloudConfiguration.clientID,
            "redirect_uri": XerahSCloudConfiguration.oauthRedirectURL.absoluteString,
            "code_verifier": verifier,
            "resource": XerahSCloudConfiguration.apiAudience
        ], expectedNonce: expectedNonce, expectedSubject: nil)
    }

    private func refresh() async throws -> XerahSCloudSession {
        guard let stored = try XerahSCloudCredentialStore.read() else {
            throw cloudSecurityError("Session refresh", "Sign in to XerahS Cloud first.")
        }
        do {
            return try await tokenRequest(fields: [
                "grant_type": "refresh_token", "refresh_token": stored.refreshToken,
                "client_id": XerahSCloudConfiguration.clientID,
                "resource": XerahSCloudConfiguration.apiAudience
            ], expectedNonce: nil, expectedSubject: stored.subject)
        } catch {
            if let cloudError = error as? XerahSCloudError, let status = cloudError.statusCode, (400..<500).contains(status) { signOut() }
            throw error
        }
    }

    private func tokenRequest(fields: [String: String], expectedNonce: String?, expectedSubject: String?) async throws -> XerahSCloudSession {
        let url = XerahSCloudConfiguration.oauthAuthority.appendingPathComponent(XerahSCloudConfiguration.tokenPath)
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        request.httpBody = fields.map { key, value in
            "\(formEncode(key))=\(formEncode(value))"
        }.sorted().joined(separator: "&").data(using: .utf8)
        let (data, response) = try await urlSession.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw cloudSecurityError("OAuth", "The token server returned an invalid response.") }
        guard (200..<300).contains(http.statusCode) else { throw responseError(operation: "OAuth", response: http, data: data) }
        struct Token: Decodable {
            let accessToken: String
            let refreshToken: String
            let idToken: String?
            let expiresIn: Int
            enum CodingKeys: String, CodingKey {
                case accessToken = "access_token"; case refreshToken = "refresh_token"
                case idToken = "id_token"; case expiresIn = "expires_in"
            }
        }
        let token = try decode(Token.self, from: data, operation: "OAuth")
        guard !token.accessToken.isEmpty, !token.refreshToken.isEmpty,
              token.expiresIn > 0, token.expiresIn <= 3600 else { throw cloudSecurityError("OAuth", "The access-token lifetime is outside the accepted policy.") }
        let accessClaims = try await verifiedJWTClaims(token.accessToken)
        let subject = try validateAccessClaims(accessClaims)
        let responseExpiry = Date().addingTimeInterval(TimeInterval(token.expiresIn))
        let tokenExpiry = numberClaim("exp", accessClaims).map(Date.init(timeIntervalSince1970:)) ?? responseExpiry
        if let expectedSubject, subject != expectedSubject { throw cloudSecurityError("OAuth", "Session refresh attempted to switch accounts.") }
        if let expectedNonce {
            guard let idToken = token.idToken else { throw cloudSecurityError("OAuth", "The sign-in response did not contain an ID token.") }
            let claims = try await verifiedJWTClaims(idToken)
            try validateCommonClaims(claims, expectedAudience: XerahSCloudConfiguration.clientID)
            guard fixedTimeEquals(stringClaim("nonce", claims), expectedNonce),
                  stringClaim("sub", claims) == subject else { throw cloudSecurityError("OAuth", "The OpenID nonce or subject did not match.") }
        } else if let idToken = token.idToken {
            let claims = try await verifiedJWTClaims(idToken)
            try validateCommonClaims(claims, expectedAudience: XerahSCloudConfiguration.clientID)
            guard stringClaim("sub", claims) == subject else { throw cloudSecurityError("OAuth", "The refreshed OpenID subject did not match.") }
        }
        return XerahSCloudSession(accessToken: token.accessToken, refreshToken: token.refreshToken, ownerSubject: subject, expiresAt: min(responseExpiry, tokenExpiry))
    }

    private func acceptInteractiveSession(_ session: XerahSCloudSession) throws {
        lock.lock()
        defer { lock.unlock() }
        activeRefresh?.task.cancel()
        activeRefresh = nil
        sessionGeneration &+= 1
        try XerahSCloudCredentialStore.write(subject: session.ownerSubject, refreshToken: session.refreshToken)
        currentSession = session
    }

    private func acceptRefreshedSession(_ session: XerahSCloudSession, expectedGeneration: UInt64) throws {
        lock.lock()
        defer { lock.unlock() }
        guard sessionGeneration == expectedGeneration, !Task.isCancelled else {
            throw CancellationError()
        }
        try XerahSCloudCredentialStore.write(subject: session.ownerSubject, refreshToken: session.refreshToken)
        currentSession = session
    }

    private func session(forceRefresh: Bool) async throws -> XerahSCloudSession {
        let cached = lock.withLock { currentSession }
        if !forceRefresh, let cached, cached.expiresAt > Date().addingTimeInterval(60) { return cached }
        let refreshOperation = lock.withLock { () -> (id: UUID, task: Task<XerahSCloudSession, Error>, isOwner: Bool) in
            if let activeRefresh { return (activeRefresh.id, activeRefresh.task, false) }
            let id = UUID()
            let generation = sessionGeneration
            let task = Task {
                let updated = try await self.refresh()
                try Task.checkCancellation()
                try self.acceptRefreshedSession(updated, expectedGeneration: generation)
                return updated
            }
            activeRefresh = (id, task)
            return (id, task, true)
        }
        do {
            let updated = try await refreshOperation.task.value
            if refreshOperation.isOwner {
                lock.withLock {
                    if activeRefresh?.id == refreshOperation.id { activeRefresh = nil }
                }
            }
            return updated
        } catch {
            if refreshOperation.isOwner {
                lock.withLock {
                    if activeRefresh?.id == refreshOperation.id { activeRefresh = nil }
                }
            }
            throw error
        }
    }

    private func authorizedRequest(
        operation: String,
        method: String,
        path: String,
        body: Data? = nil,
        idempotencyKey: String? = nil,
        expectedOwnerSubject: String? = nil,
        acceptedStatusCodes: Set<Int> = Set(200..<300)
    ) async throws -> (Data, HTTPURLResponse) {
        var active = try await session(forceRefresh: false)
        for attempt in 0...1 {
            if let expectedOwnerSubject, active.ownerSubject != expectedOwnerSubject {
                throw cloudSecurityError(operation, "The authenticated Cloud account changed before the request could be sent.")
            }
            let url = URL(string: path, relativeTo: XerahSCloudConfiguration.apiBaseURL)!.absoluteURL
            var request = URLRequest(url: url)
            request.httpMethod = method
            request.setValue("Bearer \(active.accessToken)", forHTTPHeaderField: "Authorization")
            request.setValue("application/json", forHTTPHeaderField: "Accept")
            if let body { request.httpBody = body; request.setValue("application/json", forHTTPHeaderField: "Content-Type") }
            if let idempotencyKey { request.setValue(idempotencyKey, forHTTPHeaderField: "Idempotency-Key") }
            let (data, response) = try await urlSession.data(for: request)
            guard let http = response as? HTTPURLResponse else { throw cloudSecurityError(operation, "The server returned an invalid response.") }
            if http.statusCode == 401, attempt == 0 {
                active = try await session(forceRefresh: true)
                if let expectedOwnerSubject, active.ownerSubject != expectedOwnerSubject {
                    throw cloudSecurityError(operation, "The authenticated Cloud account changed during session refresh.")
                }
                continue
            }
            guard acceptedStatusCodes.contains(http.statusCode) else { throw responseError(operation: operation, response: http, data: data) }
            return (data, http)
        }
        throw cloudSecurityError(operation, "Authentication failed after one refresh retry.")
    }

    private func responseError(operation: String, response: HTTPURLResponse, data: Data) -> XerahSCloudError {
        let envelope = try? JSONDecoder().decode(XerahSCloudAPIErrorEnvelope.self, from: data)
        let headerCorrelation = response.value(forHTTPHeaderField: "X-Correlation-ID")
        return XerahSCloudError(
            operation: operation,
            statusCode: response.statusCode,
            code: sanitize(envelope?.error?.code, maximum: 64),
            detail: sanitize(envelope?.error?.message, maximum: 256) ?? "The server rejected the request.",
            correlationID: sanitize(envelope?.error?.correlationId ?? headerCorrelation, maximum: 128)
        )
    }

    private func validateAccessClaims(_ claims: [String: Any]) throws -> String {
        try validateCommonClaims(claims, expectedAudience: XerahSCloudConfiguration.apiAudience)
        guard stringClaim("client_id", claims) == XerahSCloudConfiguration.clientID,
              stringClaim("aal", claims) == "aal2",
              let subject = stringClaim("sub", claims), !subject.isEmpty,
              let sessionID = stringClaim("session_id", claims), !sessionID.isEmpty,
              let expiry = numberClaim("exp", claims),
              expiry <= Date().addingTimeInterval(3660).timeIntervalSince1970 else {
            throw cloudSecurityError("OAuth", "The access token claims did not pass security checks.")
        }
        return subject
    }

    private func validateCommonClaims(_ claims: [String: Any], expectedAudience: String) throws {
        let issuer = XerahSCloudConfiguration.issuer
        let now = Date().timeIntervalSince1970
        guard stringClaim("iss", claims)?.trimmingCharacters(in: CharacterSet(charactersIn: "/")) == issuer,
              hasAudience(claims, expected: expectedAudience),
              let expiry = numberClaim("exp", claims), expiry > now - 60 else {
            throw cloudSecurityError("OAuth", "The token issuer, audience, or expiry was invalid.")
        }
        if let notBefore = numberClaim("nbf", claims), notBefore > now + 60 {
            throw cloudSecurityError("OAuth", "The token is not valid yet.")
        }
    }

    private func hasAudience(_ claims: [String: Any], expected: String) -> Bool {
        if let value = claims["aud"] as? String { return value == expected }
        if let values = claims["aud"] as? [String] { return values.contains(expected) }
        return false
    }

    private struct JSONWebKeySet: Decodable { let keys: [JSONWebKey] }
    private struct JSONWebKey: Decodable {
        let kid: String
        let kty: String
        let alg: String?
        let n: String?
        let e: String?
        let crv: String?
        let x: String?
        let y: String?
    }

    private func verifiedJWTClaims(_ token: String) async throws -> [String: Any] {
        let parts = token.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3, token.count <= 32768,
              let headerData = Data(base64URLEncoded: String(parts[0])),
              let header = try JSONSerialization.jsonObject(with: headerData) as? [String: Any],
              let data = Data(base64URLEncoded: String(parts[1])),
              let object = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let algorithm = header["alg"] as? String,
              let keyID = header["kid"] as? String,
              ["RS256", "ES256"].contains(algorithm),
              let signature = Data(base64URLEncoded: String(parts[2])) else {
            throw cloudSecurityError("OAuth", "The token server returned a malformed JWT.")
        }
        let jwksURL = XerahSCloudConfiguration.oauthAuthority.appendingPathComponent(XerahSCloudConfiguration.jwksPath)
        let (jwksData, response) = try await urlSession.data(from: jwksURL)
        guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
            throw cloudSecurityError("OAuth", "The signing-key endpoint could not be verified.")
        }
        let keySet = try decode(JSONWebKeySet.self, from: jwksData, operation: "OAuth")
        guard let key = keySet.keys.first(where: { $0.kid == keyID && ($0.alg == nil || $0.alg == algorithm) }) else {
            throw cloudSecurityError("OAuth", "The token signing key was not found.")
        }
        let signedData = Data("\(parts[0]).\(parts[1])".utf8)
        let signatureValid: Bool
        switch algorithm {
        case "ES256": signatureValid = verifyES256(key: key, message: signedData, signature: signature)
        case "RS256": signatureValid = verifyRS256(key: key, message: signedData, signature: signature)
        default: signatureValid = false
        }
        guard signatureValid else { throw cloudSecurityError("OAuth", "The token signature was invalid.") }
        return object
    }

    private func verifyES256(key: JSONWebKey, message: Data, signature: Data) -> Bool {
        guard key.kty == "EC", key.crv == "P-256", let x = key.x.flatMap(Data.init(base64URLEncoded:)),
              let y = key.y.flatMap(Data.init(base64URLEncoded:)), x.count == 32, y.count == 32 else { return false }
        var representation = Data([0x04]); representation.append(x); representation.append(y)
        guard let publicKey = try? P256.Signing.PublicKey(x963Representation: representation),
              let ecdsaSignature = try? P256.Signing.ECDSASignature(rawRepresentation: signature) else { return false }
        return publicKey.isValidSignature(ecdsaSignature, for: message)
    }

    private func verifyRS256(key: JSONWebKey, message: Data, signature: Data) -> Bool {
        guard key.kty == "RSA", let modulus = key.n.flatMap(Data.init(base64URLEncoded:)),
              let exponent = key.e.flatMap(Data.init(base64URLEncoded:)) else { return false }
        let rsaData = derSequence(derInteger(modulus) + derInteger(exponent))
        let attributes: [CFString: Any] = [
            kSecAttrKeyType: kSecAttrKeyTypeRSA,
            kSecAttrKeyClass: kSecAttrKeyClassPublic,
            kSecAttrKeySizeInBits: modulus.count * 8
        ]
        guard let publicKey = SecKeyCreateWithData(rsaData as CFData, attributes as CFDictionary, nil) else { return false }
        return SecKeyVerifySignature(publicKey, .rsaSignatureMessagePKCS1v15SHA256, message as CFData, signature as CFData, nil)
    }

    private func derInteger(_ value: Data) -> Data {
        var bytes = value
        while bytes.count > 1 && bytes.first == 0 && (bytes.dropFirst().first ?? 0) < 0x80 { bytes.removeFirst() }
        if let first = bytes.first, first >= 0x80 { bytes.insert(0, at: 0) }
        return Data([0x02]) + derLength(bytes.count) + bytes
    }

    private func derSequence(_ value: Data) -> Data { Data([0x30]) + derLength(value.count) + value }
    private func derLength(_ length: Int) -> Data {
        if length < 128 { return Data([UInt8(length)]) }
        var value = length
        var bytes: [UInt8] = []
        while value > 0 { bytes.insert(UInt8(value & 0xff), at: 0); value >>= 8 }
        return Data([0x80 | UInt8(bytes.count)] + bytes)
    }

    private func stringClaim(_ key: String, _ claims: [String: Any]) -> String? { claims[key] as? String }
    private func numberClaim(_ key: String, _ claims: [String: Any]) -> TimeInterval? {
        if let value = claims[key] as? NSNumber { return value.doubleValue }
        return nil
    }
    private func fixedTimeEquals(_ lhs: String?, _ rhs: String) -> Bool {
        guard let lhs else { return false }
        let left = Array(lhs.utf8), right = Array(rhs.utf8)
        var difference = UInt8(truncatingIfNeeded: left.count ^ right.count)
        for index in 0..<max(left.count, right.count) {
            difference |= (index < left.count ? left[index] : 0) ^ (index < right.count ? right[index] : 0)
        }
        return difference == 0
    }
    private func decode<T: Decodable>(_ type: T.Type, from data: Data, operation: String) throws -> T {
        do { return try JSONDecoder().decode(type, from: data) }
        catch { throw cloudSecurityError(operation, "The server returned an invalid response.") }
    }
    private func randomBase64URL(byteCount: Int) -> String {
        var bytes = [UInt8](repeating: 0, count: byteCount)
        let status = SecRandomCopyBytes(kSecRandomDefault, byteCount, &bytes)
        precondition(status == errSecSuccess)
        return Data(bytes).base64URLEncodedString()
    }
    private func formEncode(_ value: String) -> String {
        value.addingPercentEncoding(withAllowedCharacters: CharacterSet.alphanumerics.union(CharacterSet(charactersIn: "-._~"))) ?? ""
    }
    private func sanitize(_ value: String?, maximum: Int) -> String? {
        guard let value else { return nil }
        let safe = value.unicodeScalars.filter { !CharacterSet.controlCharacters.contains($0) }.prefix(maximum)
        return String(String.UnicodeScalarView(safe)).trimmingCharacters(in: .whitespacesAndNewlines)
    }
    private func cloudSecurityError(_ operation: String, _ detail: String) -> XerahSCloudError {
        XerahSCloudError(operation: operation, statusCode: nil, code: "security_error", detail: detail, correlationID: nil)
    }
}

private extension Data {
    func base64URLEncodedString() -> String {
        base64EncodedString().trimmingCharacters(in: CharacterSet(charactersIn: "=")).replacingOccurrences(of: "+", with: "-").replacingOccurrences(of: "/", with: "_")
    }

    init?(base64URLEncoded value: String) {
        var normalized = value.replacingOccurrences(of: "-", with: "+").replacingOccurrences(of: "_", with: "/")
        normalized += String(repeating: "=", count: (4 - normalized.count % 4) % 4)
        self.init(base64Encoded: normalized)
    }
}
