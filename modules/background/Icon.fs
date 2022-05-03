namespace UrlRotation

[<RequireQualifiedAccess>]
type Icon =
    | Play
    | Pause

module Icon =
    let getPath =
        function
        | Icon.Play -> "assets/icons/play-32.png"
        | Icon.Pause -> "assets/icons/pause-32.png"
