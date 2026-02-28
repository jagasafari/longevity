module AuthCallback

open System.Net.Http
open System.Text.Json

let private fetchToken
    (http: HttpClient) (cfg: Auth.GoogleOAuth) code =
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
                Auth.Google.tokenUrl,
                new FormUrlEncodedContent(form))
        let! json = resp.Content.ReadAsStringAsync()
        return JsonDocument.Parse(json)
               |> Auth.jsonProp "access_token"
    }

let private fetchEmail (http: HttpClient) token =
    task {
        use req =
            new HttpRequestMessage(
                HttpMethod.Get, Auth.Google.userInfoUrl)
        req.Headers.Authorization <-
            Headers.AuthenticationHeaderValue(
                "Bearer", token)
        let! resp = http.SendAsync req
        let! json = resp.Content.ReadAsStringAsync()
        return JsonDocument.Parse(json)
               |> Auth.jsonProp "email"
    }

let internal authorize allowedEmail email =
    if email = allowedEmail
    then Auth.Authorized email
    else Auth.Denied $"Email {email} not allowed"

let exchangeCodeForEmail
    (cfg: Auth.GoogleOAuth)
    (http: HttpClient)
    (code: string)
    = task {
        try
            match! fetchToken http cfg code with
            | Result.Error msg -> return Auth.Error msg
            | Ok token ->
            match! fetchEmail http token with
            | Result.Error msg -> return Auth.Error msg
            | Ok email ->
                return authorize cfg.AllowedEmail email
        with ex ->
            return Auth.Error ex.Message
    }
