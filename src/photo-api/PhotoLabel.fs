module PhotoLabel

open System
open System.Text.Json
open System.Threading.Tasks
open Azure.AI.OpenAI
open Azure.Identity
open OpenAI.Chat
open Dapper
open Npgsql

let private visionModel = "gpt-4o-mini"
let private textModel = "gpt-4o-mini"

[<CLIMutable>]
type LabelResult =
    { PhotoName: string
      Word: string
      Source: string
      Confidence: float
      LabeledBy: string }

[<CLIMutable>]
type SubgroupProposal = { Word: string; PhotoNames: string array }

[<CLIMutable>]
[<CLIMutable>]
type private LabelJson = { word: string; source: string; confidence: float }

[<CLIMutable>]
type private FuzzyJson = { canonical: string; members: string array }

[<CLIMutable>]
type private UnlabeledRow = { photo_name: string }

[<CLIMutable>]
type private LabeledRow = { photo_name: string; word: string }

let private labelSystemPrompt =
    "You label photos for an English vocabulary learning app. \
     Return ONE lowercase English word that best teaches what the photo shows. \
     Priority order: \
     1) if subtitles or large written English text are visible, return that \
        word; \
     2) otherwise return the most prominent object, action, or concept \
        depicted in the photo. \
     Respond ONLY with raw JSON (no markdown, no code fences): \
     {\"word\": \"<lowercase word>\", \
      \"source\": \"netflix_caption\" | \"ai_image_with_word\" | \"object\", \
      \"confidence\": 0.0..1.0}. \
     Use empty word only when the image is unreadable or blank."

let private fuzzySystemPrompt =
    "You group near-duplicate English words. \
     Given a list of words, merge ones that are minor variants \
     (plurals, tenses, gerunds, common typos) into one canonical lowercase word. \
     Respond ONLY with JSON array: \
     [{\"canonical\": \"<word>\", \"members\": [\"<word>\", ...]}]. \
     Only include groups with 2+ members. Skip words that have no near-variants."

let private stripFences (s: string) =
    s.Trim()
        .Replace("```json", "")
        .Replace("```", "")
        .Trim()

let private parseLabel (json: string) : LabelJson option =
    let stripped = stripFences json
    try
        let opts = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        Some (JsonSerializer.Deserialize<LabelJson>(stripped, opts))
    with ex ->
        eprintfn "parseLabel failed on %A: %s" stripped ex.Message
        None

let private parseFuzzy (json: string) : FuzzyJson array =
    try
        let opts = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        JsonSerializer.Deserialize<FuzzyJson array>(stripFences json, opts)
    with _ -> [||]

let private chatClient (endpoint: string) (model: string) =
    let opts =
        AzureOpenAIClientOptions(
            AzureOpenAIClientOptions.ServiceVersion.V2024_06_01)
    let client = AzureOpenAIClient(Uri endpoint, DefaultAzureCredential(), opts)
    client.GetChatClient model

let private callChat
    (endpoint: string) (model: string) (messages: ChatMessage array)
    : Task<string> = task {
    let client = chatClient endpoint model
    let maxAttempts = 5
    let mutable attempt = 1
    let mutable result = ""
    let mutable lastErr : exn option = None
    let mutable doneLoop = false
    while not doneLoop do
        try
            let! completion = client.CompleteChatAsync(messages)
            result <-
                if completion.Value.Content.Count > 0
                then completion.Value.Content[0].Text
                else ""
            doneLoop <- true
        with
        | :? System.ClientModel.ClientResultException as ex when ex.Status = 429 ->
            lastErr <- Some (ex :> exn)
            if attempt >= maxAttempts then
                doneLoop <- true
            else
                let retryAfter =
                    let h = ex.GetRawResponse().Headers
                    let ok, v = h.TryGetValue "Retry-After"
                    match ok, System.Int32.TryParse v with
                    | true, (true, s) -> s
                    | _ -> int (pown 2 attempt) * 2
                let waitMs = max 1000 (retryAfter * 1000)
                eprintfn "callChat 429 attempt %d, waiting %dms" attempt waitMs
                do! Task.Delay waitMs
                attempt <- attempt + 1
    match lastErr, doneLoop with
    | Some ex, true when result = "" -> raise ex
    | _ -> ()
    return result
}

let private httpClient = new System.Net.Http.HttpClient()

let private mediaTypeFor (name: string) =
    let lower = name.ToLowerInvariant()
    if lower.EndsWith ".png" then "image/png"
    elif lower.EndsWith ".gif" then "image/gif"
    elif lower.EndsWith ".webp" then "image/webp"
    else "image/jpeg"

let private labelOnePhoto
    (endpoint: string) (photoName: string) (imageUrl: string)
    : Task<LabelJson> = task {
    let! resp = httpClient.GetAsync(imageUrl)
    resp.EnsureSuccessStatusCode() |> ignore
    let! bytes = resp.Content.ReadAsByteArrayAsync()
    let data = BinaryData.FromBytes(bytes)
    let parts : ChatMessageContentPart array =
        [| ChatMessageContentPart.CreateTextPart(
               $"Label this image (filename: {photoName}). Respond with JSON only.")
           ChatMessageContentPart.CreateImagePart(
               data, mediaTypeFor photoName, ChatImageDetailLevel.High) |]
    let messages : ChatMessage array =
        [| SystemChatMessage labelSystemPrompt
           UserChatMessage(parts) |]
    let! raw = callChat endpoint visionModel messages
    eprintfn "labelOnePhoto %s -> %s" photoName (raw.Replace("\n", " "))
    return
        parseLabel raw
        |> Option.defaultValue { word = ""; source = "object"; confidence = 0.0 }
}

