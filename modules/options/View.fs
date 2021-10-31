namespace UrlRotation

open Feliz
open Feliz.Bulma

[<RequireQualifiedAccess>]
module View =
    let renderInput i (url: string) dispatch =
        Bulma.field.div [
            field.hasAddons
            prop.children [
                Bulma.control.div [
                    Bulma.button.button [
                        button.isStatic
                        prop.text "https://"
                    ]
                ]
                Bulma.control.div [
                    control.isExpanded
                    prop.children [
                        Bulma.input.text [
                            prop.placeholder "url"
                            prop.value url
                            prop.onChange (fun url -> ChangeUrl(i, url) |> dispatch)
                        ]
                    ]
                ]
            ]
        ]

    let renderForm state dispatch =
        // We always want one empty field for new urls
        let urls =
            state.urls
            // We don't save empty urls
            |> Map.toList
            |> List.sortBy fst
            |> List.map snd
            |> (fun lst -> List.append lst [ "" ])
            |> List.indexed

        Html.form [
            prop.onSubmit (fun e ->
                // We don't want the browser to handle the submit
                e.preventDefault ()
                Save |> dispatch)
            prop.children [
                for (i, url) in urls do
                    renderInput i url dispatch

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

    let render state dispatch =
        Bulma.columns [
            columns.isCentered
            columns.isMobile
            prop.children [
                Bulma.column [
                    column.isHalfDesktop
                    column.isThreeQuartersTablet
                    column.isFourFifthsMobile
                    prop.children [
                        Bulma.title "Settings"
                        renderForm state dispatch
                    ]
                ]
            ]
        ]
