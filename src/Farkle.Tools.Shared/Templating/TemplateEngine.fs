// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Monads.Either
open Farkle.Tools
open Scriban
open Scriban.Parsing
open Scriban.Runtime
open Serilog
open System.IO

module TemplateEngine =

    let private (|LanguageNames|) lang =
        match lang with
        | Language.``F#`` -> "FSharp", "F# grammar skeleton template"
        | Language.``C#`` -> "CSharp", "C# grammar skeleton template"

    let private parseTemplate (log: ILogger) templateText templateFileName =
        log.Debug("Parsing {TemplateFileName}", templateFileName.ToString())
        let template = Template.Parse(templateText, templateFileName)
        for x in template.Messages do
            match x.Type with
            | ParserMessageType.Error -> log.Error("{Error}", x)
            | ParserMessageType.Warning -> log.Warning("{Warning}", x)
            | _ -> ()
        if template.HasErrors then
            log.Error("Parsing {TemplateFileName} failed.", templateFileName)
            Error()
        else
            Ok template

    let private getTemplate (log: ILogger) =
        function
        | GrammarHtml _ | LALRConflictReport _ ->
            let templateText = ResourceLoader.load "Html.Root.scriban"
            let templateName = "HTML root template"
            parseTemplate log templateText templateName
        | GrammarSkeleton(_, LanguageNames(langName, templateName), _) ->
            let resourceKey = sprintf "GrammarSkeleton.%s.scriban" langName
            let templateText = ResourceLoader.load resourceKey
            parseTemplate log templateText templateName
        | GrammarCustomTemplate(_, path, _) -> either {
            let! path = assertFileExistsEx log path
            let templateText = File.ReadAllText path
            return! parseTemplate log templateText path
            }

    let private createTemplateContext templateType =
        let tc = TemplateContext()
        tc.StrictVariables <- true

        let so = Utilities.createDefaultScriptObject()
        match templateType with
        | GrammarHtml(g, options) ->
            Utilities.loadGrammar g so
            Utilities.loadHtml options tc so
        | GrammarSkeleton(g, _, ns) ->
            Utilities.loadGrammar g so
            let ns =
                ns
                |> Option.defaultValue (Path.GetFileNameWithoutExtension g.GrammarPath)
            so.SetValue("namespace", ns, true)
        | GrammarCustomTemplate(g, _, options) ->
            Utilities.loadGrammar g so
            let properties = ScriptObject()
            for propKey, propValue in options.AdditionalProperties do
                so.SetValue(propKey, propValue, true)
            so.SetValue("properties", properties, true)
        | LALRConflictReport ->
            raise (System.NotImplementedException("Creating LALR conflict reports is not yet supported."))
        tc.PushGlobal so
        tc

    let renderTemplate log templateType = either {
        let! template = getTemplate log templateType
        let tc = createTemplateContext templateType

        log.Verbose("Rendering template")
        let output = template.Render(tc)
        let fileExtension =
            match tc.CurrentGlobal.TryGetValue "file_extension" with
            | true, x -> x.ToString()
            | false, _ -> ".out.txt"
        return {
            FileExtension = fileExtension
            Content = output
        }
    }
