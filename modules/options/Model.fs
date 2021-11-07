namespace UrlRotation

[<RequireQualifiedAccess>]
type Tab =
    | GUI
    | Json

type WipConfig =
    { urls: Map<int, string>
      timePerUrl: int<s> }

module WipConfig =
    let toConfig config =
        let urls =
            Map.toList config.urls
            |> List.sortBy fst
            |> List.map snd
            // TODO: check for validity
            |> List.filter (fun value -> value <> "")
            |> List.map (fun url ->
                if url.StartsWith("http://") then
                    url.Replace("http://", "")
                else if url.StartsWith("https://") then
                    url.Replace("https://", "")
                else
                    url)

        { Config.urls = urls
          timePerUrl = config.timePerUrl }

    let fromConfig (config: Config) =
        { urls = config.urls |> List.indexed |> Map.ofList
          timePerUrl = config.timePerUrl }

module WipConfigLens =
    let urls =
        Lens((fun config -> config.urls), (fun config urls -> { config with urls = urls }))

    let timePerUrl =
        Lens((fun config -> config.timePerUrl), (fun config timePerUrl -> { config with timePerUrl = timePerUrl }))

type State =
    { config: WipConfig
      saved: bool
      json: string
      jsonInvalid: bool
      tab: Tab }

module StateLens =
    let config =
        Lens((fun state -> state.config), (fun state config -> { state with config = config }))

    let json =
        Lens((fun state -> state.json), (fun state json -> { state with json = json }))

    let jsonInvalid =
        Lens((fun state -> state.jsonInvalid), (fun state jsonInvalid -> { state with jsonInvalid = jsonInvalid }))

    let saved =
        Lens((fun state -> state.saved), (fun state saved -> { state with saved = saved }))

    let tab =
        Lens((fun state -> state.tab), (fun state tab -> { state with tab = tab }))

    let urls = config << WipConfigLens.urls
    let timePerUrl = config << WipConfigLens.timePerUrl

type Msg =
    | ChangeUrl of key: int * value: string
    | ChangeTime of time: int<s>
    | SetJson of json: string
    | SetTab of tab: Tab
    | Save

[<RequireQualifiedAccess>]
module Model =
    let init () =
        let wipConfig =
            Storage.loadConfig () |> WipConfig.fromConfig

        { config = wipConfig
          saved = false
          json = ""
          jsonInvalid = false
          tab = Tab.GUI }

[<RequireQualifiedAccess>]
module Update =
    let postprocess msg state =
        match msg with
        // For all changing messages, we disable the saved flag
        | ChangeUrl _
        | ChangeTime _
        | SetJson _ -> { state with saved = false }
        | _ -> state

    let perform msg state =
        match msg with
        | ChangeUrl (key, value) ->
            state.config.urls
            |> Map.add key value
            |> setlr StateLens.urls state
        | ChangeTime time -> setl StateLens.timePerUrl time state
        | SetTab tab ->
            let config = state.config |> WipConfig.toConfig

            state
            // Refresh json
            |> setl StateLens.json (toJson 2 config)
            |> setl StateLens.jsonInvalid false
            |> setl StateLens.tab tab
        | SetJson json ->
            fromJson<Config> json
            |> function
                | Ok config ->
                    // The json is valid, we can use it to fill the other settings
                    config
                    |> WipConfig.fromConfig
                    |> setlr StateLens.config state
                    |> setl StateLens.jsonInvalid false
                | Error _ -> state |> setl StateLens.jsonInvalid true
        | Save ->
            WipConfig.toConfig state.config
            |> Storage.saveConfig

            { state with saved = true }
        |> postprocess msg
