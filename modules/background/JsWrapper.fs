namespace UrlRotation

/// Module which wraps some js apis in a more typesafe way
module JsWrapper =
    open Fable.Core

    module Promise =
        let ignore (promise: JS.Promise<'a>) = promise |> ignore

    module Timeout =
        [<Measure>]
        type private id

        type Id = int<id>

        module Id =
            let create id : Id = id * 1<id>
            let unwrap (id: Id) = id / 1<id>

        let set callback (time: int<s>) =
            JS.setTimeout callback (1000 * int time)
            |> Id.create

        let clear timeoutId = JS.clearTimeout (Id.unwrap timeoutId)
