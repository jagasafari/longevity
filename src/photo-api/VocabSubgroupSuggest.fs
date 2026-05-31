module VocabSubgroupSuggest

open System
open System.Text.Json
open System.Threading.Tasks
open Azure.AI.OpenAI
open Azure.Identity
open OpenAI.Chat
open Dapper
open Npgsql

[<CLIMutable>]
type SubgroupSuggestion = { Word: string; PhotoNames: string[] }

[<CLIMutable>]
type CrossGroupPhoto = { PhotoName: string; GroupId: string; GroupName: string }

[<CLIMutable>]
type CrossGroupSuggestion = { Word: string; PrimaryGroupId: string; Photos: CrossGroupPhoto[] }

[<CLIMutable>]
type private SuggestionJson = { word: string; photoNames: string[] }

[<CLIMutable>]
type private ExampleRow = { word: string; photo_name: string }

[<CLIMutable>]
type private PhotoGroupRow = { photo_name: string; group_id: string; group_name: string }

let private maxExamples = 3
let private maxPhotosPerCall = 20

let private systemPrompt =
    "You analyze photos for a vocabulary learning app. \
     Each photo is either a Netflix screenshot with English subtitles/captions, \
     or an AI-generated image with a vocabulary word written on it. \
     Identify the English vocabulary word in each image and group photos by word. \
     Respond ONLY with a JSON array."

let private parseResponse (json: string) : SubgroupSuggestion list =
    let cleaned =
        json.Trim()
            .TrimStart('`')
            .TrimEnd('`')
            .Replace("```json", "")
            .Replace("```", "")
            .Trim()
    try
        let opts = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        JsonSerializer.Deserialize<SuggestionJson[]>(cleaned, opts)
        |> Array.toList
        |> List.map (fun s -> { Word = s.word; PhotoNames = s.photoNames })
    with ex ->
        let preview = if cleaned.Length > 500 then cleaned[..499] else cleaned
        eprintfn "VocabSubgroupSuggest: failed to parse model response: %s | raw: %s" ex.Message preview
        []

let private fetchExamples (conn: NpgsqlConnection) (excludeGroupId: string) : Task<SubgroupSuggestion list> = task {
    let! rows =
        conn.QueryAsync<ExampleRow>(
            """SELECT vp.subgroup_word AS word, vp.photo_name
               FROM vocabulary.photos vp
               WHERE vp.subgroup_id IS NOT NULL
                 AND vp.subgroup_id <> ''
                 AND vp.subgroup_word IS NOT NULL
                 AND vp.subgroup_word <> ''
                 AND (@exclude = '' OR vp.group_id <> @exclude)
               ORDER BY vp.subgroup_word, vp.photo_name
               LIMIT 30""",
            {| exclude = excludeGroupId |})
    return
        rows
        |> Seq.toList
        |> List.groupBy (fun r -> r.word)
        |> List.truncate maxExamples
        |> List.map (fun (word, items) ->
            { Word = word
              PhotoNames = items |> List.map (fun r -> r.photo_name) |> Array.ofList })
}

let private formatExamples (examples: SubgroupSuggestion list) =
    if examples.IsEmpty then ""
    else
        let lines =
            examples
            |> List.map (fun e ->
                let names = e.PhotoNames |> String.concat "\", \""
                $"  {{\"word\": \"{e.Word}\", \"photoNames\": [\"{names}\"]}}")
            |> String.concat ",\n"
        $"Here are real examples from your collection showing the correct format:\n[\n{lines}\n]\n\n"

let private callVision (aiEndpoint: string) (messages: ChatMessage[]) : Task<string> = task {
    let opts = AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01)
    let openAiClient = AzureOpenAIClient(Uri aiEndpoint, DefaultAzureCredential(), opts)
    let chatClient = openAiClient.GetChatClient "gpt-4o"
    let! completion = chatClient.CompleteChatAsync(messages)
    return
        if completion.Value.Content.Count > 0
        then completion.Value.Content[0].Text
        else ""
}

let suggest
    (connStr: string)
    (storage: Storage.StorageConfig)
    (aiEndpoint: string)
    (vocabGroupId: string) : Task<SubgroupSuggestion[]> = task {
    use conn = new NpgsqlConnection(connStr)
    let! photoNames =
        conn.QueryAsync<string>(
            """SELECT photo_name FROM vocabulary.photos
               WHERE group_id = @g AND (subgroup_id IS NULL OR subgroup_id = '')""",
            {| g = vocabGroupId |})
    let names = photoNames |> Seq.toList
    if names.IsEmpty then
        return [||]
    else
        let! examples = fetchExamples conn vocabGroupId
        let! urlMap = Storage.getPhotoSasUrls storage names
        let indexed = names |> List.mapi (fun i n -> i + 1, n)
        let nameList =
            indexed
            |> List.map (fun (i, n) -> $"Image {i}: {n}")
            |> String.concat "\n"
        let userText =
            $"{formatExamples examples}\
              Now group these {names.Length} photos from one vocabulary group. \
              For each image, read the English vocabulary word shown \
              (from subtitle text, caption, or text written on the image). \
              Group images that share the same word together.\n\n\
              Filenames:\n{nameList}\n\n\
              Respond ONLY with JSON: \
              [{{\"word\": \"<word>\", \"photoNames\": [\"<exact filename>\", ...]}}]. \
              Use lowercase for the word. \
              If a photo has no readable word, use word \"unknown\"."
        let imageParts : ChatMessageContentPart[] =
            [|
                yield ChatMessageContentPart.CreateTextPart(userText)
                for _, n in indexed do
                    let url, _ = urlMap |> Map.tryFind n |> Option.defaultValue ("", "")
                    if url <> "" then
                        yield ChatMessageContentPart.CreateImagePart(Uri url)
            |]
        let messages : ChatMessage[] =
            [| SystemChatMessage systemPrompt; UserChatMessage(imageParts) |]
        try
            let! responseText = callVision aiEndpoint messages
            return parseResponse responseText |> List.toArray
        with ex ->
            let inner = if ex.InnerException <> null then $" | inner: {ex.InnerException.Message}" else ""
            return raise (InvalidOperationException($"AI call failed: {ex.Message}{inner}", ex))
}

