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

namespace XerahS.OmaXerahs.Models;

internal static class CliErrorCodes
{
    public const string NotReady = "not_ready";
    public const string Auth = "auth";
    public const string InvalidPath = "invalid_path";
    public const string UnsupportedType = "unsupported_type";
    public const string Network = "network";
    public const string Provider = "provider";
    public const string Cancelled = "cancelled";
    public const string Timeout = "timeout";
    public const string SecretStore = "secret_store";
    public const string Incompatible = "incompatible";
    public const string Usage = "usage";
    public const string NotFound = "not_found";
    public const string Ambiguous = "ambiguous";
    public const string InvalidValue = "invalid_value";
}

internal sealed class CliError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

internal sealed class CliFailureResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; }
    public CliError Error { get; init; } = new();

    public static CliFailureResponse Create(string code, string message)
    {
        return new CliFailureResponse
        {
            SchemaVersion = 1,
            Ok = false,
            Error = new CliError { Code = code, Message = message }
        };
    }
}

internal sealed class CapabilitiesResponse
{
    public int SchemaVersion { get; init; } = 1;
    public string Name { get; init; } = "omaxerahs";
    public string Version { get; init; } = "0.1.0";
    public int MinPluginProtocol { get; init; } = 1;
    public string[] Capabilities { get; init; } = [];
}

internal sealed class DoctorResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; }
    public DoctorCliInfo Cli { get; init; } = new();
    public DoctorImageInfo Image { get; init; } = new();
    public DoctorSecretStoreInfo SecretStore { get; init; } = new();
    public DoctorPluginsInfo Plugins { get; init; } = new();
    public DoctorHistoryInfo History { get; init; } = new();
}

/// <summary>Whether the native SQLite provider loads, so uploads can be written to history.</summary>
internal sealed class DoctorHistoryInfo
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
}

internal sealed class DoctorCliInfo
{
    public string Name { get; init; } = "omaxerahs";
    public string Version { get; init; } = "0.1.0";
}

internal sealed class DoctorImageInfo
{
    public bool Ready { get; init; }
    public string? ProviderId { get; init; }
    public string? InstanceId { get; init; }
    public string? DisplayName { get; init; }

    /// <summary>"Image", or "File" when no Image destination exists and uploads fall back to a File destination.</summary>
    public string? Category { get; init; }
}

internal sealed class DoctorSecretStoreInfo
{
    public string Backend { get; init; } = "unknown";
    public bool Fallback { get; init; }
}

internal sealed class DoctorPluginsInfo
{
    public int Loaded { get; init; }
}

internal sealed class UploadSuccessResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public string Url { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
    public long Size { get; init; }
    public string Type { get; init; } = "application/octet-stream";
    public string DataType { get; init; } = "image";
    public string? ProviderId { get; init; }
    public string? InstanceId { get; init; }
    public string? DisplayName { get; init; }
}
