open System
open System.IO

open Fake.Core
open Fake.IO

open RunHelpers
open RunHelpers.BasicShortcuts
open RunHelpers.FakeHelpers
open RunHelpers.Watch

module Process =
    let create cmd args = CreateProcess.fromRawCommand cmd args
    let runAsJob = Proc.runAsJob Constant.errorExitCode

[<AutoOpen>]
module Shortcuts =
    let sass args =
        Process.create "sass" args |> Process.runAsJob

module Task =
    let restore () =
        let modules =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)

        job {
            Template.DotNet.toolRestore ()
            Template.Pnpm.install ()

            for modul in modules do
                Template.DotNet.restore modul
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

    let buildSass () =
        sass [
            "-I"
            "node_modules/bulma"
            "sass/:dist/css/"
        ]

    let buildModule mode modul =
        job {
            let folder = Path.GetDirectoryName(modul: string)

            // Build F#/JavaScript
            dotnet [
                "fable"
                folder
                "-e"
                ".fs.js"
                "-o"
                $"%s{folder}/dist/"
            ]

            Process.create
                "pnpm"
                [ "exec"
                  "webpack-cli"
                  "--mode"
                  mode
                  "-c"
                  $"%s{folder}/webpack.config.js" ]
            |> CreateProcess.withWorkingDirectory folder
            |> Process.runAsJob
        }

    let buildModules mode =
        job {
            let modules =
                Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)

            for modul in modules do
                buildModule mode modul
        }

    let watch () =
        let setupWatcher =
            WatcherOptions.create ()
            |> WatcherOptions.excludeFolder "dist"
            |> setupWatcher

        // Register watchers
        let mode = "development"

        use sassWatcher = setupWatcher [ "sass/" ] buildSass

        use libWatcher =
            setupWatcher [ "lib/" ] (fun () ->
                printfn "Change!"
                buildModules mode)

        use moduleWatcher =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)
            |> Seq.toList
            |> List.map (fun modul -> setupWatcher [ Path.GetDirectoryName modul ] (fun () -> buildModule mode modul))
            |> FileSystemWatcherList.combine

        printfn "Waiting for changes... (enter to exit)"
        Console.ReadLine() |> ignore
        Ok

    let build mode =
        job {
            buildSass ()
            buildModules mode
        }

    let pack () =
        let folder = "./out"

        job {
            Shell.cleanDir folder

            // We need the manifest, the assets and the scripts
            Shell.copy folder [ "manifest.json" ]

            [ "assets"; "dist" ]
            |> List.iter (fun dir -> Shell.copyDir $"%s{folder}/%s{dir}" dir (fun _ -> true))

            // Now we can bundle stuff together
            Process.create
                "pnpm"
                [ "exec"
                  "web-ext"
                  "build"
                  "-a"
                  folder ]
            |> CreateProcess.withWorkingDirectory folder
            |> Process.runAsJob
        }

module Command =
    let restore () = Task.restore ()

    let subbuild () = Task.build "development"

    let subwatch () = Task.watch ()

    let watch () =
        job {
            restore ()
            Task.femto ()
            subbuild ()
            subwatch ()
        }

    let build () =
        job {
            restore ()
            Task.femto ()
            subbuild ()
        }

    let pack () =
        job {
            restore ()
            Task.femto ()
            Task.build "production"
            Task.pack ()
        }

[<EntryPoint>]
let main args =
    args
    |> List.ofArray
    |> function
        | [ "restore" ] -> Command.restore ()
        | [ "subbuild" ] -> Command.subbuild ()
        | []
        | [ "build" ] -> Command.build ()
        | [ "subwatch" ] -> Command.subwatch ()
        | [ "watch" ] -> Command.watch ()
        | [ "bundle" ]
        | [ "pack" ] -> Command.pack ()
        | _ ->
            let msg = [ "Invalid input" ]
            Error(1, msg)
    |> ProcessResult.wrapUp
