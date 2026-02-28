module WeatherTests

open System
open Expecto
open Swensen.Unquote

[<Tests>]
let tests = testList "Weather" [

    testList "generate" [

        testCase "returns requested count" <| fun () ->
            let today = DateOnly(2026, 3, 1)
            let result =
                Weather.generate
                    (fun () -> 20) (fun () -> 0)
                    today 3
            test <@ result.Length = 3 @>

        testCase "dates are consecutive from tomorrow" <| fun () ->
            let today = DateOnly(2026, 3, 1)
            let result =
                Weather.generate
                    (fun () -> 20) (fun () -> 0)
                    today 3
            let dates =
                result |> Array.map (fun f -> f.Date)
            test <@ dates = [|
                DateOnly(2026, 3, 2)
                DateOnly(2026, 3, 3)
                DateOnly(2026, 3, 4) |] @>

        testCase "uses injected temperature" <| fun () ->
            let today = DateOnly(2026, 3, 1)
            let result =
                Weather.generate
                    (fun () -> 42) (fun () -> 0)
                    today 2
            let temps =
                result
                |> Array.map (fun f -> f.TemperatureC)
            test <@ temps = [| 42; 42 |] @>

        testCase "uses injected summary index" <| fun () ->
            let today = DateOnly(2026, 3, 1)
            let result =
                Weather.generate
                    (fun () -> 10) (fun () -> 5)
                    today 1
            test <@ result[0].Summary = "Warm" @>

        testCase "empty for count zero" <| fun () ->
            let today = DateOnly(2026, 3, 1)
            let result =
                Weather.generate
                    (fun () -> 0) (fun () -> 0)
                    today 0
            test <@ result.Length = 0 @>
    ]

    testList "generateRandom" [

        testCase "returns requested count" <| fun () ->
            let today = DateOnly(2026, 3, 1)
            let result = Weather.generateRandom today 5
            test <@ result.Length = 5 @>

        testCase "temperatures in valid range" <| fun () ->
            let today = DateOnly(2026, 3, 1)
            let result = Weather.generateRandom today 100
            let allValid =
                result
                |> Array.forall (fun f ->
                    f.TemperatureC >= -20
                    && f.TemperatureC < 55)
            test <@ allValid @>
    ]
]
