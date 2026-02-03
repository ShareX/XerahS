#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Security;

public sealed class SecretStore : ISecretStore, ISecretStoreInfo
{
    private readonly ISecretStore _backend;
    private readonly string _backendName;
    private readonly string _backendDetails;
    private readonly bool _isFallback;

    public SecretStore(string filePath)
    {
        var backend = CreateBackend(filePath);
        _backend = backend.Backend;
        _backendName = backend.Name;
        _backendDetails = backend.Details;
        _isFallback = backend.IsFallback;
    }

    public string BackendName => _backendName;

    public string BackendDetails => _backendDetails;

    public bool IsFallback => _isFallback;

    public string? GetSecret(string providerId, string secretKey, string name)
        => _backend.GetSecret(providerId, secretKey, name);

    public void SetSecret(string providerId, string secretKey, string name, string value)
        => _backend.SetSecret(providerId, secretKey, name, value);

    public void DeleteSecret(string providerId, string secretKey, string name)
        => _backend.DeleteSecret(providerId, secretKey, name);

    public bool HasSecret(string providerId, string secretKey, string name)
        => _backend.HasSecret(providerId, secretKey, name);

    private static BackendSelection CreateBackend(string filePath)
    {
        const string serviceName = "XerahS";

        if (OperatingSystem.IsWindows())
        {
            return BackendSelection.Create(
                new DpapiFileSecretStore(filePath),
                "Windows DPAPI file store",
                "DPAPI-protected SecretsStore.json",
                isFallback: false);
        }

        if (OperatingSystem.IsMacOS())
        {
            if (MacOSKeychainSecretStore.IsAvailable())
            {
                return BackendSelection.Create(
                    new MacOSKeychainSecretStore(serviceName),
                    "macOS Keychain",
                    $"Stored in Keychain service \"{serviceName}\"",
                    isFallback: false);
            }

            return BackendSelection.Create(
                new AesFileSecretStore(filePath, GetKeyPath(filePath)),
                "AES file store (fallback)",
                "AES-GCM SecretsStore.json + SecretsStore.key",
                isFallback: true);
        }

        if (OperatingSystem.IsLinux())
        {
            if (LinuxLibsecretSecretStore.IsAvailable())
            {
                return BackendSelection.Create(
                    new LinuxLibsecretSecretStore(serviceName),
                    "Linux libsecret",
                    $"Stored via secret-tool service \"{serviceName}\"",
                    isFallback: false);
            }

            return BackendSelection.Create(
                new AesFileSecretStore(filePath, GetKeyPath(filePath)),
                "AES file store (fallback)",
                "AES-GCM SecretsStore.json + SecretsStore.key",
                isFallback: true);
        }

        return BackendSelection.Create(
            new AesFileSecretStore(filePath, GetKeyPath(filePath)),
            "AES file store (fallback)",
            "AES-GCM SecretsStore.json + SecretsStore.key",
            isFallback: true);
    }

