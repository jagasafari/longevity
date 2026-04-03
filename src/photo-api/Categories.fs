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
            "SELECT group_id, category_id FROM photo_group_categories")
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
            """INSERT INTO photo_group_categories (group_id, category_id)
               VALUES (@g, @c) ON CONFLICT DO NOTHING""",
            {| g = groupId; c = catId |})
    ()
}

let removeCategory (connStr: string) (groupId: string) (categoryId: int) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM photo_group_categories WHERE group_id = @g AND category_id = @c",
            {| g = groupId; c = categoryId |})
    ()
}

let photoNamesForCategory (connStr: string) (categoryId: int) = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<string>(
            """WITH RECURSIVE cat_groups AS (
                   SELECT gc.group_id
                   FROM photo_group_categories gc
                   WHERE gc.category_id = @c
                   UNION
                   SELECT pg.group_id
                   FROM photo_groups pg
                   JOIN cat_groups cg ON pg.parent_group_id = cg.group_id
               )
               SELECT DISTINCT m.photo_name
               FROM cat_groups cg
               JOIN photo_group_members m ON m.group_id = cg.group_id""",
            {| c = categoryId |})
    return rows |> Seq.toArray |> Set.ofArray
}
