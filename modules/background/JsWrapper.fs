namespace UrlRotation

/// Module which wraps some js apis in a more typesafe way
module JsWrapper =
    open Fable.Core

    module Interval =
        [<Measure>]
        type private id

        type Id = int<id>

        module Id =
            let create id : Id = id * 1<id>
            let unwrap (id: Id) = id / 1<id>

        let set callback (time: int<ms>) =
            JS.setInterval callback (int time) |> Id.create

        let clear timeoutId = JS.clearInterval (Id.unwrap timeoutId)

    module Promise =
        let ignore (promise: JS.Promise<'a>) = promise |> ignore

    module Timeout =
        [<Measure>]
        type private id

        type Id = int<id>

        module Id =
            let create id : Id = id * 1<id>
            let unwrap (id: Id) = id / 1<id>

        let set callback (time: int<ms>) =
            JS.setTimeout callback (int time) |> Id.create

        let clear timeoutId = JS.clearTimeout (Id.unwrap timeoutId)
