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

using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShareX.AmazonS3.Plugin;

internal static class AwsS3Signer
{
    private const string Algorithm = "AWS4-HMAC-SHA256";

    public static void Sign(NameValueCollection headers, string method, string canonicalUri, string canonicalQueryString, string region,
        string accessKeyId, string secretAccessKey, string? sessionToken, string hashedPayload, DateTime? utcNow = null)
    {
        DateTime now = utcNow ?? DateTime.UtcNow;
        string dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string timeStamp = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        headers["x-amz-date"] = timeStamp;
        headers["x-amz-content-sha256"] = hashedPayload;

        if (!string.IsNullOrEmpty(sessionToken))
        {
            headers["x-amz-security-token"] = sessionToken;
        }

        string scope = string.Join("/", dateStamp, region, "s3", "aws4_request");
        string canonicalHeaders = CreateCanonicalHeaders(headers);
        string signedHeaders = GetSignedHeaders(headers);

        string canonicalRequest = $"{method}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";
        string stringToSign = $"{Algorithm}\n{timeStamp}\n{scope}\n{BytesToHex(ComputeSHA256(canonicalRequest))}";

        byte[] dateKey = ComputeHMACSHA256(dateStamp, "AWS4" + secretAccessKey);
        byte[] dateRegionKey = ComputeHMACSHA256(region, dateKey);
        byte[] dateRegionServiceKey = ComputeHMACSHA256("s3", dateRegionKey);
        byte[] signingKey = ComputeHMACSHA256("aws4_request", dateRegionServiceKey);

        string signature = BytesToHex(ComputeHMACSHA256(stringToSign, signingKey));

        headers["Authorization"] = $"{Algorithm} Credential={accessKeyId}/{scope},SignedHeaders={signedHeaders},Signature={signature}";
    }

    private static string CreateCanonicalHeaders(NameValueCollection headers)
    {
        var sorted = headers.AllKeys
            .Where(k => k != null)
            .OrderBy(k => k)
            .Select(k => $"{k!.ToLowerInvariant()}:{headers[k]?.Trim()}\n");
        return string.Join("", sorted);
    }

    private static string GetSignedHeaders(NameValueCollection headers)
    {
        return string.Join(";", headers.AllKeys
            .Where(k => k != null)
            .OrderBy(k => k)
            .Select(k => k!.ToLowerInvariant()));
    }

    private static byte[] ComputeSHA256(string text)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(text));
    }

    private static byte[] ComputeHMACSHA256(string text, string key)
    {
        return ComputeHMACSHA256(text, Encoding.UTF8.GetBytes(key));
    }

    private static byte[] ComputeHMACSHA256(string text, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
    }

    private static string BytesToHex(byte[] bytes)
    {
        return Convert.ToHexStringLower(bytes);
    }
}
