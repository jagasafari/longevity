module PhotoGroups

open System
open System.Collections.Generic
open System.Data
open Dapper
open Npgsql

[<CLIMutable>]
type private GroupPhoto = { group_id: string; photo_name: string }

type internal GroupChange =
    | NoChange
    | MergeGroups of sourceGroupId: string * targetGroupId: string
    | AddPhotoToGroup of groupId: string * photoName: string
    | CreateGroup of sourceName: string * targetName: string

let internal planGroupChange sourceGroup targetGroup sourceName targetName =
    match sourceGroup, targetGroup with
    | Some sg, Some tg when sg = tg -> NoChange
    | Some sg, Some tg -> MergeGroups (sg, tg)
    | Some sg, None -> AddPhotoToGroup (sg, targetName)
    | None, Some tg -> AddPhotoToGroup (tg, sourceName)
    | None, None -> CreateGroup (sourceName, targetName)

let internal shouldDeleteGroupAfterRemoval remainingCount = remainingCount <= 1

let private withTransaction connStr work = task {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync()
    use! tx = conn.BeginTransactionAsync(IsolationLevel.Serializable)
    try
        let! result = work conn tx
        do! tx.CommitAsync()
        return result
    with ex ->
        do! tx.RollbackAsync()
        return raise ex
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

let private findGroupId (conn: NpgsqlConnection) (tx: NpgsqlTransaction) (photo: string) = task {
    let! result =
        conn.ExecuteScalarAsync<string>(
            "SELECT group_id FROM group_photos WHERE photo_name = @p LIMIT 1",
            {| p = photo |},
            tx)
    return Option.ofObj result
}

let private insertPhoto (conn: NpgsqlConnection) (tx: NpgsqlTransaction) gid photo = task {
    let! _ =
        conn.ExecuteAsync(
            "INSERT INTO group_photos (group_id, photo_name) VALUES (@g, @p)
             ON CONFLICT DO NOTHING",
            {| g = gid; p = photo |},
            tx)
    ()
}

let private movePhotos (conn: NpgsqlConnection) (tx: NpgsqlTransaction) fromGid toGid = task {
    let! _ =
        conn.ExecuteAsync(
            """UPDATE group_photos SET group_id = @t
               WHERE group_id = @f
               AND photo_name NOT IN
                   (SELECT photo_name FROM group_photos WHERE group_id = @t)""",
            {| t = toGid; f = fromGid |},
            tx)
    ()
}

let private deleteSingletonOrEmpty (conn: NpgsqlConnection) (tx: NpgsqlTransaction) gid = task {
    let! _ =
        conn.ExecuteAsync(
            """DELETE FROM group_photos WHERE group_id = @g
               AND (SELECT COUNT(*) FROM group_photos WHERE group_id = @g) <= 1""",
            {| g = gid |},
            tx)
    ()
}

let private deletePhoto (conn: NpgsqlConnection) (tx: NpgsqlTransaction) blobName = task {
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM group_photos WHERE photo_name = @p",
            {| p = blobName |},
            tx)
    ()
}

let groupPhotos (connStr: string) (sourceName: string) (targetName: string) =
    withTransaction connStr <| fun conn tx -> task {
        let! sourceGroup = findGroupId conn tx sourceName
        let! targetGroup = findGroupId conn tx targetName
        match planGroupChange sourceGroup targetGroup sourceName targetName with
        | NoChange -> ()
        | MergeGroups (sourceGroupId, targetGroupId) ->
            do! movePhotos conn tx sourceGroupId targetGroupId
            do! deleteSingletonOrEmpty conn tx sourceGroupId
        | AddPhotoToGroup (groupId, photoName) ->
            do! insertPhoto conn tx groupId photoName
        | CreateGroup (source, target) ->
            let groupId = Guid.NewGuid().ToString("N")
            do! insertPhoto conn tx groupId source
            do! insertPhoto conn tx groupId target
    }

let removePhotoFromGroups (connStr: string) (blobName: string) =
    withTransaction connStr <| fun conn tx -> task {
        let! gid = findGroupId conn tx blobName
        match gid with
        | None -> ()
        | Some g ->
            do! deletePhoto conn tx blobName
            do! deleteSingletonOrEmpty conn tx g
    }
