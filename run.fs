open Fake.Core
open Fake.IO
open System.IO

module CreateProcess =
    let create cmd args =
        let args' = String.concat " " args
        printfn $"> %s{cmd} %s{args'}"
        CreateProcess.fromRawCommand cmd args

module Task =
    let restore () =
        let solution =
            Directory.EnumerateFiles(".", "*.sln")
            |> Seq.head

        CreateProcess.create "dotnet" [ "tool"; "restore" ]
        |> Proc.run |> ignore
        CreateProcess.create "dotnet" [ "restore"; solution ]
        |> Proc.run |> ignore

    let build () =
        Directory.EnumerateDirectories("modules")
        |> Seq.iter (fun folder ->
            CreateProcess.create "dotnet" [ "fable"; folder; "-e"; ".fs.js"; "-o"; $"%s{folder}/dist/" ]
            |> Proc.run |> ignore
            CreateProcess.create "pnpm" [ "exec"; "webpack-cli"; "--mode"; "production" ]
            |> CreateProcess.withWorkingDirectory folder
            |> Proc.run |> ignore
        )

    let pack () =
        let folder = "./out"

        Shell.cleanDir folder

        // We need the manifest, the assets and the scripts
        Shell.copy folder [ "manifest.json" ]
        [ "assets"; "dist" ]
        |> List.iter (fun dir -> Shell.copyDir $"%s{folder}/%s{dir}" dir (fun _ -> true))

        // Now we can bundle stuff together
        CreateProcess.create "pnpm" [ "exec"; "web-ext"; "build"; "-a"; folder]
        |> CreateProcess.withWorkingDirectory folder
        |> Proc.run |> ignore

module Command =
    let restore () = Task.restore (); 0

    let build () =
        restore () |> ignore; Task.build (); 0

    let pack () = build() |> ignore; Task.pack(); 0

[<EntryPoint>]
let main =
    function
    | [| "restore" |] ->
        Command.restore ()
    | [||]
    | [| "build" |] ->
        Command.build ()
    | [| "bundle" |]
    | [| "pack" |] ->
        Command.pack()
    | _ ->
        printfn "Invalid input"
        1
