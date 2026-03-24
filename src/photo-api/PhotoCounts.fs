module PhotoCounts

open System
open Dapper
open Npgsql

[<CLIMutable>]
type private PhotoCountRow = { date: DateTime; count: int }

type PhotoCount = { date: DateOnly; count: int }

let listPhotoCounts (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows = conn.QueryAsync<PhotoCountRow>("SELECT date, count FROM photo_counts ORDER BY date")
    return rows |> Seq.map (fun r -> { date = DateOnly.FromDateTime(r.date); count = r.count }) |> Seq.toArray
}
