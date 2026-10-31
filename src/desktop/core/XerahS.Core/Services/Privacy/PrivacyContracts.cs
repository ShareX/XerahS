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

using SkiaSharp;

namespace XerahS.Core.Services.Privacy;

public enum PrivacyCategory
{
    Email, PersonName, Phone, Address, Location, GovernmentId, Financial, PaymentCard,
    Credential, Medical, PrivateMessage, CustomerData, DeviceIdentifier, EncodedSecret, Custom
}
public enum PrivacyScanStatus { Complete, Incomplete, Unsupported, Error, Cancelled, ManuallyReviewed }
public enum PrivacyRedactionEffect { Blur, Pixelate, Mask }
public enum PrivacyReviewDecision { Cancel, KeepLocal, Upload }
public sealed record PrivacyFinding(PrivacyCategory Category, double X, double Y, double Width, double Height, double Confidence);
public sealed record PrivacyScanResult(PrivacyScanStatus Status, IReadOnlyList<PrivacyFinding> Findings, string? Limitation = null)
{
    public static PrivacyScanResult Unsupported => new(PrivacyScanStatus.Unsupported, []);
}
public interface IPrivacyAnalysisProvider
{
    string Identity { get; }
    string DisplayName { get; }
    bool RequiresCloudConsent { get; }
    Task<PrivacyScanResult> AnalyzeAsync(SKBitmap image, TaskSettingsPrivacy policy, CancellationToken token);
}
public sealed record PrivacyReviewResult(PrivacyReviewDecision Decision, SKBitmap? Image);
public interface IPrivacyReviewService
{
    Task<bool> ConfirmCloudAnalysisAsync(string provider, CancellationToken token);
    Task<PrivacyReviewResult> ReviewAsync(SKBitmap original, SKBitmap candidate, PrivacyScanResult scan,
        string destination, TaskSettingsPrivacy policy, CancellationToken token);
}

/// <summary>Composition boundary: GUI hosts supply analysis/review; other hosts fail closed.</summary>
public static class PrivacyServices
{
    public static Func<IPrivacyAnalysisProvider?>? AnalysisProviderFactory { get; set; }
    public static IPrivacyReviewService? ReviewService { get; set; }
}
