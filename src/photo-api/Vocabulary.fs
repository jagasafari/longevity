module Vocabulary

open System
open Dapper
open Npgsql

[<CLIMutable>]
type private GroupRow = { id: string; name: string }

[<CLIMutable>]
type private PhotoRow = { photo_name: string; group_id: string; subgroup_id: string; added_at: DateTimeOffset }

type VocabPhotoDto =
    { Name: string
      Url: string
      ThumbnailUrl: string
      LastModified: string }

type VocabSubgroupDto = { Id: string; Photos: VocabPhotoDto array }

type VocabGroupDto =
    { Id: string
      Name: string
      Subgroups: VocabSubgroupDto array
      UngroupedPhotos: VocabPhotoDto array }

let private inTransaction (conn: NpgsqlConnection) (f: NpgsqlTransaction -> System.Threading.Tasks.Task<'a>) = task {
    do! conn.OpenAsync()
    use tx = conn.BeginTransaction()
    try
        let! result = f tx
        do! tx.CommitAsync()
        return result
    with ex ->
        do! tx.RollbackAsync()
        return raise (InvalidOperationException($"Transaction failed: {ex.Message}", ex))
}

let listGroups (storage: Storage.StorageConfig) (connStr: string) () = task {
    use conn = new NpgsqlConnection(connStr)
    let! groups =
        conn.QueryAsync<GroupRow>(
            "SELECT id, name FROM vocabulary.groups ORDER BY created_at")
    let! photoRows =
        conn.QueryAsync<PhotoRow>(
            "SELECT photo_name, group_id, COALESCE(subgroup_id, '') AS subgroup_id, added_at FROM vocabulary.photos")
    let photoList = photoRows |> Seq.toList
    let! urlMap = Storage.getPhotoSasUrls storage (photoList |> List.map (fun r -> r.photo_name))
    let photosByGroup = photoList |> List.groupBy (fun r -> r.group_id) |> Map.ofList
    let toDto (p: PhotoRow) =
        let url, thumbnailUrl = urlMap |> Map.tryFind p.photo_name |> Option.defaultValue ("", "")
        { Name = p.photo_name; Url = url; ThumbnailUrl = thumbnailUrl; LastModified = p.added_at.ToString("O") }
    return
        groups
        |> Seq.map (fun g ->
            let groupPhotos = photosByGroup |> Map.tryFind g.id |> Option.defaultValue []
            let subgroups =
                groupPhotos
                |> List.filter (fun p -> not (String.IsNullOrEmpty p.subgroup_id))
                |> List.groupBy (fun p -> p.subgroup_id)
                |> List.map (fun (subId, items) ->
                    { Id = subId; Photos = items |> List.map toDto |> Array.ofList })
                |> Array.ofList
            let ungrouped =
                groupPhotos
                |> List.filter (fun p -> String.IsNullOrEmpty p.subgroup_id)
                |> List.map toDto
                |> Array.ofList
            { Id = g.id; Name = g.name; Subgroups = subgroups; UngroupedPhotos = ungrouped })
        |> Seq.toArray
}

let moveGalleryGroup (connStr: string) (galleryGroupId: string) = task {
    use conn = new NpgsqlConnection(connStr)
    return!
        inTransaction conn (fun tx -> task {
            let! catName =
                conn.ExecuteScalarAsync<string>(
                    """SELECT COALESCE(
                           (SELECT c.name
                            FROM   photo_group_categories gc
                            JOIN   categories c ON c.id = gc.category_id
                            WHERE  gc.group_id = @g
                            LIMIT  1),
                           'Untitled')""",
                    {| g = galleryGroupId |}, tx)
            let vocabId = Guid.NewGuid().ToString("N")
            let! _ =
                conn.ExecuteAsync(
                    "INSERT INTO vocabulary.groups (id, name) VALUES (@id, @name)",
                    {| id = vocabId; name = catName |}, tx)
            let! childRows =
                conn.QueryAsync<{| child_group_id: string; photo_name: string |}>(
                    """SELECT pgm.group_id AS child_group_id, pgm.photo_name
                       FROM photo_group_members pgm
                       JOIN photo_groups pg ON pg.group_id = pgm.group_id
                       WHERE pg.parent_group_id = @g""",
                    {| g = galleryGroupId |}, tx)
            let childRowList = childRows |> Seq.toList
            if not childRowList.IsEmpty then
                let childInserts =
                    childRowList
                    |> List.groupBy (fun r -> r.child_group_id)
                    |> List.collect (fun (_, items) ->
                        let subId = Guid.NewGuid().ToString("N")
                        items |> List.map (fun r ->
                            {| photo_name = r.photo_name
                               group_id = vocabId
                               subgroup_id = subId |}))
                    |> Array.ofList
                let! _ =
                    conn.ExecuteAsync(
                        "INSERT INTO vocabulary.photos (photo_name, group_id, subgroup_id) VALUES (@photo_name, @group_id, @subgroup_id) ON CONFLICT DO NOTHING",
                        childInserts, tx)
                ()
            let! parentPhotos =
                conn.QueryAsync<string>(
                    "SELECT photo_name FROM photo_group_members WHERE group_id = @g",
                    {| g = galleryGroupId |}, tx)
            let parentList = parentPhotos |> Seq.toList
            if not parentList.IsEmpty then
                let! _ =
                    conn.ExecuteAsync(
                        "INSERT INTO vocabulary.photos (photo_name, group_id) VALUES (@photo_name, @group_id) ON CONFLICT DO NOTHING",
                        parentList |> List.map (fun p -> {| photo_name = p; group_id = vocabId |}) |> Array.ofList,
                        tx)
                ()
            let! _ =
                conn.ExecuteAsync(
                    """DELETE FROM photo_group_members
                       WHERE group_id = @g
                          OR group_id IN (
                                 SELECT group_id FROM photo_groups WHERE parent_group_id = @g)""",
                    {| g = galleryGroupId |}, tx)
            let! _ =
                conn.ExecuteAsync(
                    """DELETE FROM photo_group_categories
                       WHERE group_id IN (
                                 SELECT group_id FROM photo_groups WHERE parent_group_id = @g)""",
                    {| g = galleryGroupId |}, tx)
            let! _ =
                conn.ExecuteAsync(
                    "DELETE FROM photo_groups WHERE parent_group_id = @g",
                    {| g = galleryGroupId |}, tx)
            let! _ =
                conn.ExecuteAsync(
                    "DELETE FROM photo_group_categories WHERE group_id = @g",
                    {| g = galleryGroupId |}, tx)
            let! _ =
                conn.ExecuteAsync(
                    "DELETE FROM photo_groups WHERE group_id = @g",
                    {| g = galleryGroupId |}, tx)
            return vocabId
        })
}

let listExcludedPhotoNames (connStr: string) () : System.Threading.Tasks.Task<Set<string>> = task {
    use conn = new NpgsqlConnection(connStr)
    let! names = conn.QueryAsync<string>("SELECT photo_name FROM vocabulary.photos")
    return names |> Set.ofSeq
}

let removeGroup (connStr: string) (vocabGroupId: string) = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM vocabulary.photos WHERE group_id = @g",
            {| g = vocabGroupId |})
    let! _ =
        conn.ExecuteAsync(
            "DELETE FROM vocabulary.groups WHERE id = @g",
            {| g = vocabGroupId |})
    ()
}
