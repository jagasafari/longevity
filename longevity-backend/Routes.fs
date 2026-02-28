module Routes

open System
open System.Net.Http
open Microsoft.AspNetCore.Http

// ── Weather ──────────────────────────────

let weatherForecast () =
    DateOnly.FromDateTime DateTime.Now
    |> fun today -> Weather.generateRandom today 5

// ── Auth ─────────────────────────────────

let authLogin (cfg: Auth.GoogleOAuth) : IResult =
    Auth.buildLoginUrl cfg |> Results.Redirect

/// Map domain result to HTTP response — pure.
let private toResult = function
    | Auth.Authorized email ->
        Results.Ok {| message = "Authenticated"
                      email   = email |}
    | Auth.Denied reason ->
        Results.Json(
            {| error = reason |}, statusCode = 403)
    | Auth.Error msg ->
        Results.Json(
            {| error = msg |}, statusCode = 502)

let authCallback
    (cfg: Auth.GoogleOAuth)
    (ctx: HttpContext)
    (factory: IHttpClientFactory)
    = task {
        let code = ctx.Request.Query["code"] |> string

        if String.IsNullOrEmpty code then
            return Results.BadRequest
                {| error = "Missing code parameter" |}
        else
            let http = factory.CreateClient()
            let! result =
                Auth.exchangeCodeForEmail http cfg code
            return toResult result
    }
