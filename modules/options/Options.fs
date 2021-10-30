namespace UrlRotation

open Elmish
open Elmish.React
open Feliz
open Feliz.Bulma

type State = { urls: Map<int, string>; saved: bool }

type Msg =
    | ChangeUrl of key: int * value: string
    | Save

module Options =
    let init () =
        let config = Storage.loadConfig ()

        { urls = config.urls |> List.indexed |> Map.ofList
          saved = false }

    let update msg state =
        match msg with
        | ChangeUrl (key, value) ->
            let urls = Map.add key value state.urls

            { state with
                urls = urls
                saved = false }
        | Save ->
            Map.toList state.urls
            |> List.sortBy fst
            |> List.map snd
            |> (fun urls -> { Config.urls = urls })
            |> Storage.saveConfig

            { state with saved = true }

    let render state dispatch =
        // We always want one empty field for new urls
        let urls =
            state.urls
            // We don't save empty urls
            // TODO: check for validity
            |> Map.filter (fun _ value -> value <> "")
            |> Map.toList
            |> List.sortBy fst
            |> List.map snd
            |> (fun lst -> List.append lst [ "" ])
            |> List.indexed

        Bulma.columns [
            Bulma.column [
                column.is2
                prop.children [
                    Html.form [
                        prop.onSubmit (fun _ -> Save |> dispatch)
                        prop.children [
                            for (i, url) in urls do
                                Bulma.field.div [
                                    Bulma.control.div [
                                        Bulma.input.text [
                                            prop.placeholder "url"
                                            prop.value url
                                            prop.onChange (fun url -> ChangeUrl(i, url) |> dispatch)
                                        ]
                                    ]
                                ]
                            Bulma.button.button [
                                if state.saved then
                                    color.isSuccess
                                    prop.text "Saved"
                                else
                                    color.isPrimary
                                    prop.text "Save"
                            ]
                        ]
                    ]
                ]
            ]
        ]

    Program.mkSimple init update render
    |> Program.withReactSynchronous "elmish-app"
    |> Program.run
