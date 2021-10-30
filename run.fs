open System
open System.IO

open Fake.Core
open Fake.IO

open RunHelpers
open RunHelpers.BasicShortcuts
open RunHelpers.FakeHelpers

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
            "assets/sass/:dist/css/"
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

    // Helpertype for multiple FileSystemWatchers
    type FileSystemWatcherList =
        { watchers: FileSystemWatcher list }
        interface IDisposable with
            member this.Dispose() =
                this.watchers
                |> List.iter (fun watcher -> watcher.Dispose())

    module FileSystemWatcherList =
        let create watchers = { watchers = watchers }

        let combine watcherLists =
            watcherLists
            |> List.map (fun lst -> lst.watchers)
            |> List.concat
            |> create

    let watch () =
        let setupWatcher folders onChange =
            let filter = [ "/bin/"; "/obj/"; "/dist/" ]

            let mutable working = false
            let mutable changedWhileWorking = false

            let disable (watcher: FileSystemWatcher) = watcher.EnableRaisingEvents <- false
            let enable (watcher: FileSystemWatcher) = watcher.EnableRaisingEvents <- true

            let watchers =
                folders
                |> List.map (fun folder ->
                    let watcher = new FileSystemWatcher(folder)
                    watcher.IncludeSubdirectories <- true
                    watcher)

            let rec work () =
                working <- true
                onChange () |> ignore

                if changedWhileWorking then
                    changedWhileWorking <- false
                    printfn "- Do it again, there were changes while running"
                    work ()
                else
                    printfn "- Waiting for changes... (enter to exit)"
                    working <- false

            let handler (args: FileSystemEventArgs) =
                let filtered =
                    filter |> List.exists (args.FullPath.Contains)

                if not filtered then
                    List.iter disable watchers

                    let working =
                        async {
                            if working then
                                changedWhileWorking <- true
                            else
                                work ()
                        }

                    let debouncer =
                        async {
                            do! Async.Sleep(500)
                            List.iter enable watchers
                        }

                    [ working; debouncer ]
                    |> Async.Parallel
                    |> Async.Ignore
                    |> Async.RunSynchronously

            // Register handler
            List.iter
                (fun (watcher: FileSystemWatcher) ->
                    watcher.Changed.Add handler
                    watcher.Created.Add handler
                    watcher.Deleted.Add handler)
                watchers

            // Enable Watchers
            List.iter enable watchers
            FileSystemWatcherList.create watchers

        // Register watchers
        let mode = "development"

        use sassWatcher =
            setupWatcher [ "assets/sass/" ] buildSass

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
