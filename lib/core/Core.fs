namespace UrlRotation

[<Measure>]
type s

type Config =
    { timePerUrl: int<s>
      urls: string list }

[<RequireQualifiedAccess>]
module Storage =
    open Fable.SimpleJson

    module Internal =
        open Browser

        let configKey = "config"

        let getItem key =
            // Sadly the fable bindings are not at all nullsafe..
            let item = localStorage.getItem key
            if isNull item then None else Some item

        let setItem key data = localStorage.setItem (key, data)

    let loadConfig () =
        Internal.getItem Internal.configKey
        |> function
            | Some configJson -> Json.parseAs<Config> configJson
            | None ->
                { urls =
                    [ "www.ecosia.org/"
                      "duckduckgo.com/"
                      "www.startpage.com/" ]

                  timePerUrl = 60<s> }

    let saveConfig (config: Config) =
        Json.stringify config
        |> Internal.setItem Internal.configKey
