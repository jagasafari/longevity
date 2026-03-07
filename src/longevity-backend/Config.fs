module Config

open Microsoft.Extensions.Configuration

let private require key (section: IConfigurationSection) =
    section.[key]
    |> Option.ofObj
    |> Option.filter (fun s -> s.Length > 0)
    |> Option.defaultWith (fun () -> failwith $"Missing config: GoogleOAuth:{key}")

let loadGoogleOAuth (cfg: IConfiguration) : Auth.GoogleOAuth =
    let s = cfg.GetSection "GoogleOAuth"
    { ClientId     = require "ClientId" s |> Auth.ClientId
      ClientSecret = require "ClientSecret" s |> Auth.ClientSecret
      RedirectUri  = s["RedirectUri"]  |> Auth.requireHttpsUri "GoogleOAuth:RedirectUri"
      AllowedEmail = s["AllowedEmail"] |> Auth.requireEmail "GoogleOAuth:AllowedEmail" }