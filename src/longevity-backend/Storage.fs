module Storage

open System
open System.Threading.Tasks
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

let selectRecent (toUrl: string -> string) (blobs: seq<string * DateTimeOffset>) (count: int) =
    let insert top item =
        item :: top
        |> List.sortByDescending snd
        |> List.truncate count

    blobs
    |> Seq.fold insert []
    |> List.map (fun (name, modified) ->
        { Name = name; Url = toUrl name; LastModified = modified })
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

let listRecentPhotos (config: StorageConfig) (count: int) = task {
    let serviceUri = Uri $"https://{config.AccountName}.blob.core.windows.net"
    let service = BlobServiceClient(serviceUri, DefaultAzureCredential())
    let container = service.GetBlobContainerClient config.ContainerName
    let expiry = DateTimeOffset.UtcNow.AddHours 1.0
    let! delegationKeyResponse = service.GetUserDelegationKeyAsync(Nullable(), expiry)
    let delegationKey = delegationKeyResponse.Value
    let! blobs = listBlobsAsync container

    let toUrl = buildSasUrl service delegationKey config.ContainerName expiry

    return selectRecent toUrl blobs count
}