    private static string GetKeyPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? ".";
        return Path.Combine(directory, "SecretsStore.key");
    }

    private sealed class DpapiFileSecretStore : ISecretStore
    {
        private readonly object _lock = new();
        private readonly string _filePath;
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);
        private bool _loaded;

        public DpapiFileSecretStore(string filePath)
        {
            _filePath = filePath;
        }

        public string? GetSecret(string providerId, string secretKey, string name)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                if (!_secrets.TryGetValue(key, out var encrypted))
                {
                    return null;
                }

                try
                {
                    return DPAPI.Decrypt(encrypted);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, $"SecretStore failed to decrypt secret: {key}");
                    return null;
                }
            }
        }

        public void SetSecret(string providerId, string secretKey, string name, string value)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                _secrets[key] = DPAPI.Encrypt(value);
                Save();
            }
        }

        public void DeleteSecret(string providerId, string secretKey, string name)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                if (_secrets.Remove(key))
                {
                    Save();
                }
            }
        }

        public bool HasSecret(string providerId, string secretKey, string name)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                return _secrets.ContainsKey(key);
            }
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var model = JsonConvert.DeserializeObject<SecretStoreModel>(json);
                    if (model?.Secrets != null)
                    {
                        foreach (var pair in model.Secrets)
                        {
                            _secrets[pair.Key] = pair.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "SecretStore failed to load secrets file.");
                }
            }

            _loaded = true;
        }

        private void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var model = new SecretStoreModel
                {
                    Version = 1,
                    Secrets = new Dictionary<string, string>(_secrets)
                };

                var json = JsonConvert.SerializeObject(model, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "SecretStore failed to save secrets file.");
            }
        }
    }

    private sealed class AesFileSecretStore : ISecretStore
    {
        private readonly object _lock = new();
        private readonly string _filePath;
        private readonly string _keyPath;
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);
        private bool _loaded;

        public AesFileSecretStore(string filePath, string keyPath)
        {
            _filePath = filePath;
            _keyPath = keyPath;
        }

        public string? GetSecret(string providerId, string secretKey, string name)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                if (!_secrets.TryGetValue(key, out var encrypted))
                {
                    return null;
                }

                try
                {
                    var bytes = Convert.FromBase64String(encrypted);
                    if (bytes.Length < 12 + 16)
                    {
                        return null;
                    }

                    var nonce = bytes.AsSpan(0, 12).ToArray();
                    var tag = bytes.AsSpan(12, 16).ToArray();
                    var cipher = bytes.AsSpan(28).ToArray();

                    var keyBytes = LoadOrCreateKey();
                    var plain = new byte[cipher.Length];
                    using var aes = new AesGcm(keyBytes, 16);
                    aes.Decrypt(nonce, cipher, tag, plain);
                    return Encoding.UTF8.GetString(plain);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, $"SecretStore AES decrypt failed for {key}");
                    return null;
                }
            }
        }

        public void SetSecret(string providerId, string secretKey, string name, string value)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                var keyBytes = LoadOrCreateKey();
                var nonce = RandomNumberGenerator.GetBytes(12);
                var plain = Encoding.UTF8.GetBytes(value);
                var cipher = new byte[plain.Length];
                var tag = new byte[16];
                using var aes = new AesGcm(keyBytes, 16);
                aes.Encrypt(nonce, plain, cipher, tag);
                var combined = new byte[nonce.Length + tag.Length + cipher.Length];
                Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
                Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
                Buffer.BlockCopy(cipher, 0, combined, nonce.Length + tag.Length, cipher.Length);
                _secrets[key] = Convert.ToBase64String(combined);
                Save();
            }
        }

        public void DeleteSecret(string providerId, string secretKey, string name)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                if (_secrets.Remove(key))
                {
                    Save();
                }
            }
        }

        public bool HasSecret(string providerId, string secretKey, string name)
        {
            var key = BuildKey(providerId, secretKey, name);
            lock (_lock)
            {
                EnsureLoaded();
                return _secrets.ContainsKey(key);
            }
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var model = JsonConvert.DeserializeObject<SecretStoreModel>(json);
                    if (model?.Secrets != null)
                    {
                        foreach (var pair in model.Secrets)
                        {
                            _secrets[pair.Key] = pair.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "SecretStore failed to load AES secrets file.");
                }
            }

            _loaded = true;
        }

        private void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var model = new SecretStoreModel
                {
                    Version = 1,
                    Secrets = new Dictionary<string, string>(_secrets)
                };

                var json = JsonConvert.SerializeObject(model, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "SecretStore failed to save AES secrets file.");
            }
        }

        private byte[] LoadOrCreateKey()
        {
            try
            {
                if (File.Exists(_keyPath))
                {
                    var text = File.ReadAllText(_keyPath);
                    return Convert.FromBase64String(text);
                }

                var key = RandomNumberGenerator.GetBytes(32);
                var directory = Path.GetDirectoryName(_keyPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_keyPath, Convert.ToBase64String(key));
                return key;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "SecretStore failed to load or create AES key.");
                return RandomNumberGenerator.GetBytes(32);
            }
        }
    }

    private sealed class MacOSKeychainSecretStore : ISecretStore
    {
        private readonly string _service;
        private readonly string _securityPath;

        public MacOSKeychainSecretStore(string service)
        {
            _service = service;
            _securityPath = File.Exists("/usr/bin/security") ? "/usr/bin/security" : "security";
        }

        public static bool IsAvailable()
        {
            return File.Exists("/usr/bin/security") || CommandExists("security", "-h");
        }

        public string? GetSecret(string providerId, string secretKey, string name)
        {
            var account = BuildKey(providerId, secretKey, name);
            var result = RunCommand(_securityPath, $"find-generic-password -a \"{account}\" -s \"{_service}\" -w");
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }

        public void SetSecret(string providerId, string secretKey, string name, string value)
        {
            var account = BuildKey(providerId, secretKey, name);
            RunCommand(_securityPath, $"add-generic-password -a \"{account}\" -s \"{_service}\" -w \"{value}\" -U");
        }

        public void DeleteSecret(string providerId, string secretKey, string name)
        {
            var account = BuildKey(providerId, secretKey, name);
            RunCommand(_securityPath, $"delete-generic-password -a \"{account}\" -s \"{_service}\"");
        }

        public bool HasSecret(string providerId, string secretKey, string name)
        {
            var account = BuildKey(providerId, secretKey, name);
            var result = RunCommand(_securityPath, $"find-generic-password -a \"{account}\" -s \"{_service}\" -w");
            return !string.IsNullOrWhiteSpace(result);
        }
    }

    private sealed class LinuxLibsecretSecretStore : ISecretStore
    {
        private readonly string _service;

        public LinuxLibsecretSecretStore(string service)
        {
            _service = service;
        }

        public static bool IsAvailable()
        {
            return CommandExists("secret-tool", "--version");
        }

        public string? GetSecret(string providerId, string secretKey, string name)
        {
            var account = BuildKey(providerId, secretKey, name);
            var result = RunCommand("secret-tool", $"lookup service {_service} key \"{account}\"");
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }

        public void SetSecret(string providerId, string secretKey, string name, string value)
        {
            var account = BuildKey(providerId, secretKey, name);
            RunCommandWithInput("secret-tool", $"store --label=\"XerahS Secret\" service {_service} key \"{account}\"", value);
        }

        public void DeleteSecret(string providerId, string secretKey, string name)
        {
            var account = BuildKey(providerId, secretKey, name);
            RunCommand("secret-tool", $"clear service {_service} key \"{account}\"");
        }

        public bool HasSecret(string providerId, string secretKey, string name)
        {
            var account = BuildKey(providerId, secretKey, name);
            var result = RunCommand("secret-tool", $"lookup service {_service} key \"{account}\"");
            return !string.IsNullOrWhiteSpace(result);
        }
    }

    private sealed class SecretStoreModel
    {
        public int Version { get; set; }
        public Dictionary<string, string> Secrets { get; set; } = new();
    }

    private static string BuildKey(string providerId, string secretKey, string name)
    {
        return $"{providerId}:{secretKey}:{name}";
    }

    private static bool CommandExists(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                DebugHelper.WriteLine($"SecretStore command error: {error}");
            }

            return output;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"SecretStore command failed: {fileName} {arguments}");
            return string.Empty;
        }
    }

    private static void RunCommandWithInput(string fileName, string arguments, string input)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return;
            }

            process.StandardInput.Write(input);
            process.StandardInput.Close();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                DebugHelper.WriteLine($"SecretStore command error: {error}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"SecretStore command failed: {fileName} {arguments}");
        }
    }

    private readonly record struct BackendSelection(ISecretStore Backend, string Name, string Details, bool IsFallback)
    {
        public static BackendSelection Create(ISecretStore backend, string name, string details, bool isFallback)
            => new(backend, name, details, isFallback);
    }
}
