namespace UrlRotation

open UrlRotation.JsWrapper

// Measures for Domainmodelling
[<Measure>]
type tabId

type TabId = int<tabId>

[<RequireQualifiedAccess>]
module TabId =
    let inline toInt (tabId: TabId) = int tabId

    let inline tryCreate value : TabId option =
        if value > 0 then
            value * 1<tabId> |> Some
        else
            None

    let inline create value : TabId = tryCreate value |> Option.get

type PlayState =
    { page: int
      currentTab: TabId
      tabs: TabId list
      intervalId: Interval.Id }

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

type State =
    { config: Config
      play: PlayState option }

module State =
    let create config = { config = config; play = None }
