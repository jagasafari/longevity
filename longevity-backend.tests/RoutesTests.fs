module RoutesTests

open System
open Expecto
open Swensen.Unquote

[<Tests>]
let tests = testList "Routes" [

    testList "redirectUrl" [

        testCase "Authorized → root" <| fun () ->
            let url = Routes.redirectUrl (Auth.Authorized (Auth.Email "a@b.com"))
            test <@ url = "/" @>

        testCase "Denied → error with reason" <| fun () ->
            let url = Routes.redirectUrl (Auth.Denied "not allowed")
            let expected = Uri.EscapeDataString "not allowed"
            test <@ url = $"/?error={expected}" @>

        testCase "Error → error with message" <| fun () ->
            let url = Routes.redirectUrl (Auth.Error "token failed")
            let expected = Uri.EscapeDataString "token failed"
            test <@ url = $"/?error={expected}" @>

        testCase "Denied escapes special chars" <| fun () ->
            let reason = "Email bad@x.com not ok"
            let url = Routes.redirectUrl (Auth.Denied reason)
            test <@ url.Contains("bad%40x.com") @>

        testCase "Error escapes ampersand" <| fun () ->
            let msg = "a&b=c"
            let url = Routes.redirectUrl (Auth.Error msg)
            test <@ not (url.Contains("&b=")) @>
    ]
]
