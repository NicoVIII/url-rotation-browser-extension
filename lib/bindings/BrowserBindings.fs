namespace UrlRotation

open Browser.Types
open Fable.Core

type BrowserEvent = Event

module BrowserBindings =
    module Internal =
        type Event<'Listener> =
            abstract addListener: 'Listener -> unit
            abstract removeListener: 'Listener -> unit
            abstract hasListener: 'Listener -> unit

        type BrowserActionClickEvent =
            abstract addListener: (BrowserEvent -> unit) -> unit

        type IconDetails =
            abstract path: string

        type BrowserAction =
            abstract onClicked: BrowserActionClickEvent
            abstract setIcon: IconDetails -> unit

        [<StringEnum>]
        type InstallEventReason =
            | Install
            | Update
#if CHROME
            | [<CompiledName("chrome-update")>] ChromeUpdate
#else
            | [<CompiledName("browser-update")>] BrowserUpdate
#endif
            | [<CompiledName("shared-module-update")>] SharedModuleUpdate

        type InstallEventDetails =
            abstract id: string option
            abstract previousVersion: string option
            abstract reason: InstallEventReason
            abstract temporary: bool

        type Runtime =
            abstract onInstalled: Event<InstallEventDetails -> unit>
            abstract openOptionsPage: unit -> unit

        type ActiveInfo =
            abstract previousTabId: int
            abstract tabId: int
            abstract windowId: int

        type RemoveInfo =
            abstract windowId: int
            abstract isWindowClosing: bool

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
                // Chrome uses Callbacks in MV2, but Firefox already uses Promises.
                // Chrome does it in MV3, which isn't supported in Firefox
#if CHROME
                abstract create: CreateProperties -> (Tab -> unit) -> unit
                //abstract remove: int -> (unit -> unit) -> unit // This causes a compiler error
                abstract remove: ResizeArray<int> -> (unit -> unit) -> unit
                abstract update: int option -> UpdateProperties -> (Tab -> unit) -> unit
#else
                abstract create: CreateProperties -> JS.Promise<Tab>
                abstract remove: int -> JS.Promise<unit>
                abstract remove: ResizeArray<int> -> JS.Promise<unit>
                abstract update: int option -> UpdateProperties -> JS.Promise<Tab>
#endif

                abstract onActivated: Event<ActiveInfo -> unit>
                abstract onRemoved: Event<int -> RemoveInfo -> unit>

        type Browser =
            abstract browserAction: BrowserAction
            abstract runtime: Runtime
            abstract tabs: Tabs.T

#if CHROME
    [<Global>]
    let chrome: Internal.Browser = jsNative

    let browser = chrome
#else
    [<Global>]
    let browser: Internal.Browser = jsNative
#endif
