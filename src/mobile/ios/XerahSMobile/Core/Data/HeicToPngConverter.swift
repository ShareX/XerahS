//
//  HeicToPngConverter.swift
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

import Foundation
import UIKit

private let heicExtensions: Set<String> = ["heic", "heif"]

/// If `convertEnabled` is true and `filePath` is a HEIC/HEIF image, decodes it and writes a PNG to cache,
/// returning the path to the PNG. Otherwise returns `filePath`.
func convertHeicToPngIfNeeded(filePath: String, convertEnabled: Bool) -> String {
    guard convertEnabled else { return filePath }
    let url = URL(fileURLWithPath: filePath)
    guard FileManager.default.fileExists(atPath: filePath) else { return filePath }
    let ext = url.pathExtension.lowercased()
    guard heicExtensions.contains(ext) else { return filePath }
    guard let cacheDir = Paths.cacheDir else { return filePath }
    guard let image = UIImage(contentsOfFile: filePath),
          let pngData = image.pngData() else { return filePath }
    let baseName = url.deletingPathExtension().lastPathComponent
    let outFile = cacheDir.appendingPathComponent("\(baseName)_converted.png")
    do {
        try pngData.write(to: outFile)
        return outFile.path
    } catch {
        return filePath
    }
}
