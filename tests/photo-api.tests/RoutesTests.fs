module RoutesTests

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Swensen.Unquote
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SignalR

type StubClientProxy() =
    interface IClientProxy with
        member _.SendCoreAsync(_method: string, _args: obj array, _cancellationToken: CancellationToken) =
            Task.CompletedTask

type StubSingleClientProxy() =
    inherit StubClientProxy()
    interface ISingleClientProxy with
        member _.InvokeCoreAsync<'T>(_method: string, _args: obj array, _cancellationToken: CancellationToken) : Task<'T> =
            Task.FromResult(Unchecked.defaultof<'T>)

type StubHubClients() =
    let proxy = StubClientProxy() :> IClientProxy
    let single = StubSingleClientProxy()
    interface IHubClients<IClientProxy> with
        member _.All = proxy
        member _.AllExcept _  = proxy
        member _.Client _     = proxy
        member _.Clients _    = proxy
        member _.Group _      = proxy
        member _.Groups _     = proxy
        member _.GroupExcept(_, _) = proxy
        member _.User _       = proxy
        member _.Users _      = proxy
    interface IHubClients with
        member _.Client(_connectionId: string) = single :> ISingleClientProxy

type StubHubContext() =
    interface IHubContext<PhotoHub.PhotoHub> with
        member _.Clients = StubHubClients() :> IHubClients
        member _.Groups  = Unchecked.defaultof<IGroupManager>

let stubHub = StubHubContext() :> IHubContext<PhotoHub.PhotoHub>

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
            let removeFromGroups _ = Task.CompletedTask
            let result = (Routes.deletePhoto delete removeFromGroups stubHub "photo.jpg").Result
            test <@ result :? IStatusCodeHttpResult @>
            let status = (result :?> IStatusCodeHttpResult).StatusCode
            test <@ status = Nullable 204 @>

        testCase "returns 404 when blob not found" <| fun () ->
            let delete _ = Task.FromResult false
            let removeFromGroups _ = Task.CompletedTask
            let result = (Routes.deletePhoto delete removeFromGroups stubHub "missing.jpg").Result
            test <@ result :? IStatusCodeHttpResult @>
            let status = (result :?> IStatusCodeHttpResult).StatusCode
            test <@ status = Nullable 404 @>

        testCase "passes blob name to delete function" <| fun () ->
            let delete name =
                test <@ name = "my-photo.jpg" @>
                Task.FromResult true
            let removeFromGroups _ = Task.CompletedTask
            (Routes.deletePhoto delete removeFromGroups stubHub "my-photo.jpg").Result |> ignore

        testCase "returns 400 when blob name missing" <| fun () ->
            let delete _ = failwith "delete should not be called for missing name"
            let removeFromGroups _ = Task.CompletedTask
            let result = (Routes.deletePhoto delete removeFromGroups stubHub "").Result
            test <@ result :? IStatusCodeHttpResult @>
            let status = (result :?> IStatusCodeHttpResult).StatusCode
            test <@ status = Nullable 400 @>

        testCase "returns 400 when blob name null" <| fun () ->
            let delete _ = failwith "delete should not be called for null name"
            let removeFromGroups _ = Task.CompletedTask
            let result = (Routes.deletePhoto delete removeFromGroups stubHub null).Result
            test <@ result :? IStatusCodeHttpResult @>
            let status = (result :?> IStatusCodeHttpResult).StatusCode
            test <@ status = Nullable 400 @>
    ]

    testList "movePhotoToGroup" [

        testCase "returns 204 on success" <| fun () ->
            let move _ _ = Task.CompletedTask
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = "a.jpg"; TargetGroupId = "group-1" }
            let result = (Routes.movePhotoToGroup move stubHub req).Result
            test <@ result :? IStatusCodeHttpResult @>
            test <@ (result :?> IStatusCodeHttpResult).StatusCode = Nullable 204 @>

        testCase "passes correct photo name and group id to move function" <| fun () ->
            let mutable calledWith = ("", "")
            let move photo groupId =
                calledWith <- (photo, groupId)
                Task.CompletedTask
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = "photo.jpg"; TargetGroupId = "grp-42" }
            (Routes.movePhotoToGroup move stubHub req).Result |> ignore
            test <@ calledWith = ("photo.jpg", "grp-42") @>

        testCase "trims whitespace from inputs" <| fun () ->
            let mutable calledWith = ("", "")
            let move photo groupId =
                calledWith <- (photo, groupId)
                Task.CompletedTask
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = "  photo.jpg  "; TargetGroupId = "  grp-1  " }
            (Routes.movePhotoToGroup move stubHub req).Result |> ignore
            test <@ calledWith = ("photo.jpg", "grp-1") @>

        testCase "returns 400 when photo name is empty" <| fun () ->
            let move _ _ = failwith "should not be called"
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = ""; TargetGroupId = "grp-1" }
            let result = (Routes.movePhotoToGroup move stubHub req).Result
            test <@ result :? IStatusCodeHttpResult @>
            test <@ (result :?> IStatusCodeHttpResult).StatusCode = Nullable 400 @>

        testCase "returns 400 when photo name is whitespace" <| fun () ->
            let move _ _ = failwith "should not be called"
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = "   "; TargetGroupId = "grp-1" }
            let result = (Routes.movePhotoToGroup move stubHub req).Result
            test <@ result :? IStatusCodeHttpResult @>
            test <@ (result :?> IStatusCodeHttpResult).StatusCode = Nullable 400 @>

        testCase "returns 400 when group id is empty" <| fun () ->
            let move _ _ = failwith "should not be called"
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = "photo.jpg"; TargetGroupId = "" }
            let result = (Routes.movePhotoToGroup move stubHub req).Result
            test <@ result :? IStatusCodeHttpResult @>
            test <@ (result :?> IStatusCodeHttpResult).StatusCode = Nullable 400 @>

        testCase "returns 404 when group does not exist (FK violation)" <| fun () ->
            let move _ _ =
                let ex = Npgsql.PostgresException("", "", "", "23503")
                raise ex
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = "photo.jpg"; TargetGroupId = "missing-grp" }
            let result = (Routes.movePhotoToGroup move stubHub req).Result
            test <@ result :? IStatusCodeHttpResult @>
            test <@ (result :?> IStatusCodeHttpResult).StatusCode = Nullable 404 @>

        testCase "returns 503 on other postgres error" <| fun () ->
            let move _ _ =
                let ex = Npgsql.PostgresException("", "", "", "58000")
                raise ex
            let req = { Routes.MovePhotoToGroupRequest.PhotoName = "photo.jpg"; TargetGroupId = "grp-1" }
            let result = (Routes.movePhotoToGroup move stubHub req).Result
            test <@ result :? IStatusCodeHttpResult @>
            test <@ (result :?> IStatusCodeHttpResult).StatusCode = Nullable 503 @>
    ]
]
