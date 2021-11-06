namespace UrlRotation

open System
open System.Collections.Generic

open Fable.Core
open Fable.Core.JsInterop

open UrlRotation.BrowserBindings

[<RequireQualifiedAccess>]
type Icon =
    | Play
    | Pause

module Icon =
    let getPath =
        function
        | Icon.Play -> "assets/icons/play-32.png"
        | Icon.Pause -> "assets/icons/pause-32.png"

// Measures for Domainmodelling
[<Measure>]
type tabId

type TabId = int<tabId>

[<RequireQualifiedAccess>]
module TabId =
    let inline create (value: int) = value * 1<tabId>
    let inline toInt (tabId: TabId) = int tabId

type PlayState =
    { page: int
      currentTab: TabId
      tabs: TabId list
      timeout: int }

module PlayState =
    let getNextTab play =
        play.tabs
        |> List.findIndex ((=) play.currentTab)
        |> (fun i -> (i + 1) % List.length play.tabs)
        |> (fun i -> List.item i play.tabs)

    let getNextPage page urlAmount = (page + 1) % urlAmount

    let getNextUrl page urls =
        List.item (getNextPage page (List.length urls)) urls

    let setPage value state = { state with page = value }

/// Used while data is collected
type PartialPlayState =
    { page: int
      currentTab: TabId option
      tabs: TabId list
      timeout: int option }

module PartialPlayState =
    let create page =
        { page = page
          currentTab = None
          tabs = []
          timeout = None }

    let inline toPlayState (partial: PartialPlayState) =
        match partial with
        | { currentTab = Some currentTab
            tabs = (_ :: _ :: _) as tabs
            timeout = Some timeout } as partial ->

            { PlayState.page = partial.page
              currentTab = currentTab
              tabs = tabs
              timeout = timeout }
            |> Some
        | _ -> None

type CurrentPlayState =
    | Creating of PartialPlayState
    | Ready of PlayState

module CurrentPlayState =
    let createFromPartial play =
        match PartialPlayState.toPlayState play with
        | Some play -> Ready play
        | None -> Creating play

type State =
    { config: Config
      play: CurrentPlayState option }

module State =
    let create config = { config = config; play = None }

type Msg =
    | SetTabs of TabId list
    | SetTimeout of int
    | SetCurrentTab of TabId
    | SetConfig of Config
    | PreparePlay
    | Play
    | Pause
    | SwitchPlay
    | NextPage
    | OnTabActivate of TabId
    | OnTabRemove of TabId

type Action =
    | NoAction
    | Actions of Action list
    | SetActionIcon of Icon
    | SendMsg of Msg
    | CloseTabs of TabId list
    | SetTimeout of Msg * int<s>
    | ClearTimeout of int
    | ActivateNextTab of TabId * TabId * string
    | OpenTabs of string list
    | LoadConfig of Msg

module Action =
    let combine a1 a2 =
        match a1, a2 with
        | Actions a1, Actions a2 -> [ a1; a2 ] |> List.concat
        | Actions actionList, action
        | action, Actions actionList -> [ yield! actionList; action ]
        | action1, action2 -> [ action1; action2 ]
        |> Actions

    let inline concat actions =
        match actions with
        | [] -> NoAction
        | [ action ] -> action
        | actions -> List.reduce combine actions

/// Some helpers for Promise handling
module Promise =
    let inline wrapValue value = promise { return value }

[<AutoOpen>]
module Functions =
    let setTimeout dispatch (time: int<s>) msg =
        JS.setTimeout (fun () -> dispatch msg) ((int time) * 1000)

    let loadConfig () =
        Storage.loadConfig ()
        |> (fun config ->
            { config with
                urls =
                    config.urls
                    |> List.map (fun url -> "https://" + url) })

