namespace ThumbnailSubscriber

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open StackExchange.Redis

module private Cancellation =
    let awaitToken (ct: CancellationToken) =
        let tcs = TaskCompletionSource()
        ct.Register(fun () -> tcs.TrySetResult() |> ignore)
        |> ignore
        tcs.Task

type ThumbnailSubscriberService(hub: IHubContext<PhotoHub.PhotoHub>, redis: IConnectionMultiplexer, logger: ILogger<ThumbnailSubscriberService>) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken: CancellationToken) = task {
        logger.LogInformation("ThumbnailSubscriber starting, Redis connected: {IsConnected}", redis.IsConnected)
        let subscriber = redis.GetSubscriber()
        let channel =
            RedisChannel(
                "thumbnail-ready",
                RedisChannel.PatternMode.Literal)

        do! subscriber.SubscribeAsync(channel, fun _ msg ->
            logger.LogInformation("Received thumbnail-ready: {Message}, sending SignalR PhotosChanged", msg.ToString())
            hub.Clients.All.SendAsync("PhotosChanged")
                .ContinueWith(fun (t: Task) ->
                    if t.IsFaulted then
                        logger.LogError(t.Exception, "Failed to send SignalR PhotosChanged")
                    else
                        logger.LogInformation "SignalR PhotosChanged sent successfully")
            |> ignore)

        logger.LogInformation
            "Subscribed to Redis thumbnail-ready channel"

        try
            do! Cancellation.awaitToken cancellationToken
        with :? OperationCanceledException -> ()

        do! subscriber.UnsubscribeAsync channel
    }
