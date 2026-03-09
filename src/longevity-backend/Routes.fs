module Routes

open System
open System.Net.Http
open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http

let weatherForecast () =
    DateOnly.FromDateTime DateTime.Now |> fun today -> Weather.generateRandom today 5

let authLogin clientId redirectUri : IResult =
    AuthLogin.buildLoginUrl clientId redirectUri |> Results.Redirect

let private signIn (ctx: HttpContext) (Auth.Email email) =
    let scheme = CookieAuthenticationDefaults.AuthenticationScheme
    let identity = ClaimsIdentity([ Claim(ClaimTypes.Email, email) ], scheme)
    ctx.SignInAsync(scheme, ClaimsPrincipal identity)

let internal redirectUrl = function
    | Auth.Authorized _ -> "/"
    | Auth.Denied reason -> $"/?error={Uri.EscapeDataString reason}"
    | Auth.Error msg     -> $"/?error={Uri.EscapeDataString msg}"

let private signInIfAuthorized ctx = function
    | Auth.Authorized email -> signIn ctx email
    | _                     -> Task.CompletedTask

let private extractCode (ctx: HttpContext) =
    match ctx.Request.Query["code"] |> string with
    | null | "" -> None
    | code      -> Some code

let authCallback
    (exchange: HttpClient -> string -> Task<Auth.AuthResult>)
    (ctx: HttpContext)
    (factory: IHttpClientFactory)
    = task {
        let! result =
            match extractCode ctx with
            | None      -> Task.FromResult(Auth.Error "missing_code")
            | Some code -> exchange (factory.CreateClient()) code

        do! signInIfAuthorized ctx result
        return Results.Redirect(redirectUrl result)
    }

let authMe (ctx: HttpContext) : IResult =
    match ctx.User.FindFirstValue ClaimTypes.Email with
    | null  -> Results.Json({| error = "Not authenticated" |}, statusCode = 401)
    | email -> Results.Ok {| email = email |}

let authLogout (ctx: HttpContext) = task {
    do! ctx.SignOutAsync()
    return Results.Redirect "/"
}

let photos config () =
    Storage.listRecentPhotos config 10

let deletePhoto (delete: string -> Task<bool>) (name: string) = task {
    let! deleted = delete name
    return if deleted then Results.NoContent() else Results.NotFound()
}
