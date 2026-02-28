module Config

open System
open System.Text.RegularExpressions
open Microsoft.Extensions.Configuration

let private require key (section: IConfigurationSection) =
    section.[key]
    |> Option.ofObj
    |> Option.filter (fun s -> s.Length > 0)
    |> Option.defaultWith (fun () ->
        failwith $"Missing config: GoogleOAuth:{key}")

let private requireUri key section =
    let value = require key section
    match Uri.TryCreate(value, UriKind.Absolute) with
    | true, uri when uri.Scheme = "https" -> value
    | _ -> failwith $"Invalid HTTPS URI: GoogleOAuth:{key}"

let private requireEmail key section =
    let value = require key section
    if Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")
    then Auth.Email value
    else failwith $"Invalid email: GoogleOAuth:{key}"

let loadGoogleOAuth (cfg: IConfiguration) : Auth.GoogleOAuth =
    let s = cfg.GetSection("GoogleOAuth")
    { ClientId     = s |> require "ClientId"
      ClientSecret = s |> require "ClientSecret"
      RedirectUri  = s |> requireUri "RedirectUri"
      AllowedEmail = s |> requireEmail "AllowedEmail" }