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
open Npgsql

type MeResponse = { email: string }
type VocabMoveResponse = { vocabId: string }

[<CLIMutable>]
type GroupPhotosRequest = {
    SourceName: string
    TargetName: string
}

[<CLIMutable>]
type MovePhotoToGroupRequest = {
    PhotoName: string
    TargetGroupId: string
}

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

let private tryParseInt (s: string) =
    match Int32.TryParse s with
    | true, n -> Some n | _ -> None

let private tryParseDate (s: string) =
    match DateTimeOffset.TryParse s with
    | true, dt -> Some dt | _ -> None

let private tryParseDateOnly (s: string) =
    match DateOnly.TryParseExact(s, "yyyyMMdd") with
    | true, d -> Some d | _ -> None

let private qs (ctx: HttpContext) key =
    match ctx.Request.Query.TryGetValue key with
    | true, v -> Some (v.ToString())
    | _ -> None

let photos config (getExcluded: unit -> System.Threading.Tasks.Task<Set<string>>) (ctx: HttpContext) = task {
    let q = qs ctx
    let limit =
        q "limit"
        |> Option.bind tryParseInt
        |> Option.filter (fun n -> n > 0 && n <= 1000)
        |> Option.defaultValue 500
    let dateFilter = q "date"   |> Option.bind tryParseDateOnly
    let before     = q "before" |> Option.bind tryParseDate
    let! excluded = getExcluded()
    let! page = Storage.listPhotoPage config limit dateFilter before excluded
    return Results.Ok {|
        items = page.Items
        nextBefore = page.NextBefore |> Option.toObj
    |}
}

let private normalizeStr =
    Option.ofObj
    >> Option.map (fun (s: string) -> s.Trim())
    >> Option.defaultValue ""

let private (|StorageError|_|) (ex: Azure.RequestFailedException) =
    match ex.Status with
    | 403 -> Some (Results.StatusCode 403)
    | 404 -> Some (Results.NotFound())
    | 409 -> Some (Results.StatusCode 409)
    | _   -> None

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
        with :? RequestFailedException as ex when ex.Status = 403 || ex.Status = 404 || ex.Status = 409 ->
            return (|StorageError|_|) ex |> Option.defaultWith (fun () -> raise ex)
}

let groupPhotos
    (group: string -> string -> Task<unit>)
    (hub: IHubContext<PhotoHub.PhotoHub>)
    (request: GroupPhotosRequest) =
    let source = normalizeStr request.SourceName
    let target = normalizeStr request.TargetName
    let validation =
        if String.IsNullOrWhiteSpace source || String.IsNullOrWhiteSpace target then
            Error "missing_photo_name"
        elif source = target then
            Error "source_and_target_must_differ"
        else Ok (source, target)
    task {
        match validation with
        | Error code -> return Results.BadRequest {| error = code |}
        | Ok (s, t) ->
            try
                do! group s t
                do! hub.Clients.All.SendAsync("PhotosChanged")
                return Results.NoContent()
            with
            | :? RequestFailedException as ex ->
                match ex with
                | StorageError r -> return r
                | _              -> return raise ex
            | :? PostgresException as ex when ex.SqlState = "23505" ->
                return Results.Conflict {| error = "group_conflict" |}
            | :? PostgresException ->
                return Results.StatusCode 503
    }

let movePhotoToGroup
    (movePhoto: string -> string -> Task<unit>)
    (hub: IHubContext<PhotoHub.PhotoHub>)
    (request: MovePhotoToGroupRequest) =
    let photo   = normalizeStr request.PhotoName
    let groupId = normalizeStr request.TargetGroupId
    task {
        if String.IsNullOrWhiteSpace photo || String.IsNullOrWhiteSpace groupId then
            return Results.BadRequest {| error = "missing_fields" |}
        else
            try
                do! movePhoto photo groupId
                do! hub.Clients.All.SendAsync("PhotosChanged")
                return Results.NoContent()
            with
            | :? PostgresException as ex when ex.SqlState = "23503" ->
                return Results.NotFound()
            | :? PostgresException ->
                return Results.StatusCode 503
    }

let private routeStr key (ctx: HttpContext) =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
        let s = string v
        if String.IsNullOrWhiteSpace s then None
        else Some s
    | _ -> None

let private routeInt key (ctx: HttpContext) =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
        match Int32.TryParse(string v) with
        | true, n when n > 0 -> Some n
        | _ -> None
    | _ -> None

let private notifyChanged
    (hub: IHubContext<PhotoHub.PhotoHub>) =
    hub.Clients.All.SendAsync("PhotosChanged")

let assignCategory
    (assign: string -> string -> Task<unit>)
    (ctx: HttpContext)
    (hub: IHubContext<PhotoHub.PhotoHub>) = task {
    let groupId = routeStr "groupId" ctx
    let! body =
        ctx.Request
            .ReadFromJsonAsync<{| categoryName: string |}>()
    let name =
        match isNull (box body) with
        | true  -> None
        | false -> validName body.categoryName
    match groupId, name with
    | Some gid, Some n ->
        do! assign gid n
        do! notifyChanged hub
        return Results.NoContent()
    | _ ->
        return Results.BadRequest {| error = "missing_fields" |}
}

let removeGroupCategory
    (remove: string -> int -> Task<unit>)
    (ctx: HttpContext)
    (hub: IHubContext<PhotoHub.PhotoHub>) = task {
    match routeStr "groupId" ctx, routeInt "categoryId" ctx with
    | Some gid, Some cid ->
        do! remove gid cid
        do! notifyChanged hub
        return Results.NoContent()
    | _ ->
        return Results.BadRequest {| error = "missing_fields" |}
}
