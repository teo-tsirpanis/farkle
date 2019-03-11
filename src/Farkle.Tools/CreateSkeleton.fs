// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tools.CreateSkeleton

open Argu
open Farkle
open Farkle.Grammar.GOLDParser
open Scriban
open Scriban.Runtime
open System
open System.IO

type SkeletonArguments =
    | [<ExactlyOnce; AltCommandLine("-g")>] GrammarFile of string
    | [<ExactlyOnce; AltCommandLine("-t")>] TemplateFile of string
    | [<Unique; AltCommandLine("-o")>] OutputFile of string
    | [<Unique; AltCommandLine("--list")>] ListTemplates
with
    interface IArgParserTemplate with
        member x.Usage =
            match x with
            | GrammarFile _ -> "specify the *.egt grammar file to parse. Required."
            | TemplateFile _ -> "specify the template file to use. Required. Use a colon (:) for stdin, and prefix a colon for the built-in templates."
            | OutputFile _ -> "specify where the generated output will be stored. Defaults to the template's name."
            | ListTemplates -> "list the built-in templates."

let doSkeleton (args: ParseResults<_>) =
    if args.Contains <@ ListTemplates @> then
        BuiltinTemplates.getAllBuiltins() |> Array.iter (printfn "%s")
    else
        let grammarFile = args.GetResult <@ GrammarFile @>
        let templateFile = args.GetResult <@ TemplateFile @>
        let outputFile = args.TryGetResult <@ OutputFile @>
        eprintfn "Creating a skeleton program from %A, based on template %A, to %A..." grammarFile templateFile outputFile

        let templateText = BuiltinTemplates.resolveInput(templateFile).ReadToEnd()
        let grammarBase64, grammar =
            let bytes = File.ReadAllBytes grammarFile
            use mem = new MemoryStream(bytes)
            let grammar = EGT.ofStream mem |> returnOrFail
            let grammarBase64 = Convert.ToBase64String bytes
            grammarBase64, grammar

        let template = Template.Parse(templateText, templateFile)
        let tc = TemplateContext()
        let so = ScriptObject()
        do
            let farkleObject = ScriptObject()
            farkleObject.Add("version", AssemblyVersionInformation.AssemblyVersion)
            so.Add("farkle", farkleObject)
        so.Add("grammar_base64", grammarBase64)
        let mutable fileExt = "out"
        so.Import("file_extension", Action<_>(fun x -> fileExt <- x))
        tc.PushGlobal so
        let output = template.Render(tc)

        let outputFile = outputFile |> Option.defaultWith (fun () -> Path.ChangeExtension(grammarFile, "." + fileExt))
        File.WriteAllText(outputFile, output)