module App =
    let update msg state =
        match msg with
        | SetTabs tabs ->
            let play =
                match state.play with
                | Some (Ready play) -> { play with tabs = tabs } |> Ready |> Some
                | Some (Creating play) ->
                    { play with tabs = tabs }
                    |> CurrentPlayState.createFromPartial
                    |> Some
                | None -> None

            { state with play = play }, NoAction
        | Msg.SetTimeout timeout ->
            let play =
                match state.play with
                | Some (Ready play) -> { play with timeout = timeout } |> Ready |> Some
                | Some (Creating play) ->
                    { play with timeout = Some timeout }
                    |> CurrentPlayState.createFromPartial
                    |> Some
                | None -> None

            { state with play = play }, NoAction
        | SetCurrentTab tabId ->
            let play =
                match state.play with
                | Some (Ready play) -> { play with currentTab = tabId } |> Ready |> Some
                | Some (Creating play) ->
                    { play with currentTab = Some tabId }
                    |> CurrentPlayState.createFromPartial
                    |> Some
                | None -> None

            { state with play = play }, NoAction
        | SetConfig config -> { state with config = config }, NoAction
        | PreparePlay ->
            let state =
                { state with play = PartialPlayState.create 0 |> Creating |> Some }

            let action =
                [ SetActionIcon Icon.Pause
                  LoadConfig(Play) ]
                |> Action.concat

            state, action
        | Play ->
            let action =
                [ [ 0; 1 ]
                  |> List.map (fun i -> List.item i state.config.urls)
                  |> OpenTabs
                  SetTimeout(NextPage, state.config.timePerUrl) ]
                |> Action.concat

            state, action
        | Pause ->
            let action =
                // We have to do most stuff only, when we are playing
                match state.play with
                | Some (Ready play) ->
                    [ ClearTimeout play.timeout
                      CloseTabs play.tabs ]
                | Some (Creating play) ->
                    [ match play.timeout with
                      | Some timeout -> ClearTimeout timeout
                      | None -> ()
                      match play.tabs with
                      | [] -> ()
                      | tabs -> CloseTabs tabs ]
                | None -> [ NoAction ]
                |> List.append [ SetActionIcon Icon.Play ]
                |> Action.concat

            { state with play = None }, action
        | SwitchPlay ->
            match state.play with
            | None -> state, SendMsg PreparePlay
            | Some _ -> state, SendMsg Pause
        | NextPage ->
            let play, action =
                match state.play with
                | Some (Ready play) ->
                    let currentTab = play.currentTab

                    let nextPage =
                        PlayState.getNextPage play.page (List.length state.config.urls)

                    let nextTab = PlayState.getNextTab play

                    let nextNextUrl =
                        PlayState.getNextUrl nextPage state.config.urls

                    let play =
                        { play with
                            currentTab = nextTab
                            page = nextPage }
                        |> Ready
                        |> Some

                    let action =
                        [ SetTimeout(NextPage, state.config.timePerUrl)
                          (currentTab, nextTab, nextNextUrl)
                          |> ActivateNextTab ]
                        |> Action.concat

                    play, action
                | Some (Creating _)
                | None -> None, NoAction

            { state with play = play }, action
        | OnTabActivate id ->
            match state.play with
            | Some (Ready { tabs = tabs })
            | Some (Creating { tabs = tabs }) when List.contains id tabs -> state, NoAction
            | Some (Creating _)
            | None -> state, NoAction
            | Some (Ready _) -> state, SendMsg Pause
        | OnTabRemove id ->
            match state.play with
            | Some (Ready play) when List.contains id play.tabs ->
                let play =
                    { play with tabs = play.tabs |> List.filter (fun tab -> tab <> id) }
                    |> Ready

                { state with play = Some play }, SendMsg Pause
            | Some _
            | None -> state, NoAction

    let rec processAction dispatch action =
#if DEBUG
        match action with
        | NoAction
        | Actions _ -> ()
        | action -> printfn "Triggered action: %A" action
#endif
        match action with
        | NoAction -> () |> Promise.wrapValue
        | SetActionIcon icon ->
            browser.browserAction.setIcon (!!{| path = Icon.getPath icon |})
            |> Promise.wrapValue
        | Actions actions ->
            promise {
                for action in actions do
                    do! processAction dispatch action
            }
        | SendMsg msg -> dispatch msg |> Promise.wrapValue
        | CloseTabs tabIds ->
            tabIds
            |> List.map TabId.toInt
            |> ResizeArray
            |> browser.tabs.remove
        | SetTimeout (msg, time) ->
            setTimeout dispatch time msg
            |> Msg.SetTimeout
            |> dispatch
            |> Promise.wrapValue
        | ClearTimeout timeoutId -> JS.clearTimeout timeoutId |> Promise.wrapValue
        | OpenTabs urls ->
            promise {
                let! tab =
                    !!{| active = true
                         url = List.item 0 urls |}
                    |> browser.tabs.create

                and! tab2 =
                    !!{| active = false
                         url = List.item 1 urls |}
                    |> browser.tabs.create

                [ tab.id.Value; tab2.id.Value ]
                |> List.map TabId.create
                |> SetTabs
                |> dispatch

                SetCurrentTab(TabId.create tab.id.Value)
                |> dispatch
            }
        | ActivateNextTab (currentTabId, nextTabId, nextNextUrl) ->
            promise {
                let! _ = browser.tabs.update (nextTabId |> TabId.toInt |> Some) !!{| active = true |}
                let! _ = browser.tabs.update (currentTabId |> TabId.toInt |> Some) !!{| url = nextNextUrl |}
                dispatch (SetCurrentTab nextTabId)
            }
        | LoadConfig msg ->
            promise {
                loadConfig () |> SetConfig |> dispatch
                dispatch msg
            }

    let registerListeners dispatch =
        // Start / Stop from browser action
        browser.browserAction.onClicked.addListener (fun _ -> dispatch SwitchPlay)
        // We stop, when one of the two tabs is closed
        browser.tabs.onRemoved.addListener (fun id _ -> id |> TabId.create |> OnTabRemove |> dispatch)
        // We stop, when the tab is changed
        browser.tabs.onActivated.addListener (fun info ->
            info.tabId
            |> TabId.create
            |> OnTabActivate
            |> dispatch)

    // Start of script
    let mutable running = false
    let mutable state = loadConfig () |> State.create
    let queue = new Queue<Msg>()

    let rec dispatch msg : unit =
#if DEBUG
        printfn "Send msg: %A" msg
#endif

        let rec processMsg msg =
            let state', action = update msg state
            state <- state'
            let promise = processAction dispatch action

            promise.``then`` (fun () ->
                if queue.Count > 0 then
                    queue.Dequeue() |> processMsg
                else
                    ())
            |> ignore

        if running then
            queue.Enqueue msg
        else
            running <- true
            processMsg msg
            running <- false

    registerListeners dispatch
