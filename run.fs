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

            let fableTarget = $"%s{folder}/dist/"
            printfn "- Clean %s" fableTarget
            Shell.cleanDir fableTarget

            // Build F#/JavaScript
            dotnet [
                "fable"
                folder
                "-e"
                ".fs.js"
                "-o"
                $"%s{fableTarget}"
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

    let watch () =
        let setupWatcher =
            WatcherOptions.create ()
            |> WatcherOptions.excludeFolder "dist"
            |> setupWatcher

        // Register watchers
        let mode = "development"

        use sassWatcher = setupWatcher [ "sass/" ] buildSass

        use moduleWatcher =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)
            |> Seq.toList
            |> List.map (fun modul ->
                setupWatcher [ "lib/"; Path.GetDirectoryName modul ] (fun () -> buildModule mode modul))
            |> FileSystemWatcherList.combine

        printfn "Waiting for changes... (enter to exit)"
        Console.ReadLine() |> ignore
        Ok

    let build mode =
        let modules =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)

        job {
            buildSass ()

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