let private persistLabel
    (conn: NpgsqlConnection) (photoName: string) (label: LabelJson)
    : Task<unit> = task {
    let! _ =
        conn.ExecuteAsync(
            """UPDATE vocabulary.photos
               SET word = NULLIF(@word, ''),
                   source = @source,
                   confidence = @confidence,
                   labeled_at = NOW(),
                   labeled_by = 'ai'
               WHERE photo_name = @name""",
            {| name = photoName
               word = label.word
               source = label.source
               confidence = label.confidence |})
    return ()
}

let labelPhoto
    (storage: Storage.StorageConfig)
    (connStr: string)
    (endpoint: string)
    (photoName: string)
    : Task<LabelResult> = task {
    let! urlMap = Storage.getPhotoSasUrls storage [ photoName ]
    let url, _ =
        urlMap |> Map.tryFind photoName |> Option.defaultValue ("", "")
    if url = "" then
        return
            raise (InvalidOperationException $"Photo not found: {photoName}")
    else
        let! label = labelOnePhoto endpoint photoName url
        use conn = new NpgsqlConnection(connStr)
        do! conn.OpenAsync()
        do! persistLabel conn photoName label
        return
            { PhotoName = photoName
              Word = label.word
              Source = label.source
              Confidence = label.confidence
              LabeledBy = "ai" }
}

let setWord
    (connStr: string) (photoName: string) (word: string option)
    : Task<unit> = task {
    use conn = new NpgsqlConnection(connStr)
    let! _ =
        conn.ExecuteAsync(
            """UPDATE vocabulary.photos
               SET word = @word,
                   labeled_at = NOW(),
                   labeled_by = 'user'
               WHERE photo_name = @name""",
            {| name = photoName
               word = Option.toObj word |})
    return ()
}

let labelGroup
    (storage: Storage.StorageConfig)
    (connStr: string)
    (endpoint: string)
    (groupId: string)
    (onLabeled: LabelResult -> Task)
    (onFailed: string -> string -> Task)
    : Task<{| Labeled: int; Failed: int |}> = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<UnlabeledRow>(
            """SELECT photo_name FROM vocabulary.photos
               WHERE group_id = @g
                 AND removed_at IS NULL
                 AND word IS NULL""",
            {| g = groupId |})
    let names = rows |> Seq.toList |> List.map (fun r -> r.photo_name)
    if names.IsEmpty then
        return {| Labeled = 0; Failed = 0 |}
    else
        let! urlMap = Storage.getPhotoSasUrls storage names
        do! conn.OpenAsync()
        let mutable labeled = 0
        let mutable failed = 0
        for name in names do
            try
                let url, _ =
                    urlMap |> Map.tryFind name |> Option.defaultValue ("", "")
                if url <> "" then
                    let! label = labelOnePhoto endpoint name url
                    do! persistLabel conn name label
                    labeled <- labeled + 1
                    do! onLabeled
                            { PhotoName = name
                              Word = label.word
                              Source = label.source
                              Confidence = label.confidence
                              LabeledBy = "ai" }
                    do! Task.Delay 500
            with ex ->
                eprintfn "labelGroup: %s failed: %s" name ex.Message
                failed <- failed + 1
                try do! onFailed name ex.Message with _ -> ()
        return {| Labeled = labeled; Failed = failed |}
}

let private fuzzyMatch
    (endpoint: string) (words: string list)
    : Task<Map<string, string>> = task {
    if words.Length < 2 then
        return Map.empty
    else
        let payload = String.concat ", " words
        let messages : ChatMessage array =
            [| SystemChatMessage fuzzySystemPrompt
               UserChatMessage($"Words: {payload}") |]
        try
            let! raw = callChat endpoint textModel messages
            return
                parseFuzzy raw
                |> Array.collect (fun g ->
                    g.members |> Array.map (fun m -> m.ToLowerInvariant(), g.canonical.ToLowerInvariant()))
                |> Map.ofArray
        with ex ->
            eprintfn "fuzzyMatch failed: %s" ex.Message
            return Map.empty
}

let matchSubgroups
    (connStr: string)
    (endpoint: string)
    (groupId: string)
    : Task<SubgroupProposal array> = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<LabeledRow>(
            """SELECT photo_name, word FROM vocabulary.photos
               WHERE group_id = @g
                 AND removed_at IS NULL
                 AND (subgroup_id IS NULL OR subgroup_id = '')
                 AND word IS NOT NULL
                 AND word <> ''""",
            {| g = groupId |})
    let labeled =
        rows
        |> Seq.toList
        |> List.map (fun r ->
            r.photo_name, r.word.Trim().ToLowerInvariant())
    let distinctWords = labeled |> List.map snd |> List.distinct
    let! canonicalMap = fuzzyMatch endpoint distinctWords
    let canonical (w: string) =
        canonicalMap |> Map.tryFind w |> Option.defaultValue w
    return
        labeled
        |> List.groupBy (fun (_, w) -> canonical w)
        |> List.filter (fun (_, items) -> items.Length >= 2)
        |> List.map (fun (word, items) ->
            { Word = word
              PhotoNames = items |> List.map fst |> Array.ofList })
        |> Array.ofList
}

let applySubgroups
    (connStr: string)
    (groupId: string)
    (proposals: SubgroupProposal array)
    : Task<unit> = task {
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync()
    for p in proposals do
        if p.PhotoNames.Length > 0 then
            let subId = Guid.NewGuid().ToString("N")
            let! _ =
                conn.ExecuteAsync(
                    """UPDATE vocabulary.photos
                       SET subgroup_id = @subId, subgroup_word = @word
                       WHERE group_id = @g
                         AND photo_name = ANY(@names)""",
                    {| subId = subId
                       word = p.Word
                       g = groupId
                       names = p.PhotoNames |})
            ()
}
