module AuthCallback

open System.Net.Http
open System.Text.Json

let private fetchToken (cfg: Auth.GoogleOAuth) (http: HttpClient) code =
    let (Auth.ClientId id)        = cfg.ClientId
    let (Auth.ClientSecret secret) = cfg.ClientSecret
    let (Auth.HttpsUri uri)       = cfg.RedirectUri
    task {
        let form = dict [
            "code",          code
            "client_id",     id
            "client_secret", secret
            "redirect_uri",  uri
            "grant_type",    "authorization_code"
        ]
        let! resp =
            http.PostAsync(Auth.Google.tokenUrl, new FormUrlEncodedContent(form))
        let! json = resp.Content.ReadAsStringAsync()
        return JsonDocument.Parse(json) |> Auth.jsonProp "access_token"
    }

let private fetchEmail (http: HttpClient) token = task {
    use req = new HttpRequestMessage(HttpMethod.Get, Auth.Google.userInfoUrl)
    req.Headers.Authorization <-
        Headers.AuthenticationHeaderValue("Bearer", token)
    let! resp = http.SendAsync req
    let! json = resp.Content.ReadAsStringAsync()
    return JsonDocument.Parse(json) |> Auth.jsonProp "email" |> Result.map Auth.Email
}

let internal authorize (Auth.Email allowed) (Auth.Email actual) =
    if allowed = actual
    then Auth.Authorized (Auth.Email actual)
    else Auth.Denied $"Email {actual} not allowed"

let exchangeCodeForEmail (cfg: Auth.GoogleOAuth) (http: HttpClient) (code: string) =
    task {
        try
            match! fetchToken cfg http code
                   |> Auth.TaskResult.bind (fetchEmail http) with
            | Ok email    -> return authorize cfg.AllowedEmail email
            | Error msg   -> return Auth.Error msg
        with ex -> return Auth.Error ex.Message
    }
