namespace UrlRotation

open Fable.Core
open Fable.Core.JsInterop
open UrlRotation.BrowserBindings

type PlayState =
    { page: int
      currTabId: int
      nextTabId: int
      timeout: int }

type State =
    { config: Config
      play: PlayState option }

module State =
    let create config = { config = config; play = None }

    let setPage value state = { state with page = value }

    let nextPage state =
        (state.play.Value.page + 1) % state.config.urls.Length

    let getNextUrl state = state.config.urls.[nextPage state]

[<AutoOpen>]
module Functions =
    let setTimeout (time: int<s>) fnc = JS.setTimeout fnc ((int time) * 1000)

    let loadConfig () =
        Storage.loadConfig ()
        |> (fun config ->
            { config with
                urls =
                    config.urls
                    |> List.map (fun url -> "https://" + url) })

module App =
    let mutable state = loadConfig () |> State.create

    let pause () =
        browser.browserAction.setIcon (!!{| path = "assets/icons/play-32.png" |})

        // Stop timeout
        state.play
        |> Option.iter (fun play -> play.timeout |> JS.clearTimeout)

        state <- { state with play = None }

    let rec nextPage () =
        state.play
        |> Option.iter (fun play ->
            promise {
                let timeout =
                    setTimeout state.config.timePerUrl nextPage

                let play =
                    { play with
                        currTabId = play.nextTabId
                        nextTabId = play.currTabId
                        page = State.nextPage state
                        timeout = timeout }

                state <- { state with play = Some play }

                // Activate other tab
                let! _ = browser.tabs.update (Some play.currTabId) !!{| active = true |}
                let! _ = browser.tabs.update (Some play.nextTabId) !!{| url = State.getNextUrl state |}
                return ()
            }
            |> ignore)

    let play () =
        promise {
            browser.browserAction.setIcon (!!{| path = "assets/icons/pause-32.png" |})

            // Reload config
            state <- { state with config = loadConfig () }

            let! tab =
                !!{| active = true
                     url = List.item 0 state.config.urls |}
                |> browser.tabs.create

            and! tab2 =
                !!{| active = false
                     url = List.item 1 state.config.urls |}
                |> browser.tabs.create

            // We stop, when one of the two tabs is closed
            browser.tabs.onRemoved.addListener (fun id _ ->
                match state.play with
                | Some play ->
                    if play.currTabId = id || play.nextTabId = id then
                        pause ()
                | None -> ())

            let timeout =
                setTimeout state.config.timePerUrl nextPage

            let playing =
                { page = 0
                  currTabId = tab.id.Value
                  nextTabId = tab2.id.Value
                  timeout = timeout }

            state <- { state with play = Some playing }
        }
        |> ignore

    // Start / Stop from browser action
    browser.browserAction.onClicked.addListener (fun _ ->
        match state.play with
        | Some _ -> pause ()
        | None -> play ())
