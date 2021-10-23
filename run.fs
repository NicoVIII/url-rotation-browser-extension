open Fake.Core
open Fake.IO
open System.IO

module CreateProcess =
    let create cmd args =
        let args' = String.concat " " args
        printfn $"> %s{cmd} %s{args'}"
        CreateProcess.fromRawCommand cmd args

type ProcessResult =
    | Ok
    | Error

module ProcessResult =
    let toExitCode =
        function
        | Ok -> 0
        | Error -> 2

module Proc =
    let combine res1 f2 =
        match res1 with
        | Ok -> f2 ()
        | Error -> Error

    let run (proc: CreateProcess<ProcessResult<unit>>) =
        Proc.run proc
        |> (fun proc ->
            match proc.ExitCode with
            | 0 -> Ok
            | _ -> Error)

type JobBuilder() =
    member __.Yield x = x

    member __.Combine(res1, f2) = Proc.combine res1 f2

    member __.Delay f = f

    member __.Run f = f ()

let job = JobBuilder()

module Task =
    let restore () =
        let solution =
            Directory.EnumerateFiles(".", "*.sln") |> Seq.head

        job {
            CreateProcess.create "dotnet" [ "tool"; "restore" ]
            |> Proc.run

            CreateProcess.create "dotnet" [ "restore"; solution ]
            |> Proc.run

            CreateProcess.create "pnpm" [ "install" ]
            |> Proc.run
        }

    let build () =
        Directory.EnumerateDirectories("modules")
        |> Seq.map (fun folder ->
            fun () ->
                job {
                    CreateProcess.create
                        "dotnet"
                        [ "fable"
                          folder
                          "-e"
                          ".fs.js"
                          "-o"
                          $"%s{folder}/dist/" ]
                    |> Proc.run

                    CreateProcess.create
                        "pnpm"
                        [ "exec"
                          "webpack-cli"
                          "--mode"
                          "production" ]
                    |> CreateProcess.withWorkingDirectory folder
                    |> Proc.run
                })
        |> Seq.fold Proc.combine Ok

    let pack () =
        let folder = "./out"

        job {
            Shell.cleanDir folder

            // We need the manifest, the assets and the scripts
            Shell.copy folder [ "manifest.json" ]

            [ "assets"; "dist" ]
            |> List.iter (fun dir -> Shell.copyDir $"%s{folder}/%s{dir}" dir (fun _ -> true))

            // Now we can bundle stuff together
            CreateProcess.create
                "pnpm"
                [ "exec"
                  "web-ext"
                  "build"
                  "-a"
                  folder ]
            |> CreateProcess.withWorkingDirectory folder
            |> Proc.run
        }

module Command =
    let restore () = Task.restore ()

    let build () =
        job {
            restore ()
            Task.build ()
        }

    let pack () =
        job {
            build ()
            Task.pack ()
        }

[<EntryPoint>]
let main =
    function
    | [| "restore" |] -> Command.restore () |> ProcessResult.toExitCode
    | [||]
    | [| "build" |] -> Command.build () |> ProcessResult.toExitCode
    | [| "bundle" |]
    | [| "pack" |] -> Command.pack () |> ProcessResult.toExitCode
    | _ ->
        printfn "Invalid input"
        1
