module AuthLogin

let buildLoginUrl
    (Auth.ClientId clientId)
    (Auth.HttpsUri redirectUri) =
    let qs = Auth.buildQuery [
        "client_id",     clientId
        "redirect_uri",  redirectUri
        "response_type", "code"
        "scope",         "openid email"
        "access_type",   "offline"
        "prompt",        "consent"
    ]
    $"{Auth.Google.authUrl}?{qs}"
