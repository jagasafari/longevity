module PhotoCounter

open System
open Azure.Identity
open Azure.Storage.Blobs
open Dapper
open Npgsql

type Config =
    { AccountName: string
      ContainerName: string
      ConnectionString: string }

let private createBlobClient (config: Config) =
    let cred = DefaultAzureCredential()
    let uri = Uri $"https://{config.AccountName}.blob.core.windows.net"
    BlobServiceClient(uri, cred)

let private listBlobDates (container: BlobContainerClient) =
    System.Threading.Tasks.Task.Run(fun () ->
        let counts = System.Collections.Generic.Dictionary<DateOnly, int>()
        for blob in container.GetBlobs() do
            let modified =
                blob.Properties.LastModified
                |> Option.ofNullable
                |> Option.defaultValue DateTimeOffset.MinValue
            let date = DateOnly.FromDateTime(modified.Date)
            counts[date] <- (match counts.TryGetValue date with | true, v -> v | _ -> 0) + 1
        counts |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList)

let private upsertCounts (connStr: string) (counts: (DateOnly * int) list) = task {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync()
    use! tx = conn.BeginTransactionAsync()

    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM photo_counts",
            transaction = tx)

    for (date, count) in counts do
        let! _ =
            conn.ExecuteAsync(
                """INSERT INTO photo_counts (date, count) VALUES (@d, @c)""",
                {| d = date.ToDateTime(TimeOnly.MinValue); c = count |},
                tx)
        ()

    do! tx.CommitAsync()
}

let computeAndStore (config: Config) = task {
    let blobService = createBlobClient config
    let container = blobService.GetBlobContainerClient(config.ContainerName)
    let! counts = listBlobDates container
    do! upsertCounts config.ConnectionString counts
    return counts.Length
}
