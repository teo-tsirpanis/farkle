// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.RegexTests

open Expecto
open Farkle.Builder
open Farkle.Collections
open Farkle.Grammar
open Farkle.Tests
open FsCheck
open System.Collections.Immutable

/// A very simple function to check if a string is recognized by a DFA.
/// We don't need a full-blown tokenizer here.
let matchDFAToString (states: ImmutableArray<DFAState>) str =
    let rec impl currState idx =
        if idx = String.length str then
            currState.AcceptSymbol
        else
            match RangeMap.tryFind str.[idx] currState.Edges with
            | ValueSome s -> impl states.[int s] (idx + 1)
            | ValueNone -> None
    impl states.[0] 0

let terminal idx = Choice1Of4 <| Terminal(uint32 idx, string idx)

/// Performs a property test with a smaller sample size.
let testPropertySmall name prop = testPropertyWithConfigs {fsCheckConfig with endSize = 50} fsCheckConfig name prop

[<Tests>]
let tests = testList "Regex tests" [
    testProperty "Regex.optional is idempotent" (fun regex ->
        let opt1 = Regex.optional regex
        let opt2 = Regex.optional opt1
        opt1 = opt2
    )

    testProperty "Regex.ZeroOrMore is idempotent" (fun regex ->
        let star1 = Regex.atLeast 0 regex
        let star2 = Regex.atLeast 0 star1
        star1 = star2)

    testProperty "Chaining Regex.And works the same with Regex.Concat" (fun regexes ->
        let chained = Array.fold (<&>) Regex.Empty regexes
        let concatenated = Regex.Join regexes
        chained = concatenated
    )

    // The default size makes tests run for too long
    testPropertySmall "A DFA generated by regular expressions recognizes all of them" (fun (Regexes (regexes, strings)) ->
        let dfa = DFABuild.buildRegexesToDFA false true regexes
        match dfa with
        | Ok dfa ->
            List.forall (fun (str, sym) -> match matchDFAToString dfa str with | Some x -> x = sym | _ -> false) strings
        // Some regexes might turn out to be indistinguishable.
        // It's quite unlikely, but we cannot predict it, so we just ignore it.
        | Error _ -> true
    )

    testPropertySmall "A case insensitive DFA recognizes strings regardless of their capitalization" (fun (RegexStringPair (regex, str)) ->
        let dfa =
            DFABuild.buildRegexesToDFA false false [regex, Choice1Of4 <| Terminal(0u, "My Terminal")]
            // We have only one regex. It cannot fail by accident.
            |> returnOrFail "Generating a case insensitive DFA failed"
        Expect.isSome (matchDFAToString dfa str) "Recognizing the original string failed"
        Expect.isSome (matchDFAToString dfa <| str.ToLowerInvariant()) "Recognizing the lowercase string failed"
        Expect.isSome (matchDFAToString dfa <| str.ToUpperInvariant()) "Recognizing the uppercase string failed"
    )
    
    testProperty "A DFA can correctly recognize literal symbols over a wildcard" (fun literals ->
        let literals = literals |> List.distinct |> List.map (fun (NonEmptyString str) -> str)
        let ident = (Regex.chars [char 0 .. char 127] |> Regex.atLeast 1, Choice1Of4 <| Terminal(0u, "Identifier"))
        let dfa =
            literals
            |> List.mapi (fun i str -> Regex.string str, Choice1Of4 <| Terminal(uint32 i + 1u, str))
            |> (fun xs -> ident :: xs)
            |> DFABuild.buildRegexesToDFA true true
            |> returnOrFail "Generating the DFA failed"
        literals
        |> List.forall (fun str ->
            match matchDFAToString dfa str with
            | Some(Choice1Of4(Terminal(_, term))) -> term = str
            | _ -> false)
    )

    testProperty "A DFA for a literal string is minimal" (fun (NonNull str) ->
        let dfa =
            [Regex.string str, Choice1Of4 <| Terminal(0u, str)]
            |> DFABuild.buildRegexesToDFA false true
            |> returnOrFail "Generating a DFA for a literal string failed"
        Expect.hasLength dfa (str.Length + 1) "The DFA is not minimal")
]
