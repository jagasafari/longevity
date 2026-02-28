module AuthTests

open System
open System.Text.Json
open Expecto
open Swensen.Unquote

[<Tests>]
let tests = testList "Auth" [

    testList "buildLoginUrl" [

        testCase "contains client_id" <| fun () ->
            let url =
                AuthLogin.buildLoginUrl
                    (Auth.ClientId "my-id")
                    (Auth.HttpsUri "https://x.com/cb")
            test <@ url.Contains("client_id=my-id") @>

        testCase "encodes redirect_uri" <| fun () ->
            let uri = "https://example.com/auth/callback"
            let url =
                AuthLogin.buildLoginUrl
                    (Auth.ClientId "id")
                    (Auth.HttpsUri uri)
            let encoded = Uri.EscapeDataString uri
            test <@ url.Contains(
                $"redirect_uri={encoded}") @>

        testCase "includes response_type code" <| fun () ->
            let url =
                AuthLogin.buildLoginUrl
                    (Auth.ClientId "id")
                    (Auth.HttpsUri "https://x.com/cb")
            test <@ url.Contains("response_type=code") @>

        testCase "includes openid email scope" <| fun () ->
            let url =
                AuthLogin.buildLoginUrl
                    (Auth.ClientId "id")
                    (Auth.HttpsUri "https://x.com/cb")
            let encoded =
                Uri.EscapeDataString "openid email"
            test <@ url.Contains($"scope={encoded}") @>

        testCase "starts with Google auth URL" <| fun () ->
            let url =
                AuthLogin.buildLoginUrl
                    (Auth.ClientId "id")
                    (Auth.HttpsUri "https://x.com/cb")
            let prefix =
                "https://accounts.google.com/o/oauth2"
            test <@ url.StartsWith(prefix) @>

        testCase "includes access_type offline" <| fun () ->
            let url =
                AuthLogin.buildLoginUrl
                    (Auth.ClientId "id")
                    (Auth.HttpsUri "https://x.com/cb")
            test <@ url.Contains("access_type=offline") @>

        testCase "includes prompt consent" <| fun () ->
            let url =
                AuthLogin.buildLoginUrl
                    (Auth.ClientId "id")
                    (Auth.HttpsUri "https://x.com/cb")
            test <@ url.Contains("prompt=consent") @>
    ]

    testList "authorize" [

        testCase "Authorized when email matches" <| fun () ->
            let result =
                AuthCallback.authorize
                    (Auth.Email "me@example.com")
                    (Auth.Email "me@example.com")
            test <@ result =
                Auth.Authorized
                    (Auth.Email "me@example.com") @>

        testCase "Denied when email differs" <| fun () ->
            let result =
                AuthCallback.authorize
                    (Auth.Email "me@example.com")
                    (Auth.Email "other@example.com")
            match result with
            | Auth.Denied reason ->
                test <@ reason.Contains(
                    "other@example.com") @>
            | _ -> failtest "Expected Denied"
    ]

    testList "buildQuery" [

        testCase "builds key=value pairs" <| fun () ->
            let result = Auth.buildQuery [
                "a", "1"; "b", "2" ]
            test <@ result = "a=1&b=2" @>

        testCase "escapes values" <| fun () ->
            let result =
                Auth.buildQuery [ "q", "hello world" ]
            test <@ result = "q=hello%20world" @>

        testCase "empty list → empty string"
            <| fun () ->
            let result = Auth.buildQuery []
            test <@ result = "" @>

        testCase "escapes special characters"
            <| fun () ->
            let result =
                Auth.buildQuery [ "u", "a@b.com" ]
            test <@ result.Contains("%40") @>
    ]

    testList "jsonProp" [

        testCase "extracts existing property" <| fun () ->
            use doc =
                JsonDocument.Parse """{"name":"Alice"}"""
            let result = Auth.jsonProp "name" doc
            test <@ result = Ok "Alice" @>

        testCase "Error for missing property" <| fun () ->
            use doc =
                JsonDocument.Parse """{"a":"b"}"""
            let result = Auth.jsonProp "name" doc
            match result with
            | Error msg -> test <@ msg.Contains("name") @>
            | _ -> failtest "Expected Error"
    ]
]
