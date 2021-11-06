namespace UrlRotation

[<Measure>]
type s

type Config =
    { timePerUrl: int<s>
      urls: string list }

[<RequireQualifiedAccess>]
module Storage =
    open Thoth.Json

    module Internal =
        open Browser

        let configKey = "config"

        let getItem key =
            // Sadly the fable bindings are not at all nullsafe..
            let item = localStorage.getItem key
            if isNull item then None else Some item

        let setItem key data = localStorage.setItem (key, data)

    let loadConfig () =
        let buildDefault () =
            { urls =
                [ "www.ecosia.org/"
                  "duckduckgo.com/"
                  "www.startpage.com/" ]

              timePerUrl = 60<s> }

        Internal.getItem Internal.configKey
        |> function
            | Some configJson ->
                Decode.Auto.fromString<Config> configJson
                |> function
                    | Ok config -> config
                    | Error _ -> buildDefault ()
            | None -> buildDefault ()

    let saveConfig (config: Config) =
        Encode.Auto.toString (0, config)
        |> Internal.setItem Internal.configKey
