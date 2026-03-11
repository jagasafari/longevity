open System
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type ThumbnailWorker(logger: ILogger<ThumbnailWorker>, config: ThumbnailProcessor.ProcessorConfig) =
    inherit BackgroundService()

    let blobService, queue = ThumbnailProcessor.createClients config

    override _.ExecuteAsync(cancellationToken) = task {
        logger.LogInformation "Thumbnail worker starting"

        // Catch up on photos uploaded before the worker was running
        try
            let! count = ThumbnailProcessor.catchUpThumbnails blobService config
            if count > 0 then
                logger.LogInformation("Catch-up: processed {Count} existing photos", count)
        with ex ->
            logger.LogWarning(ex, "Catch-up scan failed")

        // Process new uploads via Event Grid events on the queue
        try
            while not cancellationToken.IsCancellationRequested do
                try
                    let! response = queue.ReceiveMessagesAsync(
                        maxMessages = 10,
                        cancellationToken = cancellationToken)
                    let messages = response.Value

                    if messages.Length = 0 then
                        do! Task.Delay(TimeSpan.FromSeconds 5.0, cancellationToken)
                    else
                        for msg in messages do
                            try
                                let body = msg.Body.ToString()
                                match ThumbnailProcessor.extractBlobName config.SourceContainer body with
                                | None -> ()
                                | Some blobName ->
                                    let! generated = ThumbnailProcessor.processBlobThumbnail blobService config blobName
                                    if generated then
                                        logger.LogInformation("Thumbnail created for {BlobName}", blobName)
                                do! queue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, cancellationToken)
                                   :> System.Threading.Tasks.Task
                            with ex ->
                                logger.LogWarning(ex, "Failed to process message {MessageId}", msg.MessageId)
                with
                | :? OperationCanceledException -> ()
                | ex -> logger.LogWarning(ex, "Queue receive failed")
        with :? OperationCanceledException -> ()
    }

[<EntryPoint>]
let main args =
    let builder = Host.CreateApplicationBuilder args
    let s = builder.Configuration.GetSection "Storage"

    let require key =
        s[key]
        |> Option.ofObj
        |> Option.filter (fun v -> v.Length > 0)
        |> Option.defaultWith (fun () -> failwith $"Missing config: Storage:{key}")

    let config: ThumbnailProcessor.ProcessorConfig =
        { AccountName = require "AccountName"
          SourceContainer =
            s["ContainerName"]
            |> Option.ofObj
            |> Option.defaultValue "photos"
          ThumbnailContainer =
            s["ThumbnailContainerName"]
            |> Option.ofObj
            |> Option.defaultValue "thumbnails"
          QueueName =
            s["QueueName"]
            |> Option.ofObj
            |> Option.defaultValue "thumbnail-events"
          MaxWidth = 400 }

    builder.Services.AddSingleton(config) |> ignore
    builder.Services.AddHostedService<ThumbnailWorker>() |> ignore
    builder.Build().Run()
    0
