module BlobName

open System.Text.RegularExpressions

let private invalidPattern =
    Regex(@"[^A-Za-z0-9._\-]+", RegexOptions.Compiled)

let sanitize (name: string) =
    let replaced = invalidPattern.Replace(name, "-")
    let trimmed  = replaced.Trim([| '-'; '.' |])
    if trimmed = "" then "file" else trimmed

let needsSanitizing (name: string) =
    sanitize name <> name
