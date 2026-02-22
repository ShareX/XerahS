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

package com.getsharex.xerahs.mobile.core.data.upload

import com.amazonaws.auth.BasicAWSCredentials
import com.amazonaws.regions.Region
import com.amazonaws.regions.Regions
import com.amazonaws.services.s3.AmazonS3Client
import com.amazonaws.services.s3.model.CannedAccessControlList
import com.amazonaws.services.s3.model.PutObjectRequest
import com.getsharex.xerahs.mobile.core.domain.S3Config
import java.io.File

/**
 * Upload a file to S3 using AWS SDK. Returns the public or object URL on success.
 */
class S3Uploader {

    fun uploadFile(filePath: String, config: S3Config): UploadOutcome {
        if (!config.isConfigured) return UploadOutcome.Failure("S3 is not configured")
        val file = File(filePath)
        if (!file.exists()) return UploadOutcome.Failure("File not found")
        return try {
            val credentials = BasicAWSCredentials(config.accessKeyId, config.secretAccessKey)
            val region = try {
                Region.getRegion(Regions.fromName(config.region))
            } catch (e: Exception) {
                return UploadOutcome.Failure("Invalid region: ${config.region}")
            }
            val s3 = AmazonS3Client(credentials).apply { setRegion(region) }
            val uploadName = UploadFileNameGenerator.uploadFileName(filePath)
            val key = "uploads/$uploadName"
            val request = PutObjectRequest(config.bucketName, key, file).apply {
                if (config.setPublicAcl) withCannedAcl(CannedAccessControlList.PublicRead)
            }
            s3.putObject(request)
            val url = when {
                config.useCustomDomain && config.customDomain.isNotBlank() ->
                    "${config.customDomain.trim().trimEnd('/')}/$key"
                config.customEndpoint.isNotBlank() ->
                    "${config.customEndpoint.trim().trimEnd('/')}/$key"
                else ->
                    "https://${config.bucketName}.s3.${config.region}.amazonaws.com/$key"
            }
            UploadOutcome.Success(url)
        } catch (e: Exception) {
            UploadOutcome.Failure(e.message ?: "S3 upload failed")
        }
    }
}
