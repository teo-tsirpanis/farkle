// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Chiron
open Farkle
open Farkle.Common
open Farkle.IO
open Farkle.JSON
open Farkle.PostProcessor
open FParsec
open System.IO
open System.Text

type JsonBenchmark() =

    let jsonFile = "generated.json"

    let syntaxChecker = RuntimeFarkle.changePostProcessor PostProcessor.syntaxCheck FSharp.Language.runtime

    [<Benchmark>]
    member __.FarkleCSharp() =
        RuntimeFarkle.parseFile CSharp.Language.Runtime ignore jsonFile
        |> returnOrFail

    [<Benchmark(Baseline = true)>]
    member __.FarkleFSharp() =
        RuntimeFarkle.parseFile FSharp.Language.runtime ignore jsonFile
        |> returnOrFail

    [<Benchmark>]  
    member __.FarkleSyntaxCheck() =
        RuntimeFarkle.parseFile syntaxChecker ignore jsonFile
        |> returnOrFail

    [<Benchmark>]
    // Chiron uses FParsec underneath, which is the main competitor of Farkle.
    // I could use the Big Data edition, but it is not branded as their main
    // edition, and I am not going to do them any favors by allowing unsafe code.
    member __.Chiron() =
        let parseResult = runParserOnFile !jsonR () jsonFile Encoding.UTF8
        match parseResult with
        | Success (json, _, _) -> json
        | Failure _ -> failwithf "Error while parsing '%s'" jsonFile
