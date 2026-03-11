module Routes

open System
open System.Net.Http
open System.Security.Claims
open System.Threading.Tasks
open Azure
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SignalR

[<CLIMutable>]
type GroupPhotosRequest = {
    SourceName: string
    TargetName: string
}

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

let private validName = function
    | null -> None
    | name when String.IsNullOrWhiteSpace name -> None
    | name -> Some name

let private toDeleteResult = function
    | None -> Results.BadRequest {| error = "missing_photo_name" |}
    | Some true -> Results.NoContent()
    | Some false -> Results.NotFound()

let private notifyOnDelete (hub: IHubContext<PhotoHub.PhotoHub>) = function
    | true -> hub.Clients.All.SendAsync("PhotosChanged")
    | false -> Task.CompletedTask

let deletePhoto
    (delete: string -> Task<bool>)
    (removeFromGroups: string -> Task)
    (hub: IHubContext<PhotoHub.PhotoHub>)
    (name: string) = task {
    match validName name with
    | None -> return toDeleteResult None
    | Some blobName ->
        try
            let! deleted = delete blobName
            if deleted then
                do! removeFromGroups blobName

            do! notifyOnDelete hub deleted
            return toDeleteResult (Some deleted)
        with
        | :? RequestFailedException as ex when ex.Status = 403 ->
            return Results.StatusCode 403
        | :? RequestFailedException as ex when ex.Status = 404 ->
            return Results.NotFound()
        | :? RequestFailedException as ex when ex.Status = 409 ->
            return Results.StatusCode 409
}

let groupPhotos
    (group: string -> string -> Task<unit>)
    (hub: IHubContext<PhotoHub.PhotoHub>)
    (request: GroupPhotosRequest) =
    let validate req =
        let normalize =
            Option.ofObj
            >> Option.map (fun (s: string) -> s.Trim())
            >> Option.defaultValue ""
        let source = normalize req.SourceName
        let target = normalize req.TargetName

        match source, target with
        | s, t when String.IsNullOrWhiteSpace s || String.IsNullOrWhiteSpace t ->
            Error "missing_photo_name"
        | s, t when s = t -> Error "source_and_target_must_differ"
        | s, t -> Ok (s, t)

    task {
        match validate request with
        | Error code ->
            return Results.BadRequest {| error = code |}
        | Ok (source, target) ->
            do! group source target
            do! hub.Clients.All.SendAsync("PhotosChanged")
            return Results.NoContent()
    }
