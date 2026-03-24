module Categories

open Dapper
open Npgsql

[<CLIMutable>]
type Category = { id: int; name: string }

[<CLIMutable>]
type private GroupCategory = { group_id: string; category_id: int }

let listCategories (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows = conn.QueryAsync<Category>("SELECT id, name FROM categories ORDER BY name")
    return rows |> Seq.toArray
}

let listGroupCategories (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<GroupCategory>(
            "SELECT group_id, category_id FROM group_categories")
    let dict = System.Collections.Generic.Dictionary<string, int[]>()
    let grouped = rows |> Seq.groupBy (fun r -> r.group_id)
    for (gid, cats) in grouped do
        dict[gid] <- cats |> Seq.map (fun c -> c.category_id) |> Seq.toArray
    return dict
}

let assignCategory (connStr: string) (groupId: string) (categoryName: string) = task {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync()
    let! catId =
        conn.ExecuteScalarAsync<int>(
            """INSERT INTO categories (name) VALUES (@n)
               ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name
               RETURNING id""",
            {| n = categoryName.Trim() |})
    let! _ =
        conn.ExecuteAsync(
            """INSERT INTO group_categories (group_id, category_id)
               VALUES (@g, @c) ON CONFLICT DO NOTHING""",
            {| g = groupId; c = catId |})
    ()
}

let removeCategory (connStr: string) (groupId: string) (categoryId: int) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM group_categories WHERE group_id = @g AND category_id = @c",
            {| g = groupId; c = categoryId |})
    ()
}
