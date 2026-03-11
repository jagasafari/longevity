module PhotoGroups

open System
open System.Text.Json
open System.Threading.Tasks
open Azure.Identity
open Azure.Storage.Blobs

type ContainerRef = { AccountName: string; ContainerName: string }

let groupsBlobName = "photo-groups.json"

let private getContainerClient (ref: ContainerRef) =
    let serviceUri =
        Uri $"https://{ref.AccountName}.blob.core.windows.net"
    let service = BlobServiceClient(serviceUri, DefaultAzureCredential())
    service.GetBlobContainerClient ref.ContainerName

let private parseGroups (json: string) =
    try
        JsonSerializer.Deserialize<Map<string, string> option>(json)
        |> Option.defaultValue Map.empty
    with
    | _ -> Map.empty

let private readGroups (container: BlobContainerClient) = task {
    let blob = container.GetBlobClient groupsBlobName
    let! exists = blob.ExistsAsync()
    if not exists.Value then
        return Map.empty
    else
        let! download = blob.DownloadContentAsync()
        return parseGroups (download.Value.Content.ToString())
}

let private writeGroups (container: BlobContainerClient) (groups: Map<string, string>) = task {
    let blob = container.GetBlobClient groupsBlobName
    let json = JsonSerializer.Serialize(groups)
    let! _ = blob.UploadAsync(BinaryData.FromString(json), true)
    return ()
}

let private regroup sourceName targetName groups =
    let sourceGroup = Map.tryFind sourceName groups
    let targetGroup = Map.tryFind targetName groups

    match sourceGroup, targetGroup with
    | Some sg, Some tg when sg = tg -> groups
    | Some sg, Some tg ->
        groups
        |> Map.map (fun _ groupId -> if groupId = sg then tg else groupId)
        |> Map.add sourceName tg
        |> Map.add targetName tg
    | Some sg, None -> groups |> Map.add targetName sg
    | None, Some tg -> groups |> Map.add sourceName tg
    | None, None ->
        let groupId = Guid.NewGuid().ToString("N")
        groups
        |> Map.add sourceName groupId
        |> Map.add targetName groupId

let removePhotoFromGroups (ref: ContainerRef) (blobName: string) = task {
    let container = getContainerClient ref
    let! groups = readGroups container
    let updated = groups |> Map.remove blobName
    if updated <> groups then
        do! writeGroups container updated
}

let listPhotoGroups (ref: ContainerRef) () = task {
    let container = getContainerClient ref
    return! readGroups container
}

let groupPhotos
    (ref: ContainerRef)
    (sourceName: string)
    (targetName: string) = task {
    let container = getContainerClient ref
    let! groups = readGroups container
    let updated = regroup sourceName targetName groups
    do! writeGroups container updated
}
