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
            for modul in modules do
                Template.DotNet.restore modul
                Template.Pnpm.install ()
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

    let build mode =
        let modules =
            Directory.EnumerateFiles("modules", "*.fsproj", SearchOption.AllDirectories)

        job {
            for modul in modules do
                let folder = Path.GetDirectoryName(modul)

                // Build SASS/CSS
                sass [
                    "-I"
                    "node_modules/bulma"
                    "assets/sass/:dist/css/"
                ]

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

    let build () =
        job {
            restore ()
            Task.femto ()
            Task.build "development"
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
        | []
        | [ "build" ] -> Command.build ()
        | [ "bundle" ]
        | [ "pack" ] -> Command.pack ()
        | _ ->
            let msg = [ "Invalid input" ]
            Error(1, msg)
    |> ProcessResult.wrapUp
