//
//  AppIdentifiers.swift
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

/// App Group and keychain identifiers shared by the app and the Share Extension.
///
/// xtool development builds are re-signed under `XTL-<id>.`-prefixed bundle IDs. xtool reuses the
/// team's existing App Group when it can and otherwise registers `group.XTL-<id>.com.xerahs.xerahs`,
/// and it signs both targets with the main app's `<team>.<bundle ID>` keychain group. Derive the
/// identifiers from the running build so xtool and Xcode/App Store builds both use what they were
/// actually provisioned with.
enum AppIdentifiers {
    private static let teamID = "7C5AS6VPUH"
    private static let baseAppGroupId = "group.com.xerahs.xerahs"
    private static let extensionSuffix = ".ShareExtension"

    /// `XTL-<id>` when running an xtool-signed build; otherwise nil.
    static let xtoolPrefix: String? = {
        guard let first = Bundle.main.bundleIdentifier?.split(separator: ".").first,
              first.hasPrefix("XTL-") else { return nil }
        return String(first)
    }()

    /// Bundle ID of the containing app, also when running in the Share Extension.
    private static let appBundleID: String = {
        let id = Bundle.main.bundleIdentifier ?? "com.xerahs.xerahs.mobile"
        return id.hasSuffix(extensionSuffix) ? String(id.dropLast(extensionSuffix.count)) : id
    }()

    /// The App Group this build is entitled to: iOS only returns a container for entitled groups.
    static let appGroupId: String = {
        var candidates = [baseAppGroupId]
        if let xtoolPrefix { candidates.append("group.\(xtoolPrefix).com.xerahs.xerahs") }
        return candidates.first {
            FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: $0) != nil
        } ?? baseAppGroupId
    }()

    /// Both targets are signed with the main app's keychain group, `<team>.<app bundle ID>`
    /// (`7C5AS6VPUH.com.xerahs.xerahs.mobile` for Xcode/App Store builds).
    static let keychainAccessGroup = "\(teamID).\(appBundleID)"
}
