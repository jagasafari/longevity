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

type PhotoPage = { Items: PhotoInfo[]; NextBefore: string option }
let private lastModified (b: Azure.Storage.Blobs.Models.BlobItem) =
    b.Properties.LastModified
    |> Option.ofNullable
    |> Option.defaultValue DateTimeOffset.MinValue

let private buildSasUrl (service: BlobServiceClient) delegationKey containerName expiry blobName =
    let sasBuilder =
        BlobSasBuilder(
            BlobSasPermissions.Read,
            expiry,
            BlobContainerName = containerName,
            BlobName = blobName)
    let sasParams = sasBuilder.ToSasQueryParameters(delegationKey, service.AccountName)
    let encodedName = Uri.EscapeDataString(blobName)
    $"https://{service.AccountName}.blob.core.windows.net/{containerName}/{encodedName}?{sasParams}"

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

let private selectPhotoPage
    (toUrl: string -> string)
    (toThumbnailUrl: string -> string)
    (blobs: seq<string * DateTimeOffset>)
    (limit: int) =
    let page =
        blobs
        |> Seq.sortByDescending snd
        |> Seq.truncate (limit + 1)
        |> Seq.toList
    let hasMore   = page.Length > limit
    let items     = page |> List.truncate limit
    let nextBefore =
        if hasMore then items |> List.last |> snd |> (fun dt -> dt.ToString("O")) |> Some
        else None
    let mapped =
        items
        |> List.map (fun (name, modified) ->
            { Name = name; Url = toUrl name; ThumbnailUrl = toThumbnailUrl name; LastModified = modified })
        |> List.toArray
    { Items = mapped; NextBefore = nextBefore }

let private listBlobsAsync
    (container: BlobContainerClient)
    (prefix: string option)
    (predicate: string * DateTimeOffset -> bool) = task {
    let blobs = ResizeArray<string * DateTimeOffset>()
    let blobsEnum =
        match prefix with
        | Some p -> container.GetBlobsAsync(BlobTraits.None, BlobStates.None, p, System.Threading.CancellationToken.None)
        | None   -> container.GetBlobsAsync()
    use enumerator = blobsEnum.GetAsyncEnumerator()
    let mutable hasNext = true
    while hasNext do
        let! moved = enumerator.MoveNextAsync().AsTask()
        if moved then
            let b = enumerator.Current
            let item = b.Name, lastModified b
            if predicate item then blobs.Add item
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

let private buildUrlFunctions (service: BlobServiceClient) (config: StorageConfig) = task {
    let expiry = DateTimeOffset.UtcNow.AddHours 1.0
    let! dkResp = service.GetUserDelegationKeyAsync(Nullable(), expiry)
    let delegationKey = dkResp.Value
    let thumbnailContainer = service.GetBlobContainerClient thumbnailContainerName
    let! thumbnailNames =
        task {
            try
                let! blobs = listBlobsAsync thumbnailContainer None (fun _ -> true)
                return blobs |> Seq.map fst |> Set.ofSeq
            with _ -> return Set.empty
        }
    let toUrl = buildSasUrl service delegationKey config.ContainerName expiry
    let toThumbnailUrl name =
        if Set.contains name thumbnailNames then
            buildSasUrl service delegationKey thumbnailContainerName expiry name
        else toUrl name
    return toUrl, toThumbnailUrl
}

let listPhotoPage (config: StorageConfig) (limit: int) (dateFilter: DateOnly option) (before: DateTimeOffset option) (excludeNames: Set<string>) = task {
    let service, container = getClients config
    let! toUrl, toThumbnailUrl = buildUrlFunctions service config
    let predicate (name, dt: DateTimeOffset) =
        not (Set.contains name excludeNames)
        && dateFilter |> Option.forall (fun d -> DateOnly.FromDateTime(dt.Date) = d)
        && before  |> Option.forall (fun b -> dt < b)
    let! blobs = listBlobsAsync container None predicate
    return selectPhotoPage toUrl toThumbnailUrl blobs limit
}

let getPhotoSasUrls (config: StorageConfig) (names: string list) : System.Threading.Tasks.Task<Map<string, string * string>> = task {
    let service, _ = getClients config
    let! toUrl, toThumbnailUrl = buildUrlFunctions service config
    return names |> List.map (fun n -> n, (toUrl n, toThumbnailUrl n)) |> Map.ofList
}
