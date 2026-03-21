open System
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open StackExchange.Redis

type ThumbnailWorker(logger: ILogger<ThumbnailWorker>, config: ThumbnailProcessor.ProcessorConfig, redis: IConnectionMultiplexer) =
    inherit BackgroundService()

    let blobService, queue = ThumbnailProcessor.createClients config
    let subscriber = redis.GetSubscriber()

    let publishThumbnailReady blobName = task {
        logger.LogInformation("Publishing thumbnail-ready for {BlobName} to Redis", [| blobName :> obj |])
        try
            let! receivers = subscriber.PublishAsync(RedisChannel("thumbnail-ready", RedisChannel.PatternMode.Literal), RedisValue blobName)
            logger.LogInformation("Published to {ReceiverCount} subscribers", [| receivers :> obj |])
        with ex ->
            logger.LogError(ex, "Failed to publish to Redis")
    }

    override _.ExecuteAsync(cancellationToken) = task {
        logger.LogInformation("Thumbnail worker starting, Redis connected: {IsConnected}", [| redis.IsConnected :> obj |])

        try
            let! count = ThumbnailProcessor.catchUpThumbnails blobService config
            if count > 0 then
                logger.LogInformation("Catch-up: processed {Count} existing photos", count)
                do! publishThumbnailReady "catch-up"
        with ex ->
            logger.LogWarning(ex, "Catch-up scan failed")

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
                                        do! publishThumbnailReady blobName
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

    let redisConn =
        builder.Configuration["Redis:ConnectionString"]
        |> Option.ofObj
        |> Option.defaultValue "redis-svc:6379"

    let config: ThumbnailProcessor.ProcessorConfig =
                {
                    AccountName = require "AccountName"
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
                    MaxWidth = 400
                }

    builder.Services.AddSingleton(config) |> ignore
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect redisConn) |> ignore
    builder.Services.AddHostedService<ThumbnailWorker>() |> ignore
    builder.Build().Run()
    0
