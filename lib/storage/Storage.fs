namespace UrlRotation

open Browser

[<RequireQualifiedAccess>]
module Storage =
    open Browser

    let getItem key =
        // Sadly the fable bindings are not at all nullsafe..
        let item = localStorage.getItem key
        if isNull item then None else Some item

    let setItem key data = localStorage.setItem (key, data)
