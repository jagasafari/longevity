module Auth

open System
open System.Net.Http
open System.Text.Json

type GoogleOAuth =
    { ClientId: string
      ClientSecret: string
      RedirectUri: string
      AllowedEmail: string }

type AuthResult =
    | Authorized of email: string
    | Denied     of reason: string
    | Error      of message: string

module private Google =
    let authUrl =
        "https://accounts.google.com/o/oauth2/v2/auth"
    let tokenUrl =
        "https://oauth2.googleapis.com/token"
    let userInfoUrl =
        "https://www.googleapis.com/oauth2/v2/userinfo"

let private buildQuery
    (pairs: (string * string) list) =
    pairs
    |> List.map (fun (k, v) ->
        $"{k}={Uri.EscapeDataString v}")
    |> String.concat "&"

let buildLoginUrl clientId (redirectUri: string) =
    let qs = buildQuery [
        "client_id",     clientId
        "redirect_uri",  redirectUri
        "response_type", "code"
        "scope",         "openid email"
        "access_type",   "offline"
        "prompt",        "consent"
    ]
    $"{Google.authUrl}?{qs}"

let internal jsonProp
    (name: string)
    (doc: JsonDocument)
    : Result<string, string> =
    match doc.RootElement.TryGetProperty(name) with
    | true, (el: JsonElement) -> Ok (el.GetString())
    | false, _ -> Result.Error $"Missing '{name}'"

let private fetchToken
    (http: HttpClient) (cfg: GoogleOAuth) code =
    task {
        let form = dict [
            "code",          code
            "client_id",     cfg.ClientId
            "client_secret", cfg.ClientSecret
            "redirect_uri",  cfg.RedirectUri
            "grant_type",    "authorization_code"
        ]
        let! resp =
            http.PostAsync(
                Google.tokenUrl,
                new FormUrlEncodedContent(form))
        let! json = resp.Content.ReadAsStringAsync()
        return JsonDocument.Parse(json)
               |> jsonProp "access_token"
    }

let private fetchEmail (http: HttpClient) token =
    task {
        use req =
            new HttpRequestMessage(
                HttpMethod.Get, Google.userInfoUrl)
        req.Headers.Authorization <-
            Headers.AuthenticationHeaderValue(
                "Bearer", token)
        let! resp = http.SendAsync req
        let! json = resp.Content.ReadAsStringAsync()
        return JsonDocument.Parse(json)
               |> jsonProp "email"
    }

let internal authorize allowedEmail email =
    if email = allowedEmail
    then Authorized email
    else Denied $"Email {email} not allowed"

let exchangeCodeForEmail
    (cfg: GoogleOAuth)
    (http: HttpClient)
    (code: string)
    = task {
        try
            match! fetchToken http cfg code with
            | Result.Error msg -> return Error msg
            | Ok token ->
            match! fetchEmail http token with
            | Result.Error msg -> return Error msg
            | Ok email -> return authorize cfg.AllowedEmail email
        with ex ->
            return Error ex.Message
    }
