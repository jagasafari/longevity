module RoutesTests

open System
open System.Threading.Tasks
open Expecto
open Swensen.Unquote
open Microsoft.AspNetCore.Http

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

    testList "deletePhoto" [

        testCase "returns 204 when blob deleted" <| fun () ->
            let delete _ = Task.FromResult true
            let result = (Routes.deletePhoto delete "photo.jpg").Result
            test <@ result :? IStatusCodeHttpResult @>
            let status = (result :?> IStatusCodeHttpResult).StatusCode
            test <@ status = Nullable 204 @>

        testCase "returns 404 when blob not found" <| fun () ->
            let delete _ = Task.FromResult false
            let result = (Routes.deletePhoto delete "missing.jpg").Result
            test <@ result :? IStatusCodeHttpResult @>
            let status = (result :?> IStatusCodeHttpResult).StatusCode
            test <@ status = Nullable 404 @>

        testCase "passes blob name to delete function" <| fun () ->
            let mutable captured = ""
            let delete name =
                captured <- name
                Task.FromResult true
            (Routes.deletePhoto delete "my-photo.jpg").Result |> ignore
            test <@ captured = "my-photo.jpg" @>
    ]
]
