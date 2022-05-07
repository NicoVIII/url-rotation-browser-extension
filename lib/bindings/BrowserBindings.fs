namespace UrlRotation

open Browser.Types
open Fable.Core

type BrowserEvent = Event

module BrowserBindings =
    module Internal =
        type BrowserActionClickEvent =
            abstract addListener: (BrowserEvent -> unit) -> unit

        type IconDetails =
            abstract path: string

        type BrowserAction =
            abstract onClicked: BrowserActionClickEvent
            abstract setIcon: IconDetails -> unit

        type Runtime =
            abstract openOptionsPage: unit -> unit

        type ActiveInfo =
            abstract previousTabId: int
            abstract tabId: int
            abstract windowId: int

        type RemoveInfo =
            abstract windowId: int
            abstract isWindowClosing: bool

        type TabsActivateEvent =
            abstract addListener: (ActiveInfo -> unit) -> unit
            abstract removeListener: (ActiveInfo -> unit) -> unit

        type TabsRemoveEvent =
            abstract addListener: (int * RemoveInfo -> unit) -> unit
            abstract removeListener: (int * RemoveInfo -> unit) -> unit

        module Tabs =
            [<StringEnum>]
            type MutedInfoReason =
                | Capture
                | Extension
                | User

            type MutedInfo =
                abstract extensionId: string option
                abstract muted: bool
                abstract reason: MutedInfoReason option

            type Tab =
                abstract active: bool
                abstract attention: bool option
                abstract audible: bool option
                abstract autoDiscardable: bool option
                abstract cookieStoreId: string option
                abstract discarded: bool option
                abstract favIconUrl: string option
                abstract height: int option
                abstract hidden: bool
                abstract highlighted: bool
                abstract id: int option
                abstract incognito: bool
                abstract index: int
                abstract isArticle: bool
                abstract isInReaderMode: bool
                abstract lastAccessed: double
                abstract mutedInfo: MutedInfo option
                abstract openerTabId: int option
                abstract pinned: bool
                abstract sessionId: string option
                abstract status: string option
                abstract successorTabId: int option
                abstract title: string option
                abstract url: string option
                abstract width: int option
                abstract windowId: int

            type CreateProperties =
                abstract active: bool option
                abstract cookieStoreId: string option
                abstract discarded: bool option
                abstract index: int option
                abstract openerTabId: int option
                abstract openInReaderMode: bool option
                abstract pinned: bool option
                abstract title: string option
                abstract url: string option
                abstract windowId: int option

            type UpdateProperties =
                abstract active: bool option
                abstract autoDiscardable: bool option
                abstract hightlighted: bool option
                abstract loadReplace: bool option
                abstract muted: bool option
                abstract openerTabId: int option
                abstract pinned: bool option
                abstract url: string option

            type T =
                abstract create: CreateProperties -> JS.Promise<Tab>
                abstract remove: int -> JS.Promise<unit>
                abstract remove: ResizeArray<int> -> JS.Promise<unit>
                abstract update: int option -> UpdateProperties -> JS.Promise<Tab>

                abstract onActivated: TabsActivateEvent
                abstract onRemoved: TabsRemoveEvent

        type Browser =
            abstract browserAction: BrowserAction
            abstract runtime: Runtime
            abstract tabs: Tabs.T

    [<Global>]
    let browser: Internal.Browser = jsNative
