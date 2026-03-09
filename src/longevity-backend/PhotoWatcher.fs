namespace PhotoWatcher

open System
open System.Threading
open System.Threading.Tasks
open Azure.Identity
open Azure.Storage.Blobs
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open FSharp.Control

type PhotoWatcherService(hub: IHubContext<PhotoHub.PhotoHub>, config: Storage.StorageConfig, logger: ILogger<PhotoWatcherService>) =
    inherit BackgroundService()

    let listBlobNamesAsync (container: BlobContainerClient) = task {
        let! names =
            container.GetBlobsAsync()
            |> AsyncSeq.map (fun blob -> blob.Name)
            |> AsyncSeq.toListAsync
        return Set.ofList names
    }

    let pollOnce (container: BlobContainerClient)
        (cancellationToken: CancellationToken)
        (lastKnownNames: Set<string>) = task {
        try
            let! currentNames = listBlobNamesAsync container
            if currentNames <> lastKnownNames then
                do! hub.Clients.All.SendAsync("PhotosChanged", cancellationToken)
                return currentNames
            else
                return lastKnownNames
        with ex ->
            logger.LogWarning(ex, "PhotoWatcher poll failed")
            return lastKnownNames
    }

    let rec runLoop (timer: PeriodicTimer)
        (container: BlobContainerClient)
        (cancellationToken: CancellationToken)
        (lastKnownNames: Set<string>) = task {
        let! tick = timer.WaitForNextTickAsync(cancellationToken).AsTask()
        if tick then
            let! nextKnownNames = pollOnce container cancellationToken lastKnownNames
            return! runLoop timer container cancellationToken nextKnownNames
        else
            return ()
    }

    override _.ExecuteAsync(cancellationToken: CancellationToken) = task {
        let serviceUri = Uri $"https://{config.AccountName}.blob.core.windows.net"
        let service = BlobServiceClient(serviceUri, DefaultAzureCredential())
        let container = service.GetBlobContainerClient config.ContainerName

        try
            use timer = new PeriodicTimer(TimeSpan.FromSeconds 5.0)
            do! runLoop timer container cancellationToken Set.empty
        with :? OperationCanceledException -> ()
    }
