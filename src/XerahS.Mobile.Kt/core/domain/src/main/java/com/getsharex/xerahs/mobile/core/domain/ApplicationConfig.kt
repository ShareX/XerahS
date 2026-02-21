/*
 * XerahS - The Avalonia UI implementation of ShareX
 * Copyright (c) 2007-2026 ShareX Team
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 *
 * Optionally you can also view the license at <http://www.gnu.org/licenses/>.
 */

package com.getsharex.xerahs.mobile.core.domain

/**
 * Minimal app config for mobile: default destination, S3, and custom uploaders.
 * Persisted as JSON in settings folder (ApplicationConfig.json).
 */
data class ApplicationConfig(
    var defaultDestinationInstanceId: String? = null,
    var s3Config: S3Config = S3Config(),
    var customUploaders: List<CustomUploaderEntry> = emptyList(),
    /** Convert HEIC/HEIF images to PNG before upload so they display in browsers instead of prompting download. */
    var convertHeicToPng: Boolean = true
)

data class S3Config(
    var accessKeyId: String = "",
    var secretAccessKey: String = "",
    var bucketName: String = "",
    var region: String = "",
    /** Override S3 API endpoint (e.g. for MinIO or other S3-compatible storage). Leave blank for AWS. */
    var customEndpoint: String = "",
    var usePathStyle: Boolean = false,
    /** Use custom domain (CDN) for result URLs instead of bucket.s3.region.amazonaws.com */
    var useCustomDomain: Boolean = false,
    var customDomain: String = "",
    /** Sign request body; recommended when bucket blocks public ACLs. Default true. */
    var signedPayload: Boolean = true,
    /** Set public-read ACL on uploaded objects. Default false. */
    var setPublicAcl: Boolean = false
) {
    val isConfigured: Boolean
        get() = accessKeyId.isNotBlank() && secretAccessKey.isNotBlank() && bucketName.isNotBlank() && region.isNotBlank()
}

/**
 * One custom uploader (.sxcu-style). Persisted in config or as separate files.
 */
data class CustomUploaderEntry(
    var id: String = "",
    var name: String = "",
    var requestUrl: String = "",
    var body: String = "",
    var headers: Map<String, String> = emptyMap(),
    var fileFormName: String = "file",
    var urlExpression: String = ""  // regex or template to extract URL from response
)
