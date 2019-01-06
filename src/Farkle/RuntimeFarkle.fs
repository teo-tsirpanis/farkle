// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Collections
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open Farkle.Parser
open Farkle.PostProcessor
open System
open System.IO
open System.Text

/// A type signifying an error during the parsing process.
type FarkleError =
    /// There was a parsing error.
    | ParseError of Message<ParseErrorType>
    /// There was an error while reading the grammar.
    | EGTReadError of EGTReadError
    /// There was an error while the post-processor _was being constructed_.
    | PostProcessorError
    override x.ToString() =
        match x with
        | ParseError x -> sprintf "Parsing error: %O" x
        | EGTReadError x -> sprintf "Error while reading the grammar file: %O" x
        | PostProcessorError -> """Error while creating the post-processor.
Some fusers might be missing, or there were type mismatches in the functions of the fusers or the transformers.
Check the post-processor's configuration."""

/// A reusable parser and post-processor, created for a specific grammar, and returning
/// a specific type of object that best describes an expression of the language of this grammar.
/// This is the highest-level API, and the easiest-to-use one.
[<NoComparison; NoEquality>]
type RuntimeFarkle<'TResult> = private {
    Grammar: Result<Grammar,FarkleError>
    PostProcessor: PostProcessor<'TResult>
}

/// Functions to create and use `RuntimeFarkle`s.
module RuntimeFarkle =

    let private createMaybe postProcessor grammar =
        {
            Grammar = grammar
            PostProcessor = postProcessor
        }

    /// Changes the post-processor of a `RuntimeFarkle`.
    [<CompiledName("ChangePostProcessor")>]
    let changePostProcessor pp rf = {Grammar = rf.Grammar; PostProcessor = pp}

    /// Creates a `RuntimeFarkle`.
    [<CompiledName("Create")>]
    let create postProcessor grammar = createMaybe postProcessor (Ok grammar)

    /// Creates a `RuntimeFarkle` from the given transformers and fusers, and the .egt file at the given path.
    /// In case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("CreateFromEGTFile")>]
    let ofEGTFile postProcessor fileName =
        fileName
        |> EGT.ofFile
        |> Result.mapError EGTReadError
        |> createMaybe postProcessor

    /// Creates a `RuntimeFarkle` from the given transformers and fusers, and the given Base-64 representation of an .egt file.
    /// In case the grammar file fails to be read, the `RuntimeFarkle` will fail every time it is used.
    [<CompiledName("CreateFromBase64String")>]
    let ofBase64String postProcessor x =
        x
        |> EGT.ofBase64String
        |> Result.mapError EGTReadError
        |> createMaybe postProcessor

    /// Parses and post-processes a `CharStream` of characters.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseChars")>]
    let parseChars (rf: RuntimeFarkle<'TResult>) fMessage input =
        let fParse grammar =
            let fLALR = LALRParser.LALRStep fMessage grammar rf.PostProcessor
            let fToken pos token =
                fMessage <| Message(pos, ParseMessageType.TokenRead token)
                fLALR pos token
            Tokenizer.tokenize Error fToken [] grammar rf.PostProcessor input
        rf.Grammar
        >>= (fParse >> Result.mapError ParseError)
        |> Result.map (fun x -> x :?> 'TResult)

    [<CompiledName("ParseMemory")>]
    /// Parses and post-processes a `ReadOnlyMemory` of characters.
    /// This function also accepts a custom parse message handler.
    let parseMemory rf fMessage input = input |> CharStream.ofReadOnlyMemory |> parseChars rf fMessage

    /// Parses and post-processes a string.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseString")>]
    let parseString rf fMessage (inputString: string) = inputString.AsMemory() |> parseMemory rf fMessage

    /// Parses and post-processes a .NET `Stream` with the given character encoding, which may be lazily loaded.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseStream")>]
    let parseStream rf fMessage doLazyLoad (encoding: Encoding) (inputStream: Stream) =
        use sr = new StreamReader(inputStream, encoding)
        match doLazyLoad with
        | true -> CharStream.ofTextReader sr
        | false -> sr.ReadToEnd() |> CharStream.ofString
        |> flip using (parseChars rf fMessage)

    /// Parses and post-processes a file at the given path with the given character encoding.
    /// This function also accepts a custom parse message handler.
    [<CompiledName("ParseFile")>]
    let parseFile rf fMessage encoding inputFile =
        use s = File.OpenRead(inputFile)
        parseStream rf fMessage true encoding s

    /// Parses and post-processes a string.
    // This function was inspired by FParsec, which has some "runParserOn***" functions,
    // and the simple and easy-to-use function named "run", that just parses a string.
    [<CompiledName("Parse")>]
    let parse rf x = parseString rf ignore x
