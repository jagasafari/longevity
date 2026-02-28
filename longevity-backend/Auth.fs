module Auth

open System
open System.Net.Http
open System.Text.Json

type GoogleOAuth =
    { ClientId: string
      ClientSecret: string
      RedirectUri: string
      AllowedEmail: string }

let private authBase =
    "https://accounts.google.com/o/oauth2/v2/auth"

let private tokenUrl =
    "https://oauth2.googleapis.com/token"

let private userInfoUrl =
    "https://www.googleapis.com/oauth2/v2/userinfo"

let buildLoginUrl clientId (redirectUri: string) =
    let qs = Uri.EscapeDataString redirectUri
    $"{authBase}?client_id={clientId}\
      &redirect_uri={qs}\
      &response_type=code\
      &scope=openid%%20email\
      &access_type=offline\
      &prompt=consent"

type AuthResult =
    | Authorized of email: string
    | Denied     of reason: string
    | Error      of message: string

let private jsonProp
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
                tokenUrl,
                new FormUrlEncodedContent(form))
        let! json = resp.Content.ReadAsStringAsync()
        return JsonDocument.Parse(json)
               |> jsonProp "access_token"
    }

let private fetchEmail (http: HttpClient) token =
    task {
        use req =
            new HttpRequestMessage(
                HttpMethod.Get, userInfoUrl)
        req.Headers.Authorization <-
            Headers.AuthenticationHeaderValue(
                "Bearer", token)
        let! resp = http.SendAsync req
        let! json = resp.Content.ReadAsStringAsync()
        return JsonDocument.Parse(json)
               |> jsonProp "email"
    }

let private authorize allowedEmail email =
    if email = allowedEmail
    then Authorized email
    else Denied $"Email {email} not allowed"

let exchangeCodeForEmail
    (http: HttpClient)
    (cfg: GoogleOAuth)
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
