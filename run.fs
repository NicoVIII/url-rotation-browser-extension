open System
open System.IO

open Fake.Core
open Fake.IO

open RunHelpers
open RunHelpers.BasicShortcuts
open RunHelpers.Templates
open RunHelpers.Watch

module Process =
    let create cmd args = CreateProcess.fromRawCommand cmd args

[<AutoOpen>]
module Shortcuts =
    let sass args =
        Process.create "sass" args
        |> Job.fromCreateProcess

type BuildMode =
    | Debug
    | Release

module Task =
    let restore () =
        let modules =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)

        job {
            DotNet.toolRestore ()
            Pnpm.install ()

            for modul in modules do
                DotNet.restore modul
        }

    let femto () =
        let projects =
            [ "lib"; "modules" ]
            |> List.map (fun folder -> Directory.EnumerateFiles(folder, "*.fsproj", SearchOption.AllDirectories))
            |> List.reduce Seq.append

        job {
            for project in projects do
                dotnet [ "femto"; "--resolve"; project ]
        }

    let buildSass mode =
        let path = "dist/css/"
        printfn "Clean %s" path
        Shell.cleanDir path

        sass [
            if mode = Release then "--no-source-map"
            "-I"
            "node_modules/bulma"
            $"sass/:{path}"
        ]

    let buildModule mode modul =
        job {
            let folder = Path.GetDirectoryName(modul: string)

            let fableTarget = $"%s{folder}/dist/"
            printfn "- Clean %s" fableTarget
            Shell.cleanDir fableTarget

            // Build F#/JavaScript
            dotnet [
                "fable"
                folder
                "-e"
                ".fs.js"
                if mode = Debug then
                    "--define"
                    "DEBUG"
                "-o"
                $"%s{fableTarget}"
            ]

            Process.create
                "pnpm"
                [ "exec"
                  "webpack-cli"
                  "--mode"
                  match mode with
                  | Debug -> "development"
                  | Release -> "production"
                  "-c"
                  $"%s{folder}/webpack.config.js" ]
            |> CreateProcess.withWorkingDirectory folder
            |> Job.fromCreateProcess
        }

    let watch () =
        let setupWatcher =
            WatcherOptions.create ()
            |> WatcherOptions.excludeFolder "dist"
            |> setupWatcher

        // Register watchers
        let mode = Debug

        use _ = setupWatcher [ "sass/" ] (fun () -> buildSass Debug)

        use _ =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)
            |> Seq.toList
            |> List.map (fun modul ->
                setupWatcher [ "lib/"; Path.GetDirectoryName modul ] (fun () -> buildModule mode modul))
            |> FileSystemWatcherList.combine

        printfn "Waiting for changes... (enter to exit)"
        Console.ReadLine() |> ignore
        Job.ok

    let build mode =
        let modules =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)

        job {
            buildSass mode

            for modul in modules do
                buildModule mode modul
        }

    let pack () =
        let folder = "./out"

        job {
            printfn "Clean %s" folder
            Shell.cleanDir folder

            printfn "Copy files to %s..." folder
            // We need the manifest, the assets and the scripts
            Shell.copy folder [ "manifest.json" ]

            [ "assets"; "dist" ]
            |> List.iter (fun dir -> Shell.copyDir $"%s{folder}/%s{dir}" dir (fun _ -> true))

            // We remove the content_security_policy, which is only needed for development
            printfn "Remove csp line from manifest.json"

            File.ReadAllLines $"{folder}/manifest.json"
            |> Array.filter (fun line -> not (line.Contains("\"content_security_policy\"")))
            |> (fun content -> File.WriteAllLines($"{folder}/manifest.json", content))

            // At first we lint the source
            pnpm [
                "exec"
                "web-ext"
                "lint"
                "-s"
                folder
            ]

            // Now we can bundle stuff together
            pnpm [
                "exec"
                "web-ext"
                "build"
                "-s"
                folder
                "-a"
                folder
            ]
        }

[<EntryPoint>]
let main args =
    args
    |> List.ofArray
    |> function
        | [ "restore" ] -> Task.restore ()
        | [ "subbuild" ] -> Task.build Debug
        | []
        | [ "build" ] ->
            job {
                Task.restore ()
                Task.femto ()
                Task.build Debug
            }
        | [ "subwatch" ] -> Task.watch ()
        | [ "watch" ] ->
            job {
                Task.restore ()
                Task.femto ()
                Task.build Debug
                Task.watch ()
            }
        | [ "bundle" ]
        | [ "pack" ] ->
            job {
                Task.restore ()
                Task.femto ()
                Task.build Release
                Task.pack ()
            }
        | _ -> Job.error [ "Invalid input" ]
    |> Job.execute
