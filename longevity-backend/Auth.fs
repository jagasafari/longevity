module Auth

open System
open System.Text.Json
open System.Threading.Tasks

type Email = Email of string

type GoogleOAuth =
    { ClientId: string
      ClientSecret: string
      RedirectUri: string
      AllowedEmail: Email }

type AuthResult =
    | Authorized of Email
    | Denied     of reason: string
    | Error      of message: string

module internal Google =
    let authUrl =
        "https://accounts.google.com/o/oauth2/v2/auth"
    let tokenUrl =
        "https://oauth2.googleapis.com/token"
    let userInfoUrl =
        "https://www.googleapis.com/oauth2/v2/userinfo"

module internal TaskResult =
    let bind
        (f: 'a -> Task<Result<'b, 'e>>)
        (t: Task<Result<'a, 'e>>) = task {
        match! t with
        | Ok x           -> return! f x
        | Result.Error e -> return Result.Error e
    }

let internal buildQuery
    (pairs: (string * string) list) =
    pairs
    |> List.map (fun (k, v) ->
        $"{k}={Uri.EscapeDataString v}")
    |> String.concat "&"

let internal jsonProp
    (name: string)
    (doc: JsonDocument)
    : Result<string, string> =
    match doc.RootElement.TryGetProperty(name) with
    | true, (el: JsonElement) -> Ok (el.GetString())
    | false, _ -> Result.Error $"Missing '{name}'"
