module PhotoCountCache

open System
open System.Threading
open System.Threading.Tasks
open Azure.Identity
open Azure.Storage.Blobs
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PhotoCount = { date: DateOnly; count: int }

type Cache() =
    let mutable data: PhotoCount[] = [||]
    member _.Get() = Volatile.Read(&data)
    member _.Set v = Volatile.Write(&data, v)

let private computeCounts (container: BlobContainerClient) =
    Task.Run(fun () ->
        let counts = System.Collections.Generic.Dictionary<DateOnly, int>()
        for blob in container.GetBlobs() do
            match blob.Properties.LastModified |> Option.ofNullable with
            | None -> ()
            | Some modified ->
                let date = DateOnly.FromDateTime(modified.Date)
                counts[date] <- (match counts.TryGetValue date with | true, v -> v | _ -> 0) + 1
        counts
        |> Seq.map (fun kv -> { date = kv.Key; count = kv.Value })
        |> Seq.sortBy (fun c -> c.date)
        |> Seq.toArray)

type RefreshService(logger: ILogger<RefreshService>, cache: Cache, config: Storage.StorageConfig) =
    inherit BackgroundService()

    let container =
        let uri = Uri $"https://{config.AccountName}.blob.core.windows.net"
        BlobServiceClient(uri, DefaultAzureCredential())
            .GetBlobContainerClient(config.ContainerName)

    override _.ExecuteAsync(ct) = task {
        while not ct.IsCancellationRequested do
            try
                let! counts = computeCounts container
                cache.Set counts
                logger.LogInformation("Photo counts refreshed: {Count} dates", counts.Length)
            with ex ->
                logger.LogError(ex, "Failed to refresh photo counts")
            do! Task.Delay(TimeSpan.FromMinutes 5.0, ct)
    }

let list (cache: Cache) () = cache.Get()
