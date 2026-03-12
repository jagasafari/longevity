module StorageTests

open System
open Expecto
open Swensen.Unquote

let private fakeUrl name = $"https://fake.blob.core.windows.net/photos/{name}?sas=token"
let private fakeThumbnailUrl name = $"https://fake.blob.core.windows.net/thumbnails/{name}?sas=token"

let private blob name (year, month, day) =
    name, DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero)

[<Tests>]
let tests = testList "Storage" [

    testList "selectRecent" [

        testCase "returns requested count" <| fun () ->
            let blobs = [
                blob "a.jpg" (2026, 1, 1)
                blob "b.jpg" (2026, 1, 2)
                blob "c.jpg" (2026, 1, 3)
            ]
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl blobs 2
            test <@ result.Length = 2 @>

        testCase "sorted newest first" <| fun () ->
            let blobs = [
                blob "old.jpg"    (2025, 6, 1)
                blob "newest.jpg" (2026, 3, 9)
                blob "mid.jpg"    (2026, 1, 15)
            ]
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl blobs 3
            let names = result |> Array.map (fun p -> p.Name)
            test <@ names = [| "newest.jpg"; "mid.jpg"; "old.jpg" |] @>

        testCase "truncates to count" <| fun () ->
            let blobs = [
                blob "a.jpg" (2026, 1, 1)
                blob "b.jpg" (2026, 1, 2)
                blob "c.jpg" (2026, 1, 3)
                blob "d.jpg" (2026, 1, 4)
                blob "e.jpg" (2026, 1, 5)
            ]
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl blobs 3
            let names = result |> Array.map (fun p -> p.Name)
            test <@ names = [| "e.jpg"; "d.jpg"; "c.jpg" |] @>

        testCase "empty input returns empty" <| fun () ->
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl [] 10
            test <@ result.Length = 0 @>

        testCase "count zero returns empty" <| fun () ->
            let blobs = [ blob "a.jpg" (2026, 1, 1) ]
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl blobs 0
            test <@ result.Length = 0 @>

        testCase "count larger than input returns all" <| fun () ->
            let blobs = [
                blob "a.jpg" (2026, 1, 1)
                blob "b.jpg" (2026, 1, 2)
            ]
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl blobs 100
            test <@ result.Length = 2 @>

        testCase "uses toUrl for each blob" <| fun () ->
            let blobs = [ blob "photo.jpg" (2026, 1, 1) ]
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl blobs 1
            test <@ result[0].Url = fakeUrl "photo.jpg" @>
            test <@ result[0].ThumbnailUrl = fakeThumbnailUrl "photo.jpg" @>

        testCase "preserves LastModified" <| fun () ->
            let dt = DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero)
            let blobs = [ "x.jpg", dt ]
            let result = Storage.selectRecent fakeUrl fakeThumbnailUrl blobs 1
            test <@ result[0].LastModified = dt @>
    ]
]
