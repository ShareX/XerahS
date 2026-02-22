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

/** Stable id for the built-in S3 destination. Used as defaultDestinationInstanceId when user selects Amazon S3. */
const val AMAZON_S3_DESTINATION_ID = "amazons3"

/** Human-readable label for the currently selected upload destination, or null if none configured/selected. */
fun ApplicationConfig.activeDestinationDisplayName(): String? {
    val id = defaultDestinationInstanceId
    if (id == AMAZON_S3_DESTINATION_ID || (id?.startsWith("amazons3") == true)) {
        return if (s3Config.isConfigured) "Amazon S3" else null
    }
    if (id != null) {
        val custom = customUploaders.firstOrNull { it.id == id }
        if (custom != null) return if (custom.name.isNotBlank()) custom.name else custom.id
    }
    if (s3Config.isConfigured) return "Amazon S3"
    val first = customUploaders.firstOrNull() ?: return null
    return if (first.name.isNotBlank()) first.name else first.id
}

/** All selectable destinations: (displayName, instanceId). Order: S3 first (if configured), then custom uploaders. */
fun ApplicationConfig.selectableDestinations(): List<Pair<String, String>> {
    val list = mutableListOf<Pair<String, String>>()
    if (s3Config.isConfigured) list.add("Amazon S3" to AMAZON_S3_DESTINATION_ID)
    customUploaders.filter { it.id.isNotBlank() }.forEach { entry ->
        list.add((if (entry.name.isNotBlank()) entry.name else entry.id) to entry.id)
    }
    return list
}
