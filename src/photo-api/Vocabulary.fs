module Vocabulary

open Dapper
open Npgsql

let listGroupIds (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<string>(
            "SELECT group_id FROM vocabulary.group_members ORDER BY added_at")
    return rows |> Seq.toArray
}

let addGroup (connStr: string) (groupId: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            "INSERT INTO vocabulary.group_members (group_id) VALUES (@g) ON CONFLICT DO NOTHING",
            {| g = groupId |})
    ()
}

let removeGroup (connStr: string) (groupId: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM vocabulary.group_members WHERE group_id = @g",
            {| g = groupId |})
    ()
}
