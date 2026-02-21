//
//  ShareGroup.swift
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

/// App Group used by the Share Extension to pass file paths to the main app.
enum ShareGroup {
    static let appGroupId = "group.com.getsharex.xerahs"
    static let pendingPathsKey = "PendingSharedPaths"

    /// Reads and clears pending shared file paths from the Share Extension. Call on launch and when opened via xerahs://share.
    static func consumePendingPaths() -> [String] {
        guard let defaults = UserDefaults(suiteName: appGroupId) else { return [] }
        let paths = (defaults.array(forKey: pendingPathsKey) as? [String]) ?? []
        defaults.removeObject(forKey: pendingPathsKey)
        defaults.synchronize()
        return paths
    }
}
