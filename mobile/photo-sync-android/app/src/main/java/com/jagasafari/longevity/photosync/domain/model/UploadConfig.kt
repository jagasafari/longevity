package com.jagasafari.longevity.photosync.domain.model

data class UploadConfig(
    val storageAccount: String,
    val container: String,
    val sasToken: String
) {
    /**
     * Normalizes the SAS token to ensure it starts with a single '?'
     */
    val normalizedSasToken: String
        get() = if (sasToken.startsWith("?")) sasToken else "?$sasToken"

    /**
     * Base URL for the Azure Blob Storage container
     */
    val saUrl: String
        get() = "https://$storageAccount.blob.core.windows.net/$container"
}
