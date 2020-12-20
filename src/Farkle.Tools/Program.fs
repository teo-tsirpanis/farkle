﻿// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

open Argu
open Farkle.Tools
open Farkle.Tools.Commands
open Serilog
open Serilog.Events
open System

type FarkleCLIExiter() =
    interface IExiter with
        member _.Name = "Farkle CLI exiter"
        member _.Exit(msg, code) =
            Console.Error.WriteLine(msg)
            exit <| int code

type Arguments =
    | Version
    | [<Inherit>] Json
    | [<Inherit; AltCommandLine("-v"); Unique>] Verbosity of Events.LogEventLevel
    | [<CliPrefix(CliPrefix.None)>] New of ParseResults<New.Arguments>
    | [<CliPrefix(CliPrefix.None)>] List of ParseResults<List.Arguments>
    | [<AltCommandLine("generate-predefined-sets"); Hidden>] GeneratePredefinedSets of ParseResults<GeneratePredefinedSets.Arguments>
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | Version -> "Display the program's version info."
            | Json -> "Encode output in JSON and print in in a single line in stdout. \
No files will be created and only errors will be logged by default."
            | Verbosity _ -> "Set the verbosity of the tool's logs."
            | New _ -> "Generate a skeleton program from a grammar file and a Scriban template."
            | List _ -> "List all precompiled grammars of an assembly"
            | GeneratePredefinedSets _ -> "Generate an F# source file with GOLD Parser's predefined sets. \
For internal use only."

[<EntryPoint>]
let main _ =
    let parser = ArgumentParser.Create("farkle", "Help was requested", errorHandler = FarkleCLIExiter())
    let results = parser.Parse()
    let json = results.Contains Json
    let verbosity =
        results.TryGetResult(Verbosity)
        |> Option.defaultValue (if json then LogEventLevel.Error else LogEventLevel.Information)
    Log.Logger <- LoggerConfiguration()
        .MinimumLevel.Is(verbosity)
        .WriteTo.Console()
        .CreateLogger()

    try
        try
            if results.Contains Version then
                Log.Information("Version: {toolsVersion}", toolsVersion)
                0
            else
                match results.GetSubCommand() with
                | New args -> New.run json args
                | GeneratePredefinedSets args -> GeneratePredefinedSets.run args
                | List args -> List.run json args
                | Version _ | Json | Verbosity _ -> Ok ()
                |> function | Ok () -> 0 | Error () -> 1
        with
        | ex ->
            Log.Fatal(ex, "Exception occured")
            1
    finally
        Log.CloseAndFlush()
