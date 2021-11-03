namespace UrlRotation

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

type Msg =
    | NextPage
    | Pause
    | PauseIfExtensionTab of int
    | Play
    | SetState of State
    | SwitchPlay

type Action =
    | NoAction
    | SetActionIcon of Icon
    | WaitForPromise of ((Msg -> unit) -> unit)
    | Actions of Action list
    | SendMsg of Msg

module Action =
    let waitForPromise (getPromise: unit -> JS.Promise<'a>) (msg: 'a -> Msg) =
        fun dispatch ->
            let promise = getPromise ()
            promise.``then`` (msg >> dispatch) |> ignore
        |> WaitForPromise

    let combine a1 a2 =
        match a1, a2 with
        | Actions a1, Actions a2 -> [ a1; a2 ] |> List.concat
        | Actions actionList, action
        | action, Actions actionList -> [ yield! actionList; action ]
        | action1, action2 -> [ action1; action2 ]
        |> Actions

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
    let update setTimeout msg state : State * Action =
        match msg with
        | PauseIfExtensionTab id ->
            match state.play with
            | Some play when play.currTabId = id || play.nextTabId = id -> state, SendMsg Pause
            | Some _
            | None -> state, NoAction
        | SetState state -> state, NoAction
        | SwitchPlay ->
            match state.play with
            | None -> state, SendMsg Play
            | Some _ -> state, SendMsg Pause
        | NextPage ->
            state.play
            |> Option.iter (fun play ->
                let timeout =
                    setTimeout state.config.timePerUrl NextPage

                let play =
                    { play with
                        currTabId = play.nextTabId
                        nextTabId = play.currTabId
                        page = State.nextPage state
                        timeout = timeout }

                let state = { state with play = Some play }

                promise {
                    // Activate other tab
                    let! _ = browser.tabs.update (Some play.currTabId) !!{| active = true |}
                    let! _ = browser.tabs.update (Some play.nextTabId) !!{| url = State.getNextUrl state |}
                    return ()
                }
                |> ignore)

            state, NoAction
        | Play ->
            let action1 = SetActionIcon Icon.Pause

            // Reload config
            let state = { state with config = loadConfig () }

            // Open tabs
            let promise () =
                promise {
                    let! tab =
                        !!{| active = true
                             url = List.item 0 state.config.urls |}
                        |> browser.tabs.create

                    and! tab2 =
                        !!{| active = false
                             url = List.item 1 state.config.urls |}
                        |> browser.tabs.create

                    let timeout =
                        setTimeout state.config.timePerUrl NextPage

                    let playing =
                        { page = 0
                          currTabId = tab.id.Value
                          nextTabId = tab2.id.Value
                          timeout = timeout }

                    return { state with play = Some playing }
                }

            let action2 = Action.waitForPromise promise SetState

            state, Action.combine action1 action2
        | Pause ->
            // Stop timeout
            state.play
            |> Option.iter (fun play -> play.timeout |> JS.clearTimeout)

            let action = SetActionIcon Icon.Play

            { state with play = None }, action

    let rec processAction dispatch action =
        match action with
        | NoAction -> ()
        | SetActionIcon icon -> browser.browserAction.setIcon (!!{| path = Icon.getPath icon |})
        | WaitForPromise fnc -> fnc dispatch
        | Actions actions -> List.iter (processAction dispatch) actions
        | SendMsg msg -> dispatch msg

    let registerListeners dispatch =
        // Start / Stop from browser action
        browser.browserAction.onClicked.addListener (fun _ -> dispatch SwitchPlay)
        // We stop, when one of the two tabs is closed
        browser.tabs.onRemoved.addListener (fun id _ -> PauseIfExtensionTab id |> dispatch)

    // Start of script
    let mutable running = false
    let mutable state = loadConfig () |> State.create
    let queue = new Queue<Msg>()

    let rec dispatch msg : unit =
        let rec processMsg msg =
            let state', action = update (setTimeout dispatch) msg state
            state <- state'
            processAction dispatch action

            if queue.Count > 0 then
                queue.Dequeue() |> processMsg
            else
                ()

        if running then
            queue.Enqueue msg
        else
            running <- true
            processMsg msg
            running <- false

    registerListeners dispatch
