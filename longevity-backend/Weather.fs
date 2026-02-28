module Weather

open System

// ── Pure domain ──────────────────────────

type Forecast =
    { Date: DateOnly
      TemperatureC: int
      Summary: string }

let toFahrenheit c = 32 + int (float c / 0.5556)

// ── Pure generation (inject randomness) ──

let summaries =
    [| "Freezing"; "Bracing"; "Chilly"
       "Cool"; "Mild"; "Warm"
       "Balmy"; "Hot"; "Sweltering"; "Scorching" |]

let generate (today: DateOnly) count randTemp randSummary =
    [| for i in 1..count ->
        { Date         = today.AddDays i
          TemperatureC = randTemp ()
          Summary      = summaries[randSummary ()] } |]

// ── Impure convenience (Random) ──────────

let generateRandom today count =
    generate
        today count
        (fun () -> Random.Shared.Next(-20, 55))
        (fun () -> Random.Shared.Next(summaries.Length))
