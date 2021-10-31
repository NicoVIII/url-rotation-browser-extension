namespace UrlRotation

type State = { urls: Map<int, string>; saved: bool }

type Msg =
    | ChangeUrl of key: int * value: string
    | Save

[<RequireQualifiedAccess>]
module Model =
    let init () =
        let config = Storage.loadConfig ()

        { urls = config.urls |> List.indexed |> Map.ofList
          saved = false }

[<RequireQualifiedAccess>]
module Update =
    let perform msg state =
        match msg with
        | ChangeUrl (key, value) ->
            let urls = Map.add key value state.urls

            { state with
                urls = urls
                saved = false }
        | Save ->
            Map.toList state.urls
            |> List.sortBy fst
            |> List.map snd
            // TODO: check for validity
            |> List.filter (fun value -> value <> "")
            |> (fun urls -> { Config.urls = urls })
            |> Storage.saveConfig

            { state with saved = true }
