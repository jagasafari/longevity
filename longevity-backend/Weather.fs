module Weather

open System

type Forecast =
    { Date: DateOnly
      TemperatureC: int
      Summary: string }

let private summaries =
    [| "Freezing"; "Bracing"; "Chilly"
       "Cool"; "Mild"; "Warm"
       "Balmy"; "Hot"; "Sweltering"; "Scorching" |]

let generate randTemp randSummary (today: DateOnly) count =
    [| for i in 1..count ->
        { Date         = today.AddDays i
          TemperatureC = randTemp ()
          Summary      = summaries[randSummary ()] } |]

let generateRandom =
    generate
        (fun () -> Random.Shared.Next(-20, 55))
        (fun () -> Random.Shared.Next(summaries.Length))
