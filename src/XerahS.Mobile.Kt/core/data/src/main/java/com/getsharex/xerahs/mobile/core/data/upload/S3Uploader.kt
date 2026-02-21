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
            val key = "uploads/${file.name}"
            val request = PutObjectRequest(config.bucketName, key, file)
                .withCannedAcl(CannedAccessControlList.PublicRead)
            s3.putObject(request)
            val url = if (config.customEndpoint.isNotBlank()) {
                "${config.customEndpoint.trimEnd('/')}/$key"
            } else {
                "https://${config.bucketName}.s3.${config.region}.amazonaws.com/$key"
            }
            UploadOutcome.Success(url)
        } catch (e: Exception) {
            UploadOutcome.Failure(e.message ?: "S3 upload failed")
        }
    }
}