let applySubgroups
    (connStr: string)
    (vocabGroupId: string)
    (suggestions: SubgroupSuggestion[]) : Task<unit> = task {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync()
    for s in suggestions do
        if s.PhotoNames.Length > 0 then
            let subId = Guid.NewGuid().ToString("N")
            let! _ =
                conn.ExecuteAsync(
                    """UPDATE vocabulary.photos
                       SET subgroup_id = @subId, subgroup_word = @word
                       WHERE group_id = @g AND photo_name = ANY(@names)""",
                    {| subId = subId; word = s.Word; g = vocabGroupId; names = s.PhotoNames |})
            ()
}

let private isAiGroup (name: string) =
    let n = name.ToLowerInvariant()
    n.Contains("ai") || n.Contains("generated") || n.Contains("artificial")

let suggestAllSubgroups
    (connStr: string)
    (storage: Storage.StorageConfig)
    (aiEndpoint: string)
    () : Task<CrossGroupSuggestion[]> = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<PhotoGroupRow>(
            """SELECT vp.photo_name, vp.group_id, vg.name AS group_name
               FROM vocabulary.photos vp
               JOIN vocabulary.groups vg ON vg.id = vp.group_id
               WHERE (vp.subgroup_id IS NULL OR vp.subgroup_id = '')
               LIMIT @limit""",
            {| limit = maxPhotosPerCall |})
    let photoRows = rows |> Seq.toList
    if photoRows.IsEmpty then
        return [||]
    else
        let! examples = fetchExamples conn ""
        let allNames = photoRows |> List.map (fun r -> r.photo_name)
        let! urlMap = Storage.getPhotoSasUrls storage allNames
        let indexed = photoRows |> List.mapi (fun i r -> i + 1, r)
        let nameList =
            indexed
            |> List.map (fun (i, r) -> $"Image {i}: {r.photo_name} (group: {r.group_name})")
            |> String.concat "\n"
        let userText =
            $"{formatExamples examples}\
              These {photoRows.Length} photos come from DIFFERENT vocabulary groups. \
              For each image, read the English vocabulary word shown \
              (from subtitle text, caption, or text written on the image). \
              IMPORTANT: group ALL images that share the same word together, \
              even if they come from different vocabulary groups — \
              a Netflix screenshot and an AI-generated image for the same word \
              must be in the same group.\n\n\
              Filenames:\n{nameList}\n\n\
              Respond ONLY with JSON: \
              [{{\"word\": \"<word>\", \"photoNames\": [\"<exact filename>\", ...]}}]. \
              Use lowercase for the word. \
              If a photo has no readable word, use word \"unknown\"."
        let imageParts : ChatMessageContentPart[] =
            [|
                yield ChatMessageContentPart.CreateTextPart(userText)
                for _, r in indexed do
                    let url, _ = urlMap |> Map.tryFind r.photo_name |> Option.defaultValue ("", "")
                    if url <> "" then
                        yield ChatMessageContentPart.CreateImagePart(Uri url)
            |]
        let messages : ChatMessage[] =
            [| SystemChatMessage systemPrompt; UserChatMessage(imageParts) |]
        try
            let! responseText = callVision aiEndpoint messages
            let suggestions = parseResponse responseText
            let photoInfoByName = photoRows |> List.map (fun r -> r.photo_name, r) |> Map.ofList
            return
                suggestions
                |> List.choose (fun s ->
                    let crossPhotos =
                        s.PhotoNames
                        |> Array.choose (fun n -> photoInfoByName |> Map.tryFind n)
                        |> Array.map (fun r ->
                            { PhotoName = r.photo_name; GroupId = r.group_id; GroupName = r.group_name })
                    if crossPhotos.Length = 0 then None
                    else
                        // prefer non-AI group; then group with most photos
                        let primaryGroupId =
                            crossPhotos
                            |> Array.groupBy (fun p -> p.GroupId)
                            |> Array.sortWith (fun (_, a) (_, b) ->
                                let aAi = isAiGroup a[0].GroupName
                                let bAi = isAiGroup b[0].GroupName
                                if aAi <> bAi then (if aAi then 1 else -1)
                                else b.Length.CompareTo(a.Length))
                            |> Array.head
                            |> fst
                        Some { Word = s.Word; PrimaryGroupId = primaryGroupId; Photos = crossPhotos })
                |> List.toArray
        with ex ->
            let inner = if ex.InnerException <> null then $" | inner: {ex.InnerException.Message}" else ""
            return raise (InvalidOperationException($"AI call failed: {ex.Message}{inner}", ex))
}

let applyCrossGroupSubgroups
    (connStr: string)
    (suggestions: CrossGroupSuggestion[]) : Task<unit> = task {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync()
    for s in suggestions do
        if s.Photos.Length > 0 then
            let subId = Guid.NewGuid().ToString("N")
            let names = s.Photos |> Array.map (fun p -> p.PhotoName)
            let! _ =
                conn.ExecuteAsync(
                    """UPDATE vocabulary.photos
                       SET group_id = @primaryGroup, subgroup_id = @subId, subgroup_word = @word
                       WHERE photo_name = ANY(@names)""",
                    {| primaryGroup = s.PrimaryGroupId; subId = subId; word = s.Word; names = names |})
            ()
}
