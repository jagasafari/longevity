open System
open System.Diagnostics
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
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

    let oauth   = Config.loadGoogleOAuth builder.Configuration
    let storage = Config.loadStorage builder.Configuration

    let redisConn =
        builder.Configuration["Redis:ConnectionString"]
        |> Option.ofObj
        |> Option.defaultValue "redis-svc:6379"

    builder.Services.AddSingleton(storage) |> ignore
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect redisConn) |> ignore
    builder.Services.AddHostedService<ThumbnailSubscriber.ThumbnailSubscriberService>() |> ignore

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.MapOpenApi() |> ignore

    app.UseHttpsRedirection() |> ignore
    app.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next -> task {
        let traceId =
            match Activity.Current with
            | null -> "-"
            | a -> a.TraceId.ToString()
        let logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request")
        let path = ctx.Request.Path.Value
        let method = ctx.Request.Method
        logger.LogInformation("[{TraceId}] {Method} {Path}", [| traceId :> obj; method :> obj; path :> obj |])
        do! next.Invoke ctx
        logger.LogInformation("[{TraceId}] {Method} {Path} -> {Status}", [| traceId :> obj; method :> obj; path :> obj; ctx.Response.StatusCode :> obj |])
    })) |> ignore
    app.UseAuthentication()   |> ignore
    app.UseAuthorization()    |> ignore

    app.MapHub<PhotoHub.PhotoHub>("/hubs/photos") |> ignore

    app.MapGet("/api/weatherforecast",
        Func<_>(Routes.weatherForecast))
    |> ignore

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
        Func<_>(Routes.photos storage))
        .RequireAuthorization()
    |> ignore

    app.MapGet("/api/photo-groups",
        Func<_>(PhotoGroups.listPhotoGroups storage))
        .RequireAuthorization()
    |> ignore

    app.MapPost("/api/photo-groups/group",
        Func<Routes.GroupPhotosRequest, IHubContext<PhotoHub.PhotoHub>, _>(
            fun request hub ->
                Routes.groupPhotos
                    (PhotoGroups.groupPhotos storage)
                    hub
                    request))
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
                        PhotoGroups.removePhotoFromGroups storage photoName
                        :> Task)
                    hub
                    name))
        .RequireAuthorization()
    |> ignore

    app.Run()
    0
