//
//  SettingsRepository.swift
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

private let configFileName = "ApplicationConfig.json"

/// Load/save ApplicationConfig as JSON in settings folder. Thread-safe via serial queue.
final class SettingsRepository {
    private let queue = DispatchQueue(label: "SettingsRepository")
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    private var configFile: URL? {
        Paths.settingsFolder?.appendingPathComponent(configFileName)
    }

    func load() -> ApplicationConfig {
        queue.sync {
            guard let file = configFile, FileManager.default.fileExists(atPath: file.path) else {
                return ApplicationConfig()
            }
            do {
                let data = try Data(contentsOf: file)
                return (try? decoder.decode(ApplicationConfig.self, from: data)) ?? ApplicationConfig()
            } catch {
                return ApplicationConfig()
            }
        }
    }

    func save(_ config: ApplicationConfig) {
        queue.sync {
            guard let file = configFile else { return }
            Paths.settingsFolder.flatMap { try? FileManager.default.createDirectory(at: $0, withIntermediateDirectories: true) }
            try? encoder.encode(config).write(to: file)
        }
    }

    func loadS3Config() -> S3Config { load().s3Config }
    func saveS3Config(_ config: S3Config) {
        var c = load()
        c.s3Config = config
        save(c)
    }

    func loadCustomUploaders() -> [CustomUploaderEntry] { load().customUploaders }
    func saveCustomUploaders(_ list: [CustomUploaderEntry]) {
        var c = load()
        c.customUploaders = list
        save(c)
    }

    func getDefaultDestinationInstanceId() -> String? { load().defaultDestinationInstanceId }
    func setDefaultDestinationInstanceId(_ id: String?) {
        var c = load()
        c.defaultDestinationInstanceId = id
        save(c)
    }
}
