open Fake.Core
open Fake.IO
open System.IO

module CreateProcess =
    let create cmd args = CreateProcess.fromRawCommand cmd args

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
        printfn $"> %s{proc.CommandLine}"

        Proc.run proc
        |> (fun proc ->
            match proc.ExitCode with
            | 0 -> Ok
            | _ -> Error)

type JobBuilder() =
    member __.Combine(res1, f2) = Proc.combine res1 f2

    member __.Delay f = f

    member __.For(lst, f) =
        lst
        |> Seq.fold (fun res1 el -> Proc.combine res1 (fun () -> f el)) Ok

    member __.Run f = f ()

    member __.Yield x = x
    member __.Zero() = Ok

let job = JobBuilder()

let dotnet args =
    CreateProcess.create "dotnet" args |> Proc.run

let pnpm args =
    CreateProcess.create "pnpm" args |> Proc.run

module Task =
    open Microsoft.VisualBasic.FileIO

    let restore () =
        let solution =
            Directory.EnumerateFiles(".", "*.sln") |> Seq.head

        job {
            dotnet [ "tool"; "restore" ]
            dotnet [ "restore"; solution ]
            pnpm [ "install" ]
        }

    let femto () =
        let projects =
            [ "lib"; "modules" ]
            |> List.map (fun folder -> Directory.EnumerateFiles(folder, "*.fsproj", SearchOption.AllDirectories))
            |> List.reduce Seq.append

        job {
            for project in projects do
                dotnet [ "femto"; project ]
        }

    let build mode =
        Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)
        |> Seq.map (fun project ->
            let folder = Path.GetDirectoryName(project)

            fun () ->
                job {
                    dotnet [ "fable"
                             folder
                             "-e"
                             ".fs.js"
                             "-o"
                             $"%s{folder}/dist/" ]

                    CreateProcess.create
                        "pnpm"
                        [ "exec"
                          "webpack-cli"
                          "--mode"
                          mode
                          "-c"
                          $"%s{folder}/webpack.config.js" ]
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

    let check_dependencies () =
        job {
            restore ()
            Task.femto ()
        }

    let build' mode =
        job {
            check_dependencies ()
            Task.build mode
        }

    let build () = build' "development"
    let build_for_prod () = build' "production"

    let pack () =
        job {
            build_for_prod ()
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
