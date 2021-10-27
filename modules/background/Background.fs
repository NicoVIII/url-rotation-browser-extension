namespace UrlRotation

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.SimpleJson
open UrlRotation.BrowserBindings

type PlayState =
    { page: int
      currTabId: int
      nextTabId: int
      timeout: int }

type State =
    { play: PlayState option
      urls: string list }

[<RequireQualifiedAccess>]
module Storage =
    open Browser

    let getItem key =
        // Sadly the fable bindings are not at all nullsafe..
        let item = localStorage.getItem key
        if isNull item then None else Some item

    let setItem key data = localStorage.setItem (key, data)

module State =
    let create urls = { play = None; urls = urls }

    let setPage value state = { state with page = value }

    let nextPage state =
        (state.play.Value.page + 1) % state.urls.Length

    let getNextUrl state = state.urls.[nextPage state]

module App =
    let mutable state =
        Storage.getItem "url_list"
        |> function
            | Some url_string -> Json.parseAs<string list> url_string
            | None ->
                [ "https://www.ecosia.org/"
                  "https://duckduckgo.com/"
                  "https://www.startpage.com/" ]
        |> State.create

    let pause () =
        // Stop timeout
        state.play
        |> Option.iter (fun play -> play.timeout |> JS.clearTimeout)

        state <- { state with play = None }

    let rec nextPage () =
        state.play
        |> Option.iter (fun play ->
            promise {
                let timeout = JS.setTimeout nextPage 10000

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
            let! tab =
                !!{| active = true
                     url = List.item 0 state.urls |}
                |> browser.tabs.create

            and! tab2 =
                !!{| active = false
                     url = List.item 1 state.urls |}
                |> browser.tabs.create

            let timeout = JS.setTimeout nextPage 10000

            let playing =
                { page = 0
                  currTabId = tab.id.Value
                  nextTabId = tab2.id.Value
                  timeout = timeout }

            state <- { state with play = Some playing }
        }
        |> ignore

    browser.browserAction.onClicked.addListener (fun _ ->
        match state.play with
        | Some _ -> pause ()
        | None -> play ())
