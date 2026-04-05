module PhotoGroups

open System
open System.Collections.Generic
open System.Data
open Dapper
open Npgsql

// ─── Row types ───────────────────────────────────────────────

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

// ─── State machines ──────────────────────────────────────────

/// What should happen when two photos are grouped together?
type internal GroupChange =
    | NoChange
    | MoveToGroup of sourceGroupId: string * targetGroupId: string * photoName: string
    | AddPhotoToGroup of groupId: string * photoName: string
    | CreateGroup of sourceName: string * targetName: string
    | CreateSubgroup of parentGroupId: string * sourceName: string * targetName: string

/// What should happen when a photo is moved between groups?
type internal MoveIntent =
    | AlreadyInTarget
    | MoveFromGroup of sourceGroupId: string
    | AddToGroup

/// What state is a group in after losing a photo/child?
type internal GroupState =
    | Empty
    | Singleton
    | HasNames
    | HasChildren
    | Healthy

/// Should we clean up a group, and how?
type internal CleanupAction =
    | DeleteGroup
    | KeepGroup

// ─── Pure decision functions ─────────────────────────────────

let internal planGroupChange sourceGroup targetGroup sourceName targetName =
    match sourceGroup, targetGroup with
    | Some sg, Some tg when sg = tg ->
        CreateSubgroup (sg, sourceName, targetName)
    | Some sg, Some tg -> MoveToGroup (sg, tg, sourceName)
    | Some sg, None -> AddPhotoToGroup (sg, targetName)
    | None, Some tg -> AddPhotoToGroup (tg, sourceName)
    | None, None -> CreateGroup (sourceName, targetName)

let internal planMove sourceGroup targetGroupId =
    match sourceGroup with
    | Some source when source = targetGroupId -> AlreadyInTarget
    | Some source -> MoveFromGroup source
    | None -> AddToGroup

let internal classifyGroup photoCount childCount nameCount =
    match photoCount, childCount, nameCount with
    | 0, 0, 0            -> Empty
    | pc, 0, 0 when pc <= 1 -> Singleton
    | _, _, nc when nc > 0  -> HasNames
    | _, cc, _ when cc > 0  -> HasChildren
    | _                     -> Healthy

let internal decideCleanup groupState =
    match groupState with
    | Empty | Singleton -> DeleteGroup
    | HasNames | HasChildren | Healthy -> KeepGroup

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
            "SELECT group_id, photo_name FROM photo_group_members ORDER BY group_id")
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
            "SELECT group_id, photo_name FROM photo_group_members ORDER BY group_id")

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
            "SELECT group_id FROM photo_group_members WHERE photo_name = @p LIMIT 1",
            {| p = photo |},
            tx)
    return Option.ofObj result
}

let private insertPhoto (conn: NpgsqlConnection) (tx: NpgsqlTransaction) gid photo = task {
    let! _ =
        conn.ExecuteAsync(
            "INSERT INTO photo_group_members (group_id, photo_name) VALUES (@g, @p)
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
                "UPDATE photo_group_members SET group_id = @g WHERE photo_name = @p",
                {| g = targetGroupId; p = photoName |},
                tx)
        ()
    }

let private deleteSingletonOrEmpty (conn: NpgsqlConnection) (tx: NpgsqlTransaction) gid = task {
    let! _ =
        conn.ExecuteAsync(
            """DELETE FROM photo_group_members WHERE group_id = @g
               AND (SELECT COUNT(*) FROM photo_group_members WHERE group_id = @g) <= 1""",
            {| g = gid |},
            tx)
    ()
}

let private deleteGroup
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid =
    task {
        // Ungroup all photos (they become ungrouped, never deleted from storage)
        let! _ =
            conn.ExecuteAsync(
                "DELETE FROM photo_group_members WHERE group_id = @g",
                {| g = gid |},
                tx)
        // Remove name assignments
        let! _ =
            conn.ExecuteAsync(
                "DELETE FROM photo_group_names WHERE group_id = @g",
                {| g = gid |},
                tx)
        // Children become root groups (FK is SET NULL),
        // but explicitly reparent to parent's parent for cleaner hierarchy
        let! parentId =
            conn.ExecuteScalarAsync<string>(
                "SELECT parent_group_id FROM photo_groups WHERE group_id = @g",
                {| g = gid |},
                tx)
        let! _ =
            conn.ExecuteAsync(
                "UPDATE photo_groups SET parent_group_id = @p WHERE parent_group_id = @g",
                {| p = parentId; g = gid |},
                tx)
        // Now safe to delete the group row
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
                "SELECT COUNT(*) FROM photo_group_members WHERE group_id = @g",
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

let private countNames
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction)
    gid =
    task {
        let! count =
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM photo_group_names WHERE group_id = @g",
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
                "DELETE FROM photo_group_members WHERE group_id = @g",
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
        let! nameCount = countNames conn tx gid
        let state = classifyGroup photoCount childCount nameCount
        match decideCleanup state with
        | DeleteGroup -> do! deleteGroup conn tx gid
        | KeepGroup -> ()
    }

let private deletePhoto (conn: NpgsqlConnection) (tx: NpgsqlTransaction) blobName = task {
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM photo_group_members WHERE photo_name = @p",
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
        | MoveToGroup (sourceGroupId, targetGroupId, photoName) ->
            do! movePhoto conn tx targetGroupId photoName
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
        match planMove sourceGroup targetGroupId with
        | AlreadyInTarget -> ()
        | MoveFromGroup sourceGroupId ->
            do! movePhoto conn tx targetGroupId photoName
            do! cleanupGroup conn tx sourceGroupId
        | AddToGroup ->
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
