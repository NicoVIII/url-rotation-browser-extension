namespace UrlRotation

open Elmish
open Elmish.React

module Options =
    Program.mkSimple Model.init Update.perform View.render
    |> Program.withReactSynchronous "elmish-app"
    |> Program.run
