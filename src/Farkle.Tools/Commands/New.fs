// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.Commands.New

open Argu
open Farkle.Monads.Either
open Farkle.Tools
open Farkle.Tools.Templating
open Farkle.Tools.Templating.BuiltinTemplates
open Serilog
open System
open System.IO
open System.Text.Json

type Arguments =
    | [<Unique; MainCommand>] GrammarFile of string
    | [<Unique; AltCommandLine("-lang")>] Language of Language
    | [<Unique>] Type of TemplateType
    | [<Unique; AltCommandLine("-t")>] TemplateFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
    | [<Unique; AltCommandLine("-ns")>] Namespace of string
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "The EGT grammar file to parse. \
Otherwise, the EGT file in the current directory, if only one exists."
            | Language _ -> "Specifies the language of the template to create. If there is a C# or F# project, \
defaults to this language. If there are both, the language must be specified. If there is neither, defaults to F#."
            | Type _ -> "Specifies the type of the template to create. Currently, only a file containing the grammar \
and its symbol types is allowed."
            | TemplateFile _ -> "Specifies the template file to use, in case you want a custom one. \
In this case, the language is completely ignored."
            | OutputFile _ -> "Specifies where the generated output will be stored. \
Defaults to the grammar's name and extension, with a suffix set by the template, which defaults to 'out'."
            | Namespace _ -> "Specifies the namespace of the generated source file. \
It can be retrieved from the template with the 'namespace' variable."

let tryInferGrammarFile() =
    Environment.CurrentDirectory
    |> Directory.EnumerateFiles
    |> Seq.filter (Path.GetExtension >> (=) ".egt")
    |> List.ofSeq
    |> function
    | [x] ->
        Log.Debug("No grammar file was specified; using {GrammarFile}", x)
        Ok x
    | [] ->
        Log.Error("Could not find an EGT file in {CurrentDirectory}", Environment.CurrentDirectory)
        Error()
    | _ ->
        Log.Error("More than one EGT files were found in {CurrentDirectory}", Environment.CurrentDirectory)
        Error()

let tryInferLanguage() =
    let hasExtension =
        let files = Directory.GetFiles(Environment.CurrentDirectory)
        fun ext -> files |> Array.exists (fun path -> Path.GetExtension(path) = ext)
    match hasExtension ".csproj", hasExtension ".fsproj" with
    | true, true ->
        Log.Error("Cannot infer the language to use; there are both C# and F# projects in {CurrentDirectory}", Environment.CurrentDirectory)
        Error()
    | true, false ->
        Log.Debug("No language was specified; inferred to be C#, as there are C# projects in {CurrentFirectory}", Environment.CurrentDirectory)
        Ok Language.``C#``
    | false, true ->
        Log.Debug("No language was specified; inferred to be F#, as there are F# projects in {CurrentFirectory}", Environment.CurrentDirectory)
        Ok Language.``F#``
    | false, false ->
        Log.Debug("Neither a language was specified, nor are there any supported projcets in {CurrentDirectory}. Language is inferred to be F#", Environment.CurrentDirectory)
        Ok Language.``F#``

let toCustomFile fileName =
    fileName
    |> assertFileExists
    |> Result.map CustomFile

let run json (args: ParseResults<_>) = either {
    let! grammarFile =
        args.TryPostProcessResult(GrammarFile, assertFileExists)
        |> Option.defaultWith tryInferGrammarFile
    let typ = args.GetResult(Type, defaultValue = TemplateType.Grammar)
    let ns = args.TryGetResult(Namespace)
    let! templateSource =
        args.TryPostProcessResult(TemplateFile, toCustomFile)
        |> Option.defaultWith (fun () ->
            args.TryPostProcessResult(Language, Ok)
            |> Option.defaultWith tryInferLanguage
            |> Result.map (fun lang -> BuiltinTemplate(lang, typ)))

    let! generatedTemplate =
        TemplateEngine.renderTemplate Log.Logger ns grammarFile templateSource

    let outputFile =
        args.TryGetResult OutputFile
        |> Option.defaultWith (fun () -> Path.ChangeExtension(grammarFile, generatedTemplate.FileExtension))
        |> Path.GetFullPath

    if json then
        {|outputFile = outputFile; content = generatedTemplate.Content|}
        |> JsonSerializer.Serialize
        |> printfn "%s"
    else
        Log.Verbose("Creating file at {outputFile}", outputFile)
        File.WriteAllText(outputFile, generatedTemplate.Content)

        Log.Information("Template was created at {outputFile}", outputFile)
}
