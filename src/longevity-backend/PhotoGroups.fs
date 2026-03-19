module PhotoGroups

open System
open System.Collections.Generic
open Dapper
open Npgsql

[<CLIMutable>]
type private GroupPhoto = { group_id: string; photo_name: string }

let initSchema (connStr: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ = conn.ExecuteAsync("""
        ALTER TABLE IF EXISTS group_photos
            DROP CONSTRAINT IF EXISTS group_photos_group_id_fkey;

        DROP TABLE IF EXISTS photo_groups;

        CREATE TABLE IF NOT EXISTS group_photos (
            group_id   TEXT NOT NULL,
            photo_name TEXT NOT NULL,
            PRIMARY KEY (group_id, photo_name)
        );
        CREATE INDEX IF NOT EXISTS idx_group_photos_photo
            ON group_photos (photo_name);
    """)
    ()
}

let listPhotoGroups (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<GroupPhoto>(
            "SELECT group_id, photo_name FROM group_photos ORDER BY group_id")
    let dict = Dictionary<string, ResizeArray<string>>()
    for row in rows do
        match dict.TryGetValue row.group_id with
        | true, list -> list.Add row.photo_name
        | false, _ ->
            let list = ResizeArray()
            list.Add row.photo_name
            dict[row.group_id] <- list
    let result = Dictionary<string, string[]>()
    for kv in dict do
        result[kv.Key] <- kv.Value.ToArray()
    return result
}

let private findGroupId (conn: NpgsqlConnection) (photo: string) = task {
    let! result =
        conn.ExecuteScalarAsync<string>(
            "SELECT group_id FROM group_photos WHERE photo_name = @p",
            {| p = photo |})
    return Option.ofObj result
}

let private insertPhoto (conn: NpgsqlConnection) gid photo = task {
    let! _ =
        conn.ExecuteAsync(
            "INSERT INTO group_photos (group_id, photo_name) VALUES (@g, @p)
             ON CONFLICT DO NOTHING",
            {| g = gid; p = photo |})
    ()
}

let private movePhotos (conn: NpgsqlConnection) fromGid toGid = task {
    let! _ =
        conn.ExecuteAsync(
            """UPDATE group_photos SET group_id = @t
               WHERE group_id = @f
               AND photo_name NOT IN
                   (SELECT photo_name FROM group_photos WHERE group_id = @t)""",
            {| t = toGid; f = fromGid |})
    ()
}

let private deleteSingletonOrEmpty (conn: NpgsqlConnection) gid = task {
    let! _ =
        conn.ExecuteAsync(
            """DELETE FROM group_photos WHERE group_id = @g
               AND (SELECT COUNT(*) FROM group_photos WHERE group_id = @g) <= 1""",
            {| g = gid |})
    ()
}

let groupPhotos (connStr: string) (sourceName: string) (targetName: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! sourceGroup = findGroupId conn sourceName
    let! targetGroup = findGroupId conn targetName
    match sourceGroup, targetGroup with
    | Some sg, Some tg when sg = tg -> ()
    | Some sg, Some tg ->
        do! movePhotos conn sg tg
        do! deleteSingletonOrEmpty conn sg
    | Some sg, None ->
        do! insertPhoto conn sg targetName
    | None, Some tg ->
        do! insertPhoto conn tg sourceName
    | None, None ->
        let gid = Guid.NewGuid().ToString("N")
        do! insertPhoto conn gid sourceName
        do! insertPhoto conn gid targetName
}

let removePhotoFromGroups (connStr: string) (blobName: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! gid = findGroupId conn blobName
    match gid with
    | None -> ()
    | Some g ->
        let! _ =
            conn.ExecuteAsync(
                "DELETE FROM group_photos WHERE photo_name = @p",
                {| p = blobName |})
        do! deleteSingletonOrEmpty conn g
}
