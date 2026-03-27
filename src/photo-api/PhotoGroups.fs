module PhotoGroups

open System
open System.Collections.Generic
open System.Data
open Dapper
open Npgsql

[<CLIMutable>]
type private GroupPhoto = { group_id: string; photo_name: string }

[<CLIMutable>]
type private GroupRow = {
    group_id: string
    parent_group_id: string // null when root
}

[<CLIMutable>]
type GroupTreeNode = {
    GroupId: string
    ParentGroupId: string option
    Photos: string[]
}

type internal GroupChange =
    | NoChange
    | MergeGroups of sourceGroupId: string * targetGroupId: string
    | AddPhotoToGroup of groupId: string * photoName: string
    | CreateGroup of sourceName: string * targetName: string
    | CreateSubgroup of
        parentGroupId: string
        * sourceName: string
        * targetName: string

let internal planGroupChange sourceGroup targetGroup sourceName targetName =
    match sourceGroup, targetGroup with
    | Some sg, Some tg when sg = tg ->
        CreateSubgroup (sg, sourceName, targetName)
    | Some sg, Some tg -> MergeGroups (sg, tg)
    | Some sg, None -> AddPhotoToGroup (sg, targetName)
    | None, Some tg -> AddPhotoToGroup (tg, sourceName)
    | None, None -> CreateGroup (sourceName, targetName)

let internal shouldDeleteGroupAfterRemoval photoCount childCount =
    childCount = 0 && photoCount <= 1

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

let listPhotoGroupTree (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! groups =
        conn.QueryAsync<GroupRow>(
            "SELECT group_id, parent_group_id FROM photo_groups")
    let! rows =
        conn.QueryAsync<GroupPhoto>(
            "SELECT group_id, photo_name FROM group_photos ORDER BY group_id")

    let photosByGroup = Dictionary<string, ResizeArray<string>>()
    for row in rows do
        match photosByGroup.TryGetValue row.group_id with
        | true, list -> list.Add row.photo_name
        | false, _ ->
            let list = ResizeArray()
            list.Add row.photo_name
            photosByGroup[row.group_id] <- list

    return
        groups
        |> Seq.map (fun g ->
            let photos =
                match photosByGroup.TryGetValue g.group_id with
                | true, names -> names.ToArray()
                | false, _ -> [||]

            {
                GroupId = g.group_id
                ParentGroupId = Option.ofObj g.parent_group_id
                Photos = photos
            })
        |> Seq.toArray
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

let private insertGroup
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid
    (parentGroupId: string option) =
    task {
        let! _ =
            conn.ExecuteAsync(
                """INSERT INTO photo_groups (group_id, parent_group_id)
                   VALUES (@g, @p)
                   ON CONFLICT (group_id) DO NOTHING""",
                {| g = gid; p = Option.toObj parentGroupId |},
                tx)
        ()
    }

let private movePhoto
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    targetGroupId
    photoName =
    task {
        let! _ =
            conn.ExecuteAsync(
                "UPDATE group_photos SET group_id = @g WHERE photo_name = @p",
                {| g = targetGroupId; p = photoName |},
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

let private moveChildren
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    fromGid
    toGid =
    task {
        let! _ =
            conn.ExecuteAsync(
                """UPDATE photo_groups
                   SET parent_group_id = @t
                   WHERE parent_group_id = @f""",
                {| f = fromGid; t = toGid |},
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

let private deleteGroup
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid =
    task {
        let! _ =
            conn.ExecuteAsync(
                "DELETE FROM photo_groups WHERE group_id = @g",
                {| g = gid |},
                tx)
        ()
    }

let private countPhotos
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid =
    task {
        let! count =
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM group_photos WHERE group_id = @g",
                {| g = gid |},
                tx)
        return count
    }

let private countChildren
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid =
    task {
        let! count =
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM photo_groups WHERE parent_group_id = @g",
                {| g = gid |},
                tx)
        return count
    }

let private removeOnlyPhoto
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid =
    task {
        let! _ =
            conn.ExecuteAsync(
                "DELETE FROM group_photos WHERE group_id = @g",
                {| g = gid |},
                tx)
        ()
    }

let private cleanupGroup
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid =
    task {
        let! photoCount = countPhotos conn tx gid
        let! childCount = countChildren conn tx gid
        if shouldDeleteGroupAfterRemoval photoCount childCount then
            if photoCount = 1 then
                do! removeOnlyPhoto conn tx gid
            do! deleteGroup conn tx gid
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
            do! moveChildren conn tx sourceGroupId targetGroupId
            do! cleanupGroup conn tx sourceGroupId
        | AddPhotoToGroup (groupId, photoName) ->
            do! insertPhoto conn tx groupId photoName
        | CreateGroup (source, target) ->
            let groupId = Guid.NewGuid().ToString("N")
            do! insertGroup conn tx groupId None
            do! insertPhoto conn tx groupId source
            do! insertPhoto conn tx groupId target
        | CreateSubgroup (parentGroupId, source, target) ->
            let subgroupId = Guid.NewGuid().ToString("N")
            do! insertGroup conn tx subgroupId (Some parentGroupId)
            do! movePhoto conn tx subgroupId source
            do! movePhoto conn tx subgroupId target
            do! cleanupGroup conn tx parentGroupId
    }

let movePhotoToGroup
    (connStr: string)
    (photoName: string)
    (targetGroupId: string) =
    withTransaction connStr <| fun conn tx -> task {
        let! sourceGroup = findGroupId conn tx photoName
        match sourceGroup with
        | Some source when source = targetGroupId -> ()
        | Some source ->
            do! movePhoto conn tx targetGroupId photoName
            do! cleanupGroup conn tx source
        | None ->
            do! insertPhoto conn tx targetGroupId photoName
    }

let removePhotoFromGroups (connStr: string) (blobName: string) =
    withTransaction connStr <| fun conn tx -> task {
        let! gid = findGroupId conn tx blobName
        match gid with
        | None -> ()
        | Some g ->
            do! deletePhoto conn tx blobName
            do! cleanupGroup conn tx g
    }
