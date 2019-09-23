// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.DesigntimeFarkleTests

open Expecto
open Farkle
open Farkle.Builder
open Farkle.Grammar
open System.Collections.Immutable

[<Tests>]
let tests = testList "Designtime Farkle tests" [
    test "A nonterminal with no productions gives an error" {
        let nt = nonterminal "Vacuous"
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        let expectedError = "Vacuous" |> Set.singleton |> BuildError.EmptyNonterminals |> Error
        Expect.equal result expectedError "A nonterminal with no productions does not give an error"
    }

    test "A nonterminal with duplicate productions gives an error" {
        let term = literal "a"

        let nt = "Superfluous" ||= [
            // Returning the same numbers would still fail, but
            // this demonstrates why it is an error with Farkle, but
            // just a warning with GOLD Parser. In Farkle each production
            // has a fuser associated with it. While GOLD Parser would just merge
            // the duplicate productions and raise a warning, we can't do the same
            // because we can't choose between the fusers.
            !% term =% 1
            !% term =% 2
        ]
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        let expectedError =
            (Nonterminal(0u, "Superfluous"), ImmutableArray.Empty.Add(LALRSymbol.Terminal <| Terminal(0u, "a")))
            |> Set.singleton
            |> BuildError.DuplicateProductions
            |> Error
        Expect.equal result expectedError "A nonterminal with duplicate productions does not give an error"
    }

    test "Duplicate literals do not give an error" {
        let nt = "Colliding" ||= [
            !% (literal "a") =% 1
            !% (literal "a") .>> literal "b" =% 2
        ]
        let result = nt |> DesigntimeFarkleBuild.build |> fst
        Expect.isOk result "Duplicate literals give an error"
    }

    test "A grammar that only accepts the empty string indeed accepts it" {
        let designtime = "S" ||= [empty =% ()]
        let runtime = RuntimeFarkle.build designtime
        let result = RuntimeFarkle.parse runtime ""

        Expect.isOk result "Something went wrong"
    }

    test "A grammar with a nullable terminal is not accepted" {
        let designtime =
            let term = terminal "Nullable" (T(fun _ _ -> ())) (Regex.oneOf Number |> Regex.atLeast 0)
            "S" ||= [!% term =% ()]
        let grammar = DesigntimeFarkleBuild.build designtime |> fst
        Expect.equal grammar (Error (BuildError.NullableSymbols (Set.singleton (Choice1Of4 <| Terminal(0u, "Nullable")))))
            "A grammar with a nullable symbol was accepted"
    }
]
