module GroupNames

open Dapper
open Npgsql

[<CLIMutable>]
type private GroupName = { group_id: string; name: string }

let listNames (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<string>(
            "SELECT DISTINCT name FROM photo_group_names ORDER BY name")
    return rows |> Seq.toArray
}

let listGroupNames (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<GroupName>(
            "SELECT group_id, name FROM photo_group_names")
    let dict = System.Collections.Generic.Dictionary<string, string[]>()
    let grouped = rows |> Seq.groupBy (fun r -> r.group_id)
    for (gid, names) in grouped do
        dict[gid] <- names |> Seq.map (fun n -> n.name) |> Seq.toArray
    return dict
}

let assignName (connStr: string) (groupId: string) (name: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            """INSERT INTO photo_group_names (group_id, name)
               VALUES (@g, @n) ON CONFLICT DO NOTHING""",
            {| g = groupId; n = name.Trim() |})
    ()
}

let removeName (connStr: string) (groupId: string) (name: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM photo_group_names WHERE group_id = @g AND name = @n",
            {| g = groupId; n = name |})
    ()
}

let photoNamesForGroupName (connStr: string) (name: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<string>(
            """WITH RECURSIVE named_groups AS (
                   SELECT gn.group_id
                   FROM photo_group_names gn
                   WHERE gn.name = @n
                   UNION
                   SELECT pg.group_id
                   FROM photo_groups pg
                   JOIN named_groups ng ON pg.parent_group_id = ng.group_id
               )
               SELECT DISTINCT m.photo_name
               FROM named_groups ng
               JOIN photo_group_members m ON m.group_id = ng.group_id""",
            {| n = name |})
    return rows |> Seq.toArray |> Set.ofArray
}
