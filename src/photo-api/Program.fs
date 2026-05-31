open System
open System.Diagnostics
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.DataProtection
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open StackExchange.Redis

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder args
    builder.Services.AddOpenApi()    |> ignore
    builder.Services.AddHttpClient() |> ignore
    builder.Services.AddAuthorization() |> ignore
    builder.Services.AddSignalR()    |> ignore

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun opts ->
            opts.Cookie.HttpOnly  <- true
            opts.Cookie.SameSite  <- SameSiteMode.Lax
            opts.Events.OnRedirectToLogin <- fun ctx ->
                ctx.Response.StatusCode <- 401
                Task.CompletedTask)
    |> ignore

    let oauth    = Config.loadGoogleOAuth builder.Configuration
    let storage  = Config.loadStorage builder.Configuration
    let pgConnStr = Config.loadPostgres builder.Configuration
    let aiEndpoint = Config.loadAiEndpoint builder.Configuration

    let redisConn =
        builder.Configuration["Redis:ConnectionString"]
        |> Option.ofObj
        |> Option.defaultValue "redis-svc:6379"

    let redis = ConnectionMultiplexer.Connect redisConn

    builder.Services
        .AddDataProtection()
        .SetApplicationName("longevity-app")
        .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
    |> ignore

    builder.Services.AddSingleton(storage) |> ignore
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis) |> ignore
    builder.Services.AddHostedService<ThumbnailSubscriber.ThumbnailSubscriberService>() |> ignore
    builder.Services.AddSingleton<PhotoCountCache.Cache>() |> ignore
    builder.Services.AddHostedService(fun sp ->
        PhotoCountCache.RefreshService(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PhotoCountCache.RefreshService>>(),
            sp.GetRequiredService<PhotoCountCache.Cache>(),
            storage,
            pgConnStr)) |> ignore
    builder.Services.Configure<HostOptions>(fun (opts: HostOptions) ->
        opts.BackgroundServiceExceptionBehavior <-
            BackgroundServiceExceptionBehavior.Ignore) |> ignore

    let app = builder.Build()
    DbMigrations.run pgConnStr
    let cache = app.Services.GetRequiredService<PhotoCountCache.Cache>()
    let requestLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Request")

    app.MapOpenApi() |> ignore

    app.UseHttpsRedirection() |> ignore
    app.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next -> task {
        let traceId =
            match Activity.Current with
            | null -> "-"
            | a -> a.TraceId.ToString()
        let path = ctx.Request.Path.Value
        let method = ctx.Request.Method
        requestLogger.LogInformation("[{TraceId}] {Method} {Path}", [| traceId :> obj; method :> obj; path :> obj |])
        do! next.Invoke ctx
        requestLogger.LogInformation("[{TraceId}] {Method} {Path} -> {Status}", [| traceId :> obj; method :> obj; path :> obj; ctx.Response.StatusCode :> obj |])
    })) |> ignore
    app.UseAuthentication()   |> ignore
    app.UseAuthorization()    |> ignore

    app.MapHub<PhotoHub.PhotoHub>("/hubs/photos") |> ignore

    app.MapGet("/healthz", Func<IResult>(fun () -> Results.Ok())) |> ignore

    app.MapGet("/auth/login",
        Func<IResult>(fun () -> Routes.authLogin oauth.ClientId oauth.RedirectUri))
    |> ignore

    app.MapGet("/auth/callback",
        Func<HttpContext, IHttpClientFactory, _>(
            Routes.authCallback (AuthCallback.exchangeCodeForEmail oauth)))
    |> ignore

    app.MapGet("/auth/me",
        Func<HttpContext, IResult>(Routes.authMe))
        .Produces<Routes.MeResponse>()
    |> ignore

    app.MapPost("/auth/logout",
        Func<HttpContext, Task<IResult>>(Routes.authLogout))
    |> ignore

    app.MapGet("/api/photos",
        Func<HttpContext, Task<IResult>>(fun ctx ->
            Routes.photos storage (Vocabulary.listExcludedPhotoNames pgConnStr) ctx))
        .RequireAuthorization()
        .Produces<Storage.PhotoPage>()
    |> ignore

    app.MapGet("/api/photo-groups",
        Func<_>(PhotoGroups.listPhotoGroups pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapGet("/api/photo-groups/tree",
        Func<_>(PhotoGroups.listPhotoGroupTree pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapPost("/api/photo-groups/group",
        Func<Routes.GroupPhotosRequest, IHubContext<PhotoHub.PhotoHub>, _>(
            fun request hub ->
                Routes.groupPhotos
                    (PhotoGroups.groupPhotos pgConnStr)
                    hub
                    request))
        .RequireAuthorization()
    |> ignore

    app.MapPost("/api/photo-groups/move-to-group",
        Func<Routes.MovePhotoToGroupRequest, IHubContext<PhotoHub.PhotoHub>, _>(
            fun request hub ->
                Routes.movePhotoToGroup
                    (PhotoGroups.movePhotoToGroup pgConnStr)
                    hub
                    request))
        .RequireAuthorization()
    |> ignore

    app.MapDelete("/api/photo-groups/{name}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(
            fun ctx hub ->
                let name =
                    match ctx.Request.RouteValues.TryGetValue("name") with
                    | true, value -> string value
                    | _ -> ""
                task {
                    if System.String.IsNullOrWhiteSpace name then
                        return Results.BadRequest {| error = "missing_photo_name" |}
                    else
                        do! PhotoGroups.removePhotoFromGroups pgConnStr name
                        do! hub.Clients.All.SendAsync("PhotosChanged")
                        return Results.NoContent()
                }))
        .RequireAuthorization()
    |> ignore

    app.MapDelete("/api/photos/{name}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(
            fun ctx hub ->
                let name =
                    match ctx.Request.RouteValues.TryGetValue("name") with
                    | true, value -> string value
                    | _ -> ""

                Routes.deletePhoto
                    (Storage.deletePhoto storage)
                    (fun photoName ->
                        PhotoGroups.removePhotoFromGroups pgConnStr photoName
                        :> Task)
                    hub
                    name))
        .RequireAuthorization()
    |> ignore

    // Category endpoints
    app.MapGet("/api/categories",
        Func<_>(Categories.listCategories pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapGet("/api/group-categories",
        Func<_>(Categories.listGroupCategories pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapPost("/api/group-categories/{groupId}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(
            Routes.assignCategory
                (Categories.assignCategory pgConnStr)))
        .RequireAuthorization()
    |> ignore

    app.MapDelete(
        "/api/group-categories/{groupId}/{categoryId:int}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(
            Routes.removeGroupCategory
                (Categories.removeCategory pgConnStr)))
        .RequireAuthorization()
    |> ignore

    // Vocabulary endpoints
    app.MapGet("/api/vocabulary/groups",
        Func<_>(Vocabulary.listGroups storage pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapGet("/api/vocabulary/unassigned",
        Func<_>(Vocabulary.listUnassigned storage pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapPatch("/api/vocabulary/groups/{groupId}/name",
        Func<HttpContext, _>(fun ctx ->
            let groupId =
                match ctx.Request.RouteValues.TryGetValue "groupId" with
                | true, v -> string v | _ -> ""
            task {
                if System.String.IsNullOrWhiteSpace groupId then
                    return Results.BadRequest {| error = "missing_group_id" |}
                else
                    let! body = ctx.Request.ReadFromJsonAsync<{| name: string |}>()
                    if box body = null || System.String.IsNullOrWhiteSpace body.name then
                        return Results.BadRequest {| error = "missing_name" |}
                    else
                        do! Vocabulary.renameGroup pgConnStr groupId body.name
                        return Results.NoContent()
            }))
        .RequireAuthorization()
    |> ignore

    app.MapDelete("/api/vocabulary/groups/{groupId}/photos",
        Func<HttpContext, _>(fun ctx ->
            let groupId =
                match ctx.Request.RouteValues.TryGetValue "groupId" with
                | true, v -> string v | _ -> ""
            task {
                if System.String.IsNullOrWhiteSpace groupId then
                    return Results.BadRequest {| error = "missing_group_id" |}
                else
                    let! body = ctx.Request.ReadFromJsonAsync<{| photoName: string |}>()
                    if box body = null || System.String.IsNullOrWhiteSpace body.photoName then
                        return Results.BadRequest {| error = "missing_photo_name" |}
                    else
                        do! Vocabulary.removePhoto pgConnStr groupId body.photoName
                        return Results.NoContent()
            }))
        .RequireAuthorization()
    |> ignore

    app.MapPost("/api/vocabulary/groups/{groupId}/photos",
        Func<HttpContext, _>(fun ctx ->
            let groupId =
                match ctx.Request.RouteValues.TryGetValue "groupId" with
                | true, v -> string v | _ -> ""
            task {
                if System.String.IsNullOrWhiteSpace groupId then
                    return Results.BadRequest {| error = "missing_group_id" |}
                else
                    let! body = ctx.Request.ReadFromJsonAsync<{| photoName: string |}>()
                    if box body = null || System.String.IsNullOrWhiteSpace body.photoName then
                        return Results.BadRequest {| error = "missing_photo_name" |}
                    else
                        do! Vocabulary.addPhoto storage pgConnStr groupId body.photoName
                        return Results.NoContent()
            }))
        .RequireAuthorization()
    |> ignore

    app.MapPost("/api/vocabulary/groups/{groupId}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(fun ctx hub ->
            let groupId =
                match ctx.Request.RouteValues.TryGetValue "groupId" with
                | true, v -> string v | _ -> ""
            task {
                if System.String.IsNullOrWhiteSpace groupId then
                    return Results.BadRequest {| error = "missing_group_id" |}
                else
                    let! vocabId = Vocabulary.moveGalleryGroup pgConnStr groupId
                    do! hub.Clients.All.SendAsync("PhotosChanged")
                    return Results.Ok { Routes.VocabMoveResponse.vocabId = vocabId }
            }))
        .RequireAuthorization()
        .Produces<Routes.VocabMoveResponse>()
    |> ignore

    app.MapDelete("/api/vocabulary/groups/{groupId}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(fun ctx hub ->
            let groupId =
                match ctx.Request.RouteValues.TryGetValue "groupId" with
                | true, v -> string v | _ -> ""
            task {
                if System.String.IsNullOrWhiteSpace groupId then
                    return Results.BadRequest {| error = "missing_group_id" |}
                else
                    do! Vocabulary.removeGroup pgConnStr groupId
                    do! hub.Clients.All.SendAsync("PhotosChanged")
                    return Results.NoContent()
            }))
        .RequireAuthorization()
    |> ignore

    // AI labeling endpoints (only registered when AzureAI:Endpoint is configured)
    match aiEndpoint with
    | None ->
        app.Logger.LogWarning("AzureAI:Endpoint not configured; AI label endpoints disabled")
    | Some endpoint ->
        let routeParam (ctx: HttpContext) key =
            match ctx.Request.RouteValues.TryGetValue (key: string) with
            | true, v -> string v | _ -> ""

        app.MapPost("/api/vocabulary/photos/{photoName}/label",
            Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(fun ctx hub ->
                let photoName = routeParam ctx "photoName"
                task {
                    if System.String.IsNullOrWhiteSpace photoName then
                        return Results.BadRequest {| error = "missing_photo_name" |}
                    else
                        try
                            let! result =
                                PhotoLabel.labelPhoto storage pgConnStr endpoint photoName
                            do! hub.Clients.All.SendAsync("PhotoLabeled", result)
                            return Results.Ok result
                        with ex ->
                            do! hub.Clients.All.SendAsync(
                                    "PhotoLabelFailed",
                                    {| photoName = photoName; error = ex.Message |})
                            return Results.Json(
                                {| photoName = photoName; error = ex.Message |},
                                statusCode = 502)
                }))
            .RequireAuthorization()
        |> ignore

        app.MapPost("/api/vocabulary/groups/{groupId}/label-all",
            Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(fun ctx hub ->
                let groupId = routeParam ctx "groupId"
                task {
                    if System.String.IsNullOrWhiteSpace groupId then
                        return Results.BadRequest {| error = "missing_group_id" |}
                    else
                        let onLabeled (r: PhotoLabel.LabelResult) =
                            hub.Clients.All.SendAsync("PhotoLabeled", r) :> Task
                        let onFailed (name: string) (err: string) =
                            hub.Clients.All.SendAsync(
                                "PhotoLabelFailed",
                                {| photoName = name; error = err |}) :> Task
                        let! summary =
                            PhotoLabel.labelGroup storage pgConnStr endpoint groupId
                                onLabeled onFailed
                        do! hub.Clients.All.SendAsync("PhotosChanged")
                        return Results.Ok summary
                }))
            .RequireAuthorization()
        |> ignore

        app.MapPost("/api/vocabulary/groups/{groupId}/match-subgroups",
            Func<HttpContext, _>(fun ctx ->
                let groupId = routeParam ctx "groupId"
                task {
                    if System.String.IsNullOrWhiteSpace groupId then
                        return Results.BadRequest {| error = "missing_group_id" |}
                    else
                        let! proposals = PhotoLabel.matchSubgroups pgConnStr endpoint groupId
                        return Results.Ok proposals
                }))
            .RequireAuthorization()
        |> ignore

        app.MapPost("/api/vocabulary/groups/{groupId}/apply-subgroups",
            Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(fun ctx hub ->
                let groupId = routeParam ctx "groupId"
                task {
                    if System.String.IsNullOrWhiteSpace groupId then
                        return Results.BadRequest {| error = "missing_group_id" |}
                    else
                        let! proposals =
                            ctx.Request.ReadFromJsonAsync<PhotoLabel.SubgroupProposal array>()
                        do! PhotoLabel.applySubgroups pgConnStr groupId proposals
                        do! hub.Clients.All.SendAsync("PhotosChanged")
                        return Results.NoContent()
                }))
            .RequireAuthorization()
        |> ignore

        app.MapPatch("/api/vocabulary/photos/{photoName}/word",
            Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(fun ctx hub ->
                let photoName = routeParam ctx "photoName"
                task {
                    if System.String.IsNullOrWhiteSpace photoName then
                        return Results.BadRequest {| error = "missing_photo_name" |}
                    else
                        let! body =
                            ctx.Request.ReadFromJsonAsync<{| word: string |}>()
                        let word =
                            if System.String.IsNullOrWhiteSpace body.word
                            then None else Some (body.word.Trim())
                        do! PhotoLabel.setWord pgConnStr photoName word
                        do! hub.Clients.All.SendAsync("PhotosChanged")
                        return Results.NoContent()
                }))
            .RequireAuthorization()
        |> ignore

    app.MapGet("/api/photo-counts",
        Func<_>(PhotoCountCache.list cache))
        .RequireAuthorization()
    |> ignore

    app.Run()
    0
