module PhotoCountCache

open System
open System.Threading
open System.Threading.Tasks
open Azure.Identity
open Azure.Storage.Blobs
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Dapper
open Npgsql

type PhotoCount = { date: DateOnly; count: int }

type Cache() =
    let mutable data: PhotoCount[] = [||]
    member _.Get() = Volatile.Read(&data)
    member _.Set v = Volatile.Write(&data, v)

let private vocabularyPhotoNames (connStr: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! names = conn.QueryAsync<string>("SELECT photo_name FROM vocabulary.photos")
    return names |> Set.ofSeq
}

let private computeCounts (container: BlobContainerClient) (excluded: Set<string>) =
    Task.Run(fun () ->
        container.GetBlobs()
        |> Seq.filter (fun b -> not (excluded.Contains b.Name))
        |> Seq.choose (fun b ->
            b.Properties.LastModified
            |> Option.ofNullable
            |> Option.map (fun dt -> DateOnly.FromDateTime(dt.Date)))
        |> Seq.countBy id
        |> Seq.map (fun (date, count) -> { date = date; count = count })
        |> Seq.sortBy (fun c -> c.date)
        |> Seq.toArray)

type RefreshService(logger: ILogger<RefreshService>, cache: Cache, config: Storage.StorageConfig, connStr: string) =
    inherit BackgroundService()

    let container =
        let uri = Uri $"https://{config.AccountName}.blob.core.windows.net"
        BlobServiceClient(uri, DefaultAzureCredential())
            .GetBlobContainerClient(config.ContainerName)

    override _.ExecuteAsync(ct) = task {
        let refresh () = task {
            try
                let! excluded = vocabularyPhotoNames connStr
                let! counts = computeCounts container excluded
                cache.Set counts
                logger.LogInformation("Photo counts refreshed: {Count} dates", counts.Length)
            with ex ->
                logger.LogError(ex, "Failed to refresh photo counts")
        }
        do! refresh ()
        while not ct.IsCancellationRequested do
            do! Task.Delay(TimeSpan.FromHours 1.0, ct)
            do! refresh ()
    }

let list (cache: Cache) () = cache.Get()
