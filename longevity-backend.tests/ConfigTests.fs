module ConfigTests

open System.Collections.Generic
open Expecto
open Swensen.Unquote
open Microsoft.Extensions.Configuration

let private buildConfig
    (values: (string * string) list) =
    ConfigurationBuilder()
        .AddInMemoryCollection(
            values
            |> List.map KeyValuePair.Create
            :> IEnumerable<KeyValuePair<string, string>>)
        .Build()
    :> IConfiguration

let private validConfig overrides =
    let defaults = [
        "GoogleOAuth:ClientId",     "client-123"
        "GoogleOAuth:ClientSecret", "secret-456"
        "GoogleOAuth:RedirectUri",
            "https://example.com/auth/callback"
        "GoogleOAuth:AllowedEmail", "user@example.com"
    ]
    let merged =
        defaults
        |> List.map (fun (k, v) ->
            match overrides
                  |> List.tryFind (fst >> (=) k) with
            | Some (_, ov) -> (k, ov)
            | None         -> (k, v))
    buildConfig merged

[<Tests>]
let tests = testList "Config" [

    testList "loadGoogleOAuth" [

        testCase "loads valid config" <| fun () ->
            let oauth =
                validConfig [] |> Config.loadGoogleOAuth
            test <@ oauth.ClientId = "client-123" @>
            test <@ oauth.ClientSecret = "secret-456" @>
            test <@ oauth.RedirectUri =
                "https://example.com/auth/callback" @>
            test <@ oauth.AllowedEmail =
                "user@example.com" @>

        testCase "fails on missing ClientId" <| fun () ->
            let cfg = buildConfig [
                "GoogleOAuth:ClientSecret", "secret"
                "GoogleOAuth:RedirectUri",
                    "https://x.com/cb"
                "GoogleOAuth:AllowedEmail", "a@b.com"
            ]
            Expect.throws
                (fun () ->
                    Config.loadGoogleOAuth cfg |> ignore)
                "Missing ClientId"

        testCase "fails on empty ClientSecret" <| fun () ->
            let cfg =
                validConfig [
                    "GoogleOAuth:ClientSecret", "" ]
            Expect.throws
                (fun () ->
                    Config.loadGoogleOAuth cfg |> ignore)
                "Empty ClientSecret"

        testCase "fails on HTTP redirect URI" <| fun () ->
            let cfg =
                validConfig [
                    "GoogleOAuth:RedirectUri",
                    "http://example.com/cb" ]
            Expect.throws
                (fun () ->
                    Config.loadGoogleOAuth cfg |> ignore)
                "HTTP not allowed"

        testCase "fails on invalid URI" <| fun () ->
            let cfg =
                validConfig [
                    "GoogleOAuth:RedirectUri",
                    "not-a-url" ]
            Expect.throws
                (fun () ->
                    Config.loadGoogleOAuth cfg |> ignore)
                "Invalid URI"

        testCase "fails on invalid email" <| fun () ->
            let cfg =
                validConfig [
                    "GoogleOAuth:AllowedEmail",
                    "not-an-email" ]
            Expect.throws
                (fun () ->
                    Config.loadGoogleOAuth cfg |> ignore)
                "Invalid email"

        testCase "accepts valid HTTPS URI" <| fun () ->
            let oauth =
                validConfig [
                    "GoogleOAuth:RedirectUri",
                    "https://myapp.com/cb" ]
                |> Config.loadGoogleOAuth
            test <@ oauth.RedirectUri =
                "https://myapp.com/cb" @>

        testCase "accepts valid email" <| fun () ->
            let oauth =
                validConfig [
                    "GoogleOAuth:AllowedEmail",
                    "test@domain.org" ]
                |> Config.loadGoogleOAuth
            test <@ oauth.AllowedEmail =
                "test@domain.org" @>
    ]
]
