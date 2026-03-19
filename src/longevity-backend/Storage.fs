module Storage

open System
open System.Threading.Tasks
open Azure.Identity
open Azure.Storage.Blobs
open Azure.Storage.Sas
open Azure.Storage.Blobs.Models

type StorageConfig = { AccountName: string; ContainerName: string }

type PhotoInfo =
    { Name: string
      Url: string
      ThumbnailUrl: string
      LastModified: DateTimeOffset }
let private lastModified (b: Azure.Storage.Blobs.Models.BlobItem) =
    b.Properties.LastModified
    |> Option.ofNullable
    |> Option.defaultValue DateTimeOffset.MinValue

let private buildSasUrl (service: BlobServiceClient) delegationKey containerName expiry blobName =
    let builder =
        BlobSasBuilder(
            BlobSasPermissions.Read,
            expiry,
            BlobContainerName = containerName,
            BlobName = blobName)
    let client = service.GetBlobContainerClient(containerName).GetBlobClient(blobName)
    let uriBuilder = BlobUriBuilder(client.Uri)
    uriBuilder.Sas <- builder.ToSasQueryParameters(delegationKey, service.AccountName)
    string (uriBuilder.ToUri())

let selectRecent (toUrl: string -> string) (toThumbnailUrl: string -> string) (blobs: seq<string * DateTimeOffset>) (count: int) =
    let insert top item =
        item :: top
        |> List.sortByDescending snd
        |> List.truncate count

    blobs
    |> Seq.fold insert []
    |> List.map (fun (name, modified) ->
        { Name = name; Url = toUrl name; ThumbnailUrl = toThumbnailUrl name; LastModified = modified })
    |> List.toArray

let private listBlobsAsync (container: BlobContainerClient) = task {
    let blobs = ResizeArray<string * DateTimeOffset>()
    use enumerator = container.GetBlobsAsync().GetAsyncEnumerator()
    let mutable hasNext = true
    while hasNext do
        let! moved = enumerator.MoveNextAsync().AsTask()
        if moved then
            let b = enumerator.Current
            blobs.Add (b.Name, lastModified b)
        else
            hasNext <- false

    return blobs :> seq<string * DateTimeOffset>
}

let private getClients (config: StorageConfig) =
    let serviceUri = Uri $"https://{config.AccountName}.blob.core.windows.net"
    let service = BlobServiceClient(serviceUri, DefaultAzureCredential())
    let container = service.GetBlobContainerClient config.ContainerName
    service, container

let deletePhoto (config: StorageConfig) (blobName: string) = task {
    let _, container = getClients config
    let! response =
        container.DeleteBlobIfExistsAsync(
            blobName,
            DeleteSnapshotsOption.IncludeSnapshots)
    return response.Value
}

let private thumbnailContainerName = "thumbnails"

let listRecentPhotos (config: StorageConfig) (count: int) = task {
    let service, container = getClients config
    let thumbnailContainer = service.GetBlobContainerClient thumbnailContainerName
    let expiry = DateTimeOffset.UtcNow.AddHours 1.0
    let! delegationKeyResponse = service.GetUserDelegationKeyAsync(Nullable(), expiry)
    let delegationKey = delegationKeyResponse.Value
    let! blobs = listBlobsAsync container

    let! thumbnailNames =
        task {
            try
                let! thumbBlobs = listBlobsAsync thumbnailContainer
                return thumbBlobs |> Seq.map fst |> Set.ofSeq
            with _ -> return Set.empty
        }

    let toUrl = buildSasUrl service delegationKey config.ContainerName expiry

    let toThumbnailUrl name =
        if Set.contains name thumbnailNames then
            buildSasUrl service delegationKey thumbnailContainerName expiry name
        else
            toUrl name

    return selectRecent toUrl toThumbnailUrl blobs count
}
