module Config

open Microsoft.Extensions.Configuration

let private require key (section: IConfigurationSection) =
    section.[key]
    |> Option.ofObj
    |> Option.filter (fun s -> s.Length > 0)
  |> Option.defaultWith (fun () -> failwith $"Missing config: {section.Path}:{key}")

let loadGoogleOAuth (cfg: IConfiguration) : Auth.GoogleOAuth =
    let s = cfg.GetSection "GoogleOAuth"
    { ClientId     = require "ClientId" s |> Auth.ClientId
      ClientSecret = require "ClientSecret" s |> Auth.ClientSecret
      RedirectUri  = s["RedirectUri"]  |> Auth.requireHttpsUri "GoogleOAuth:RedirectUri"
      AllowedEmail = s["AllowedEmail"] |> Auth.requireEmail "GoogleOAuth:AllowedEmail" }

let loadStorage (cfg: IConfiguration) : Storage.StorageConfig =
    let s = cfg.GetSection "Storage"
    { AccountName = require "AccountName" s
      ContainerName =
        s["ContainerName"]
        |> Option.ofObj
        |> Option.filter (fun v -> v.Length > 0)
        |> Option.defaultValue "photos" }

let loadPostgres (cfg: IConfiguration) : string =
    cfg["Postgres:ConnectionString"]
    |> Option.ofObj
    |> Option.filter (fun s -> s.Length > 0)
    |> Option.defaultWith (fun () -> failwith "Missing config: Postgres:ConnectionString")

let loadAiEndpoint (cfg: IConfiguration) : string option =
    cfg["AzureAI:Endpoint"]
    |> Option.ofObj
    |> Option.filter (fun s -> s.Length > 0)