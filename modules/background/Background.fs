namespace UrlRotation

open Fable.Core
open Fable.Core.JsInterop

open UrlRotation.BrowserBindings
open UrlRotation.BrowserBindings.Internal
open UrlRotation.JsWrapper

/// Some helpers for Promise handling
module Promise =
    let inline wrapValue value = promise { return value }

[<AutoOpen>]
module Functions =
    let loadConfig () =
        Storage.loadConfig ()
        |> (fun config ->
            { config with
                urls =
                    config.urls
                    |> List.map (fun url -> "https://" + url) })

// Here starts the mutable code
module App =
    let mutable private state = loadConfig () |> State.create

    // Shortcut for writing the state so we can hook into that and print debug messages
    let inline setState newState =
#if DEBUG
        printfn "new state: %A" newState
#endif
        state <- newState

    let setActionIcon icon =
        browser.browserAction.setIcon (!!{| path = Icon.getPath icon |})

    let loadConfig () =
        setState { state with config = loadConfig () }

    let closeTabs tabIds =
        tabIds
        |> List.map TabId.toInt
        |> ResizeArray
        |> browser.tabs.remove

    let setCurrentTab tabId =
        Option.iter
            (fun play ->
                let play = { play with currentTab = tabId }
                setState { state with play = Some play })
            state.play

    let setConfig config = setState { state with config = config }

    let openTabs urls =
        promise {
            let! tab =
                !!{| active = true
                     url = List.item 0 urls |}
                |> browser.tabs.create

            and! tab2 =
                !!{| active = false
                     url = List.item 1 urls |}
                |> browser.tabs.create

            let tabId1 = TabId.create tab.id.Value
            let tabId2 = TabId.create tab2.id.Value

            return (tabId1, tabId2)
        }

    let nextPage () =
        promise {
            match state.play with
            | Some play ->
                let currentTab = play.currentTab
                let nextPage = PlayState.getNextPage play.page (List.length state.config.urls)
                let nextTab = PlayState.getNextTab play
                let nextNextUrl = PlayState.getNextUrl nextPage state.config.urls

                // Switch to other tab
                let! _ = browser.tabs.update (nextTab |> TabId.toInt |> Some) !!{| active = true |}
                // Preload page in inactive tab
                let! _ = browser.tabs.update (currentTab |> TabId.toInt |> Some) !!{| url = nextNextUrl |}

                let play =
                    { play with
                        currentTab = nextTab
                        page = nextPage }

                setState { state with play = Some play }
            | None -> ()
        }

    let play () =
        promise {
            setActionIcon Icon.Pause
            loadConfig ()

            let tabs =
                [ 0; 1 ]
                |> List.map (fun i -> List.item i state.config.urls)

            let! (tabId1, tabId2) = openTabs tabs

            let timeoutId = Timeout.set (nextPage >> Promise.ignore) state.config.timePerUrl

            let play =
                { page = 0
                  currentTab = tabId1
                  tabs = [ tabId1; tabId2 ]
                  timeout = timeoutId }

            setState { state with play = Some play }
        }

    let pause () =
        promise {
            // We have to do most stuff only, when we are playing
            match state.play with
            | Some play ->
                Timeout.clear play.timeout
                do! closeTabs play.tabs
            | None -> ()

            setState { state with play = None }
            setActionIcon Icon.Play
        }

    let switchPlay () =
        promise {
            match state.play with
            | None -> do! play ()
            | Some _ -> do! pause ()
        }

    let onBrowserAction (_: BrowserEvent) = switchPlay () |> Promise.ignore

    let onTabActivate (info: ActiveInfo) =
        let tabId = info.tabId |> TabId.create
#if DEBUG
        printfn "Event - tab activate"
#endif
        match state.play with
        | Some play when not (List.contains tabId play.tabs) -> pause () |> Promise.ignore
        | _ -> ()

    let onTabRemove id _ =
#if DEBUG
        printfn "Event - tab remove: %i" id
#endif
        let id = TabId.create id

        match state.play with
        | Some play when List.contains id play.tabs ->
            let play = { play with tabs = play.tabs |> List.filter (fun tab -> tab <> id) }
            setState { state with play = Some play }
            pause () |> Promise.ignore
        | Some _
        | None -> ()

module Startup =
    let registerListeners () =
        // Start / Stop from browser action
        browser.browserAction.onClicked.addListener App.onBrowserAction
        // We stop, when one of the two tabs is closed
        browser.tabs.onRemoved.addListener App.onTabRemove
        // We stop, when the tab is changed
        browser.tabs.onActivated.addListener App.onTabActivate

    [<EntryPoint>]
    let run _ =
        registerListeners ()
        0
