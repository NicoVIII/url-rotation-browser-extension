namespace UrlRotation

open Feliz
open Feliz.Bulma

[<RequireQualifiedAccess>]
module View =
    let renderTab state dispatch tab text =
        Bulma.tab [
            if (getl StateLens.tab state) = tab then
                Bulma.tab.isActive
            prop.children [
                Html.a [
                    prop.text (text: string)
                    prop.onClick (fun _ -> SetTab tab |> dispatch)
                ]
            ]
        ]

    let renderTabs state dispatch =
        let renderTab = renderTab state dispatch

        Bulma.tabs [
            tabs.isMedium
            prop.children [
                Html.ul [
                    renderTab Tab.GUI "GUI"
                    renderTab Tab.Json "Json"
                ]
            ]
        ]

    let renderTimeInput (time: int<s>) dispatch =
        Bulma.field.div [
            Bulma.label "Time per Url"
            Bulma.control.div [
                Bulma.field.div [
                    field.hasAddons
                    prop.children [
                        Bulma.control.div [
                            control.isExpanded
                            prop.children [
                                Bulma.input.number [
                                    prop.min 1
                                    prop.max 99999
                                    prop.value (int time)
                                    prop.onChange (
                                        LanguagePrimitives.Int32WithMeasure
                                        >> ChangeTime
                                        >> dispatch
                                    )
                                ]
                            ]
                        ]
                        Bulma.control.div [
                            Bulma.button.button [
                                button.isStatic
                                prop.text "s"
                            ]
                        ]
                    ]
                ]
            ]
        ]

    let renderUrlInput i (url: string) dispatch =
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
            state
            |> getl StateLens.urls
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
                Bulma.subtitle "General"
                renderTimeInput (getl StateLens.timePerUrl state) dispatch

                Bulma.subtitle "Urls"
                for (i, url) in urls do
                    renderUrlInput i url dispatch

                Bulma.button.button [
                    if getl StateLens.saved state then
                        color.isSuccess
                        prop.text "Saved"
                    else
                        color.isPrimary
                        prop.text "Save"
                ]
            ]
        ]

    let renderJsonView state =
        let json = getl StateLens.json state
        let rows = json.Split '\n' |> Array.length

        Bulma.control.div [
            control.isExpanded
            prop.children [
                Bulma.textarea [
                    textarea.hasFixedSize
                    prop.value json
                    prop.readOnly true
                    prop.rows rows
                ]
            ]
        ]

    let renderTabView state dispatch =
        match getl StateLens.tab state with
        | Tab.GUI -> renderForm state dispatch
        | Tab.Json -> renderJsonView state

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
                        renderTabs state dispatch
                        renderTabView state dispatch
                    ]
                ]
            ]
        ]
