open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder args
    builder.Services.AddOpenApi()    |> ignore
    builder.Services.AddHttpClient() |> ignore
    builder.Services.AddAuthorization() |> ignore

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun opts ->
            opts.Cookie.HttpOnly  <- true
            opts.Cookie.SameSite  <- SameSiteMode.Lax
            opts.Events.OnRedirectToLogin <- fun ctx ->
                ctx.Response.StatusCode <- 401
                Task.CompletedTask)
    |> ignore

    let app     = builder.Build()
    let oauth   = Config.loadGoogleOAuth app.Configuration
    let storage = Config.loadStorage app.Configuration

    if app.Environment.IsDevelopment() then
        app.MapOpenApi() |> ignore

    app.UseHttpsRedirection() |> ignore
    app.UseAuthentication()   |> ignore
    app.UseAuthorization()    |> ignore

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

    app.Run()
    0
