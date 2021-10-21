open Fake.Core
open System.IO

module Proc =
    let create cmd args =
        let args' = String.concat " " args
        printfn $"> %s{cmd} %s{args'}"
        CreateProcess.fromRawCommand cmd args

module Task =
    let restore () =
        let solution =
            Directory.EnumerateFiles(".", "*.sln")
            |> Seq.head

        Proc.create "dotnet" [ "tool"; "restore" ]
        |> Proc.run |> ignore
        Proc.create "dotnet" [ "restore"; solution ]
        |> Proc.run |> ignore

    let build () =
        Directory.EnumerateDirectories("modules")
        |> Seq.iter (fun folder ->
            Proc.create "dotnet" [ "fable"; folder; "-e"; ".fs.js"; "-o"; $"%s{folder}/dist/" ]
            |> Proc.run |> ignore
            Proc.create "npx" [ "webpack"; "--mode"; "production" ]
            |> CreateProcess.withWorkingDirectory folder
            |> Proc.run |> ignore
        )

[<EntryPoint>]
let main =
    function
    | [| "restore" |] ->
        Task.restore ()
        0
    | [||]
    | [| "build" |] ->
        Task.restore ()
        Task.build ()
        0
    | _ ->
        printfn "Invalid input"
        1
