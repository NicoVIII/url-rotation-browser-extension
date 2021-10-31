namespace UrlRotation

type State =
    { timePerUrl: int<s>
      urls: Map<int, string>
      saved: bool }

type Msg =
    | ChangeUrl of key: int * value: string
    | ChangeTime of time: int<s>
    | Save

[<RequireQualifiedAccess>]
module Model =
    let init () =
        let config = Storage.loadConfig ()

        { urls = config.urls |> List.indexed |> Map.ofList
          saved = false
          timePerUrl = config.timePerUrl }

[<RequireQualifiedAccess>]
module Update =
    let postprocess msg state =
        match msg with
        // For all changing messages, we disable the saved flag
        | ChangeUrl _
        | ChangeTime _ -> { state with saved = false }
        | _ -> state

    let perform msg state =
        match msg with
        | ChangeUrl (key, value) ->
            let urls = Map.add key value state.urls

            { state with urls = urls }
        | ChangeTime time -> { state with timePerUrl = time }
        | Save ->
            let urls =
                Map.toList state.urls
                |> List.sortBy fst
                |> List.map snd
                // TODO: check for validity
                |> List.filter (fun value -> value <> "")

            { Config.urls = urls
              timePerUrl = state.timePerUrl }
            |> Storage.saveConfig

            { state with saved = true }
        |> postprocess msg
