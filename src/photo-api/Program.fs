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

let private logRequest (logger: ILogger) (ctx: HttpContext) (next: RequestDelegate) = task {
    let traceId =
        match Activity.Current with
        | null -> "-"
        | a    -> a.TraceId.ToString()
    let method = ctx.Request.Method
    let path   = ctx.Request.Path.Value
    logger.LogInformation("[{TraceId}] {Method} {Path}", traceId, method, path)
    do! next.Invoke ctx
    logger.LogInformation("[{TraceId}] {Method} {Path} -> {Status}", traceId, method, path, ctx.Response.StatusCode)
}

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
    builder.Services.AddHostedService<PhotoCountCache.RefreshService>() |> ignore
    builder.Services.Configure<HostOptions>(fun (opts: HostOptions) ->
        opts.BackgroundServiceExceptionBehavior <-
            BackgroundServiceExceptionBehavior.Ignore) |> ignore

    let app = builder.Build()
    DbMigrations.run pgConnStr
    let cache = app.Services.GetRequiredService<PhotoCountCache.Cache>()
    let requestLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Request")

    if app.Environment.IsDevelopment() then
        app.MapOpenApi() |> ignore

    app.UseHttpsRedirection() |> ignore
    app.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next ->
        if ctx.Request.Path.Value = "/healthz" then next.Invoke ctx
        else logRequest requestLogger ctx next)) |> ignore
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
    |> ignore

    app.MapPost("/auth/logout",
        Func<HttpContext, Task<IResult>>(Routes.authLogout))
    |> ignore

    app.MapGet("/api/photos",
        Func<HttpContext, Task<IResult>>(fun ctx -> Routes.photos storage pgConnStr ctx))
        .RequireAuthorization()
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

    // Group name endpoints
    app.MapGet("/api/group-names",
        Func<_>(GroupNames.listNames pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapGet("/api/group-name-assignments",
        Func<_>(GroupNames.listGroupNames pgConnStr))
        .RequireAuthorization()
    |> ignore

    app.MapPost("/api/group-names/{groupId}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(
            Routes.assignGroupName
                (GroupNames.assignName pgConnStr)))
        .RequireAuthorization()
    |> ignore

    app.MapDelete(
        "/api/group-names/{groupId}/{name}",
        Func<HttpContext, IHubContext<PhotoHub.PhotoHub>, _>(
            Routes.removeGroupName
                (GroupNames.removeName pgConnStr)))
        .RequireAuthorization()
    |> ignore

    app.MapGet("/api/photo-counts",
        Func<_>(PhotoCountCache.list cache))
        .RequireAuthorization()
    |> ignore

    app.Run()
    0
