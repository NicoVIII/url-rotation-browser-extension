namespace UrlRotation

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

    /// Tracks if the tab activation is triggered by us
    let mutable private automaticSwitch = false
    let mutable private onTabActivate = fun _ -> failwith "Not initialized"
    let mutable private onTabRemove = fun _ -> failwith "Not initialized"

    // Shortcut for writing the state so we can hook into that and print debug messages
    let inline setState newState =
#if DEBUG
        printfn "New state: %A" newState
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
#if CHROME
        |> Promise.fromContinuation
#endif

    let setCurrentTab tabId =
        Option.iter
            (fun play ->
                let play = { play with currentTab = tabId }
                setState { state with play = Some play })
            state.play

    let setConfig config = setState { state with config = config }

    let createTab props =
#if CHROME
        browser.tabs.create props
        |> Promise.fromContinuation
#else
        browser.tabs.create props
#endif

    let updateTab tabId props =
#if CHROME
        browser.tabs.update tabId props
        |> Promise.fromContinuation
#else
        browser.tabs.update tabId props
#endif

    let openTabs urls =
        promise {
            let tab1Props =
                !!{| active = true
                     url = List.item 0 urls |}

            let tab2Props =
                !!{| active = false
                     url = List.item 1 urls |}

            let! tab = createTab tab1Props
            and! tab2 = createTab tab2Props

            let tabId1 = TabId.create tab.id.Value
            let tabId2 = TabId.create tab2.id.Value

            return (tabId1, tabId2)
        }

    let nextPage () =
#if DEBUG
        printfn "Next page"
#endif
        promise {
            match state.play with
            | Some play ->
                let currentTab = play.currentTab
                let nextPage = PlayState.getNextPage play.page (List.length state.config.urls)
                let nextTab = PlayState.getNextTab play
                let nextNextUrl = PlayState.getNextUrl nextPage state.config.urls

                automaticSwitch <- true
                // Switch to other tab
                let! _ = updateTab (nextTab |> TabId.toInt |> Some) !!{| active = true |}
                // Preload page in inactive tab
                let! _ = updateTab (currentTab |> TabId.toInt |> Some) !!{| url = nextNextUrl |}
                automaticSwitch <- false

                let play =
                    { play with
                        currentTab = nextTab
                        page = nextPage }

                setState { state with play = Some play }
            | None -> ()
        }

    let play () =
#if DEBUG
        printfn "Trigger play"
#endif
        promise {
            // We stop, when one of the two tabs is closed
            browser.tabs.onRemoved.addListener onTabRemove

            setActionIcon Icon.Pause
            loadConfig ()

            let tabs =
                [ 0; 1 ]
                |> List.map (fun i -> List.item i state.config.urls)

            let! (tabId1, tabId2) = openTabs tabs

            // We stop, when the tab is changed
            browser.tabs.onActivated.addListener onTabActivate

            let intervalId =
                Interval.set (nextPage >> Promise.ignore) (sToMs state.config.timePerUrl)

            let play =
                { page = 0
                  currentTab = tabId1
                  tabs = [ tabId1; tabId2 ]
                  intervalId = intervalId }

            setState { state with play = Some play }
        }

    let pause () =
#if DEBUG
        printfn "Trigger pause"
#endif
        promise {
            // We have to do most stuff only, when we are playing
            match state.play with
            | Some play ->
                // We remove the listeners again
                browser.tabs.onRemoved.removeListener onTabRemove
                browser.tabs.onActivated.removeListener onTabActivate

                Interval.clear play.intervalId
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

    onTabActivate <-
        fun (info: ActiveInfo) ->
#if DEBUG
            printfn "Event - tab activate"
#endif
            let tabId = info.tabId |> TabId.create

            // On Chrome we can't update directly here, because chrome thinks the
            // user is dragging a tab. Therefor we do this after a very short timeout
            let pause () =
                Timeout.set (fun () -> pause () |> Promise.ignore) 100<ms>
                |> ignore

            match state.play, automaticSwitch with
            | Some play, true when not (List.contains tabId play.tabs) ->
                // Some tab, which is not one of ours was activated, we pause
                pause ()
            | Some _, false ->
                // Some tab was activated, but it wasn't us -> we pause
                pause ()
            | _ -> ()

    onTabRemove <-
        fun id _ ->
#if DEBUG
            printfn "Event - tab remove: %i" id
#endif
            let id = TabId.create id

            match state.play with
            | Some play when List.contains id play.tabs ->
                let play = { play with tabs = play.tabs |> List.filter (fun tab -> tab <> id) }
                setState { state with play = Some play }

                pause () |> Promise.ignore
            | _ -> ()

module Startup =
    let registerListeners () =
        // Start / Stop from browser action
        browser.browserAction.onClicked.addListener App.onBrowserAction

    [<EntryPoint>]
    let run _ =
        registerListeners ()
        0
