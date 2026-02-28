open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddOpenApi() |> ignore

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.MapOpenApi() |> ignore

    app.UseHttpsRedirection() |> ignore

    app.MapGet(
        "/weatherforecast",
        Func<_>(fun () ->
            let today = DateOnly.FromDateTime DateTime.Now
            Weather.generateRandom today 5))
        .WithName("GetWeatherForecast")
    |> ignore

    app.Run()
    0
