package com.getsharex.xerahs.mobile.core.domain

/**
 * Minimal app config for mobile: default destination, S3, and custom uploaders.
 * Persisted as JSON in settings folder (ApplicationConfig.json).
 */
data class ApplicationConfig(
    var defaultDestinationInstanceId: String? = null,
    var s3Config: S3Config = S3Config(),
    var customUploaders: List<CustomUploaderEntry> = emptyList()
)

data class S3Config(
    var accessKeyId: String = "",
    var secretAccessKey: String = "",
    var bucketName: String = "",
    var region: String = "",
    var customEndpoint: String = "",
    var usePathStyle: Boolean = false
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
