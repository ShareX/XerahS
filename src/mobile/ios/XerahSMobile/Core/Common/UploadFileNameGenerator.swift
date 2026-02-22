//
//  UploadFileNameGenerator.swift
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

/// Generates upload file names using the same default pattern as desktop: %y%mo%dT%h%mi_%ra{10}
/// (year, zero-padded month/day/hour/minute, literal T, underscore, 10 random alphanumeric).
enum UploadFileNameGenerator {
    private static let alphanumeric = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"

    /// Returns a file name for upload: pattern base + original extension from file path.
    /// Matches desktop TaskSettings.NameFormatPattern default: "%y%mo%dT%h%mi_%ra{10}".
    static func uploadFileName(for filePath: String) -> String {
        let ext = (filePath as NSString).pathExtension
        let base = defaultPatternBase()
        if ext.isEmpty { return base }
        return "\(base).\(ext)"
    }

    /// Pattern base only: yyyyMMddTHHmm_ + 10 random alphanumeric (no extension).
    private static func defaultPatternBase() -> String {
        let now = Date()
        let cal = Calendar(identifier: .gregorian)
        let y = cal.component(.year, from: now)
        let mo = cal.component(.month, from: now)
        let d = cal.component(.day, from: now)
        let h = cal.component(.hour, from: now)
        let mi = cal.component(.minute, from: now)
        let timePart = String(format: "%04d%02d%02dT%02d%02d", y, mo, d, h, mi)
        let randomPart = (0..<10).map { _ in alphanumeric.randomElement()! }.map(String.init).joined()
        return "\(timePart)_\(randomPart)"
    }
}
