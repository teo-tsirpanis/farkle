// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammar
open Farkle.Tools.Common
open Scriban.Runtime

type FarkleObject = {
    Version: string
}
with
    static member Create = {Version = toolsVersion}

type FarkleRoot = {
    Farkle: FarkleObject
    Grammar: Grammar
    GrammarPath: string
    Namespace: string
}
with
    [<ScriptMemberIgnore>]
    static member Create grammar grammarPath ns = {
        Farkle = FarkleObject.Create
        Grammar = grammar
        GrammarPath = grammarPath
        Namespace = ns
    }

type GeneratedTemplate = {
    FileExtension: string
    Content: string
}
