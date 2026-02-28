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

let buildLoginUrl (cfg: GoogleOAuth) =
    let qs = Uri.EscapeDataString cfg.RedirectUri
    $"{authBase}?client_id={cfg.ClientId}\
      &redirect_uri={qs}\
      &response_type=code\
      &scope=openid%%20email\
      &access_type=offline\
      &prompt=consent"

type AuthResult =
    | Authorized of email: string
    | Denied     of reason: string
    | Error      of message: string

let exchangeCodeForEmail
    (http: HttpClient)
    (cfg: GoogleOAuth)
    (code: string)
    = task {
    try
        // Exchange code for access token
        let form = dict [
            "code",          code
            "client_id",     cfg.ClientId
            "client_secret", cfg.ClientSecret
            "redirect_uri",  cfg.RedirectUri
            "grant_type",    "authorization_code"
        ]
        let content = new FormUrlEncodedContent(form)
        let! tokenResp =
            http.PostAsync(tokenUrl, content)
        let! tokenJson =
            tokenResp.Content.ReadAsStringAsync()
        let tokenDoc =
            JsonDocument.Parse(tokenJson)

        match tokenDoc.RootElement.TryGetProperty "access_token" with
        | false, _ ->
            return Error $"No access_token in response: {tokenJson}"
        | true, tokenEl ->

        let accessToken = tokenEl.GetString()

        // Fetch user email
        use req = new HttpRequestMessage(
            HttpMethod.Get, userInfoUrl)
        req.Headers.Authorization <-
            Headers.AuthenticationHeaderValue(
                "Bearer", accessToken)
        let! infoResp = http.SendAsync(req)
        let! infoJson =
            infoResp.Content.ReadAsStringAsync()
        let infoDoc = JsonDocument.Parse(infoJson)

        match infoDoc.RootElement.TryGetProperty "email" with
        | false, _ ->
            return Error "No email in userinfo"
        | true, emailEl ->

        let email = emailEl.GetString()

        if email = cfg.AllowedEmail then
            return Authorized email
        else
            return Denied $"Email {email} not allowed"
    with ex ->
        return Error ex.Message
}
