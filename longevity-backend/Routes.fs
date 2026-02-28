module Routes

open System
open System.Net.Http
open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http

let weatherForecast () =
    DateOnly.FromDateTime DateTime.Now
    |> fun today -> Weather.generateRandom today 5

let authLogin clientId redirectUri : IResult =
    AuthLogin.buildLoginUrl clientId redirectUri
    |> Results.Redirect

let private signIn (ctx: HttpContext) email =
    let scheme =
        CookieAuthenticationDefaults.AuthenticationScheme
    let identity =
        ClaimsIdentity(
            [ Claim(ClaimTypes.Email, email) ],
            scheme)
    ctx.SignInAsync(
        scheme, ClaimsPrincipal identity)

let authCallback
    (exchange:
        HttpClient
            -> string
            -> Task<Auth.AuthResult>)
    (ctx: HttpContext)
    (factory: IHttpClientFactory)
    = task {
        let code =
            ctx.Request.Query["code"] |> string

        if String.IsNullOrEmpty code then
            return Results.Redirect
                "/?error=missing_code"
        else
            let http = factory.CreateClient()
            let! result = exchange http code

            match result with
            | Auth.Authorized email ->
                do! signIn ctx email
                return Results.Redirect "/"
            | Auth.Denied reason ->
                let msg = Uri.EscapeDataString reason
                return Results.Redirect $"/?error={msg}"
            | Auth.Error msg ->
                let err = Uri.EscapeDataString msg
                return Results.Redirect $"/?error={err}"
    }

let authMe (ctx: HttpContext) : IResult =
    match ctx.User.Identity.IsAuthenticated with
    | true ->
        ctx.User.FindFirstValue ClaimTypes.Email
        |> fun email -> Results.Ok {| email = email |}
    | false ->
        Results.Json(
            {| error = "Not authenticated" |},
            statusCode = 401)

let authLogout (ctx: HttpContext) = task {
    do! ctx.SignOutAsync()
    return Results.Redirect "/"
}
