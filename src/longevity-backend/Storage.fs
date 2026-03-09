module Storage

open System
open Azure.Identity
open Azure.Storage.Blobs
open Azure.Storage.Sas

type StorageConfig =
    { AccountName: string
      ContainerName: string }

type PhotoInfo =
    { Name: string
      Url: string
      LastModified: DateTimeOffset }

let private lastModified (b: Azure.Storage.Blobs.Models.BlobItem) =
    b.Properties.LastModified
    |> Option.ofNullable
    |> Option.defaultValue DateTimeOffset.MinValue

let private buildSasUrl (service: BlobServiceClient) containerName expiry blobName =
    let key = service.GetUserDelegationKey(Nullable(), expiry)
    let builder =
        BlobSasBuilder(
            BlobSasPermissions.Read,
            expiry,
            BlobContainerName = containerName,
            BlobName = blobName)
    let client = service.GetBlobContainerClient(containerName).GetBlobClient(blobName)
    let uriBuilder = BlobUriBuilder(client.Uri)
    uriBuilder.Sas <- builder.ToSasQueryParameters(key.Value, service.AccountName)
    string (uriBuilder.ToUri())

let selectRecent (toUrl: string -> string) (blobs: seq<string * DateTimeOffset>) (count: int) =
    blobs
    |> Seq.sortByDescending snd
    |> Seq.truncate count
    |> Seq.map (fun (name, modified) ->
        { Name = name
          Url = toUrl name
          LastModified = modified })
    |> Seq.toArray

let listRecentPhotos (config: StorageConfig) (count: int) =
    let serviceUri = Uri $"https://{config.AccountName}.blob.core.windows.net"
    let service = BlobServiceClient(serviceUri, DefaultAzureCredential())
    let container = service.GetBlobContainerClient config.ContainerName
    let expiry = DateTimeOffset.UtcNow.AddHours 1.0

    let blobs =
        container.GetBlobs()
        |> Seq.map (fun b -> b.Name, lastModified b)

    let toUrl = buildSasUrl service config.ContainerName expiry

    selectRecent toUrl blobs count
