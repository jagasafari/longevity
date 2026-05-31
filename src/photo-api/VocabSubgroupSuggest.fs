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
type private SuggestionJson = { word: string; photoNames: string[] }

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
        let! urlMap = Storage.getPhotoSasUrls storage names
        let indexed = names |> List.mapi (fun i n -> i + 1, n)
        let nameList =
            indexed
            |> List.map (fun (i, n) -> $"Image {i}: {n}")
            |> String.concat "\n"
        let userText =
            $"These {names.Length} photos are from one vocabulary group. \
              For each image, read the English vocabulary word shown \
              (from subtitle text, caption, or text written on the image). \
              Group images that represent the same word together.\n\n\
              Filenames:\n{nameList}\n\n\
              Respond ONLY with JSON: \
              [{{\"word\": \"<word>\", \"photoNames\": [\"<filename>\", ...]}}]. \
              If a photo has no readable word, put it in a group with word \"unknown\"."

        let imageParts : ChatMessageContentPart[] =
            [|
                yield ChatMessageContentPart.CreateTextPart(userText)
                for _, n in indexed do
                    let url, _ = urlMap |> Map.tryFind n |> Option.defaultValue ("", "")
                    if url <> "" then
                        yield ChatMessageContentPart.CreateImagePart(Uri url)
            |]

        let opts = AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01)
        let openAiClient = AzureOpenAIClient(Uri aiEndpoint, DefaultAzureCredential(), opts)
        let chatClient = openAiClient.GetChatClient "gpt-4o"

        let messages : ChatMessage[] =
            [| SystemChatMessage systemPrompt
               UserChatMessage(imageParts) |]

        try
            let! completion = chatClient.CompleteChatAsync(messages)
            let responseText =
                if completion.Value.Content.Count > 0
                then completion.Value.Content[0].Text
                else ""
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
                       SET subgroup_id = @subId
                       WHERE group_id = @g AND photo_name = ANY(@names)""",
                    {| subId = subId; g = vocabGroupId; names = s.PhotoNames |})
            ()
}
