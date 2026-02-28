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
                Auth.buildLoginUrl "my-id"
                    "https://x.com/cb"
            test <@ url.Contains("client_id=my-id") @>

        testCase "encodes redirect_uri" <| fun () ->
            let uri = "https://example.com/auth/callback"
            let url = Auth.buildLoginUrl "id" uri
            let encoded = Uri.EscapeDataString uri
            test <@ url.Contains(
                $"redirect_uri={encoded}") @>

        testCase "includes response_type code" <| fun () ->
            let url =
                Auth.buildLoginUrl "id" "https://x.com/cb"
            test <@ url.Contains("response_type=code") @>

        testCase "includes openid email scope" <| fun () ->
            let url =
                Auth.buildLoginUrl "id" "https://x.com/cb"
            let encoded =
                Uri.EscapeDataString "openid email"
            test <@ url.Contains($"scope={encoded}") @>

        testCase "starts with Google auth URL" <| fun () ->
            let url =
                Auth.buildLoginUrl "id" "https://x.com/cb"
            let prefix =
                "https://accounts.google.com/o/oauth2"
            test <@ url.StartsWith(prefix) @>

        testCase "includes access_type offline" <| fun () ->
            let url =
                Auth.buildLoginUrl "id" "https://x.com/cb"
            test <@ url.Contains("access_type=offline") @>

        testCase "includes prompt consent" <| fun () ->
            let url =
                Auth.buildLoginUrl "id" "https://x.com/cb"
            test <@ url.Contains("prompt=consent") @>
    ]

    testList "authorize" [

        testCase "Authorized when email matches" <| fun () ->
            let result =
                Auth.authorize
                    "me@example.com" "me@example.com"
            test <@ result =
                Auth.Authorized "me@example.com" @>

        testCase "Denied when email differs" <| fun () ->
            let result =
                Auth.authorize
                    "me@example.com" "other@example.com"
            match result with
            | Auth.Denied reason ->
                test <@ reason.Contains(
                    "other@example.com") @>
            | _ -> failtest "Expected Denied"
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
