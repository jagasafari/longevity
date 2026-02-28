module Config

open Microsoft.Extensions.Configuration

let private readOr key (section: IConfigurationSection) =
    section.[key]
    |> Option.ofObj
    |> Option.defaultValue ""

let loadGoogleOAuth (cfg: IConfiguration) : Auth.GoogleOAuth =
    let s = cfg.GetSection("GoogleOAuth")
    { ClientId     = s |> readOr "ClientId"
      ClientSecret = s |> readOr "ClientSecret"
      RedirectUri  = s |> readOr "RedirectUri"
      AllowedEmail = s |> readOr "AllowedEmail" }