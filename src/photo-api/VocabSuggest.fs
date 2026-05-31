module VocabSuggest

open System
open System.Text.Json
open System.Threading.Tasks
open Azure.AI.OpenAI
open Azure.Identity
open OpenAI.Chat
open Dapper
open Npgsql

[<CLIMutable>]
type private CandidateRow = { group_id: string; photo_count: int; categories: string; sample_photo: string }

type Suggestion = { GroupId: string; Reason: string }

[<CLIMutable>]
type private SuggestionJson = { groupId: string; reason: string }

let private maxCandidates = 10

let private systemPrompt =
    "You help tag photo groups for a vocabulary learning feature. \
     A group is suitable for vocabulary if its subject would make a good \
     visual vocabulary flashcard (object, animal, food, plant, scene, action). \
     Respond only with a JSON array."

let private fetchCandidates (connStr: string) : Task<CandidateRow list> = task {
    use conn = new NpgsqlConnection(connStr)
    let! rows =
        conn.QueryAsync<CandidateRow>(
            """SELECT pgm.group_id,
                      COUNT(pgm.photo_name) AS photo_count,
                      COALESCE(
                          STRING_AGG(DISTINCT c.name, ', ' ORDER BY c.name),
                          ''
                      ) AS categories,
                      MIN(pgm.photo_name) AS sample_photo
               FROM public.photo_group_members pgm
               LEFT JOIN public.photo_group_categories gc ON gc.group_id = pgm.group_id
               LEFT JOIN public.categories c ON c.id = gc.category_id
               GROUP BY pgm.group_id
               ORDER BY photo_count DESC
               LIMIT @limit""",
            {| limit = maxCandidates |})
    return rows |> Seq.toList
}

let private parseResponse (json: string) : Suggestion list =
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
        |> List.map (fun s -> { GroupId = s.groupId; Reason = s.reason })
    with _ -> []

let suggest
    (connStr: string)
    (aiEndpoint: string)
    () : Task<Suggestion[]> = task {
    let! candidates = fetchCandidates connStr
    if candidates.IsEmpty then
        return [||]
    else
        let groupList =
            candidates
            |> List.mapi (fun i r ->
                let cats = if r.categories = "" then "(none)" else r.categories
                $"Group {i + 1}: id={r.group_id} | photos={r.photo_count} | sample_file={r.sample_photo} | categories={cats}")
            |> String.concat "\n"

        let userMessage =
            $"Here are photo groups not yet in the vocabulary collection:\n\n\
              {groupList}\n\n\
              Which of these should be tagged as vocabulary flashcard groups? \
              Respond with ONLY a JSON array: \
              [{{\"groupId\": \"<exact id>\", \"reason\": \"<one short phrase>\"}}]. \
              If none are suitable, return []."

        let opts = AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01)
        let openAiClient = AzureOpenAIClient(Uri aiEndpoint, DefaultAzureCredential(), opts)
        let chatClient = openAiClient.GetChatClient "gpt-4o"

        let messages : ChatMessage[] =
            [| SystemChatMessage systemPrompt
               UserChatMessage userMessage |]

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
