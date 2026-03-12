module ThumbnailProcessor

open System
open System.IO
open System.Text.Json
open Azure.Identity
open Azure.Storage.Blobs
open Azure.Storage.Blobs.Models
open Azure.Storage.Queues
open Azure.Storage.Queues.Models
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing

type ProcessorConfig =
    { AccountName: string
      SourceContainer: string
      ThumbnailContainer: string
      QueueName: string
      MaxWidth: int }

let createClients (config: ProcessorConfig) =
    let cred = DefaultAzureCredential()
    let blobUri = Uri $"https://{config.AccountName}.blob.core.windows.net"
    let blobService = BlobServiceClient(blobUri, cred)
    let queueUri = Uri $"https://{config.AccountName}.queue.core.windows.net/{config.QueueName}"
    let queueOpts = QueueClientOptions(MessageEncoding = QueueMessageEncoding.Base64)
    let queue = QueueClient(queueUri, cred, queueOpts)
    blobService, queue

let private listBlobNames (container: BlobContainerClient) = task {
    let names = ResizeArray<string>()
    use enumerator = container.GetBlobsAsync().GetAsyncEnumerator()
    let mutable hasNext = true
    while hasNext do
        let! moved = enumerator.MoveNextAsync().AsTask()
        if moved then names.Add enumerator.Current.Name
        else hasNext <- false
    return Set.ofSeq names
}

let private isImage (name: string) =
    let ext = Path.GetExtension(name).ToLowerInvariant()
    ext = ".jpg" || ext = ".jpeg" || ext = ".png" || ext = ".gif" || ext = ".webp" || ext = ".bmp"

let private resizeAndUpload (source: BlobContainerClient) (target: BlobContainerClient) maxWidth blobName = task {
    let sourceBlob = source.GetBlobClient blobName
    use! downloadStream = sourceBlob.OpenReadAsync()
    use image = Image.Load downloadStream

    if image.Width > maxWidth then
        let ratio = float maxWidth / float image.Width
        let newHeight = int (float image.Height * ratio)
        image.Mutate(fun ctx -> ctx.Resize(maxWidth, newHeight) |> ignore)

    use output = new MemoryStream()
    do! image.SaveAsJpegAsync output
    output.Position <- 0L

    let targetBlob = target.GetBlobClient blobName
    let headers = BlobHttpHeaders(ContentType = "image/jpeg")
    let! _ = targetBlob.UploadAsync(output, httpHeaders = headers, overwrite = true)
    ()
}

// Parse blob name from Event Grid subject:
// /blobServices/default/containers/{container}/blobs/{name}
let private parseBlobName (sourceContainer: string) (subject: string) =
    let prefix = $"/blobServices/default/containers/{sourceContainer}/blobs/"
    if subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
        let name = subject[prefix.Length..]
        if name.Length > 0 then Some name else None
    else
        None

let extractBlobName (sourceContainer: string) (messageBody: string) =
    try
        use doc = JsonDocument.Parse messageBody
        let root = doc.RootElement
        let evt = if root.ValueKind = JsonValueKind.Array then root[0] else root
        let subject = evt.GetProperty("subject").GetString()
        parseBlobName sourceContainer subject
    with _ -> None

// On startup: generate thumbnails for any photos that don't have one yet
let catchUpThumbnails (blobService: BlobServiceClient) (config: ProcessorConfig) = task {
    let source = blobService.GetBlobContainerClient config.SourceContainer
    let target = blobService.GetBlobContainerClient config.ThumbnailContainer
    let! _ = target.CreateIfNotExistsAsync()

    let! sourceNames = listBlobNames source
    let! thumbNames = listBlobNames target

    let missing =
        sourceNames - thumbNames
        |> Set.filter (fun n -> n <> "photo-groups.json" && isImage n)

    let mutable count = 0
    for name in missing do
        try
            do! resizeAndUpload source target config.MaxWidth name
            count <- count + 1
        with _ -> ()

    return count
}

// Process a single blob triggered by a queue event
let processBlobThumbnail (blobService: BlobServiceClient) (config: ProcessorConfig) blobName = task {
    if isImage blobName && blobName <> "photo-groups.json" then
        let source = blobService.GetBlobContainerClient config.SourceContainer
        let target = blobService.GetBlobContainerClient config.ThumbnailContainer
        let! _ = target.CreateIfNotExistsAsync()
        do! resizeAndUpload source target config.MaxWidth blobName
        return true
    else
        return false
}
