open System
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PhotoCountWorker(logger: ILogger<PhotoCountWorker>, config: PhotoCounter.Config) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken) = task {
        logger.LogInformation("Photo count worker starting")

        while not cancellationToken.IsCancellationRequested do
            try
                logger.LogInformation("Computing photo counts...")
                let! dateCount = PhotoCounter.computeAndStore config
                logger.LogInformation("Updated counts for {DateCount} dates", dateCount)
            with ex ->
                logger.LogError(ex, "Failed to compute photo counts")

            do! Task.Delay(TimeSpan.FromMinutes 5.0, cancellationToken)
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

    let config: PhotoCounter.Config =
        { AccountName = require "AccountName"
          ContainerName =
              s["ContainerName"]
              |> Option.ofObj
              |> Option.defaultValue "photos"
          ConnectionString =
              builder.Configuration["ConnectionStrings:Postgres"]
              |> Option.ofObj
              |> Option.defaultWith (fun () -> failwith "Missing ConnectionStrings:Postgres") }

    builder.Services.AddSingleton(config) |> ignore
    builder.Services.AddHostedService<PhotoCountWorker>() |> ignore
    builder.Build().Run()
    0
