namespace UrlRotation

[<Measure>]
type s

[<Measure>]
type ms

[<AutoOpen>]
module Conversion =
    let inline sToMs x : int<ms> = x * 1000<ms / s>

type Config =
    { timePerUrl: int<s>
      urls: string list }

[<RequireQualifiedAccess>]
module Promise =
    let fromContinuation continuation =
        Promise.create (fun resolve _ -> continuation resolve)

[<AutoOpen>]
module Json =
    open Thoth.Json

    let inline toJson (spaces: int) (config: 'a) =
        Encode.Auto.toString<'a> (spaces, config)

    let inline fromJson<'a> json = Decode.Auto.fromString<'a> json

[<RequireQualifiedAccess>]
module Storage =
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
            { urls = [ "www.ecosia.org/"; "duckduckgo.com/"; "www.startpage.com/" ]

              timePerUrl = 60<s> }

        Internal.getItem Internal.configKey
        |> function
            | Some configJson ->
                fromJson<Config> configJson
                |> function
                    | Ok config -> config
                    | Error _ -> buildDefault ()
            | None -> buildDefault ()

    let saveConfig (config: Config) =
        toJson 0 config |> Internal.setItem Internal.configKey
