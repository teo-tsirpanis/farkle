// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.GrammarEquivalenceTests

open Expecto
open Farkle.Grammar
open System.Collections.Generic

// A major difference between Farkle and GOLD Parser is the way they number stuff.
// GOLD Parser numbers terminals based on the order they appear in the GML file.
// And LALR item sets are being created as they are traversed on a breadth-first search fashion.
// Farkle however takes a different approach. Because it uses regular objects to represent its grammars,
// it simply performs a depth-first search to discover symbols and item sets. That's the reason why
// the grammars generated by Farkle have different table indices from those generated by GOLD.
// And that's the reason we have to write this little function to check if the LALR states
// of a grammar are equivalent, up to the names of the states.
let checkLALRStateEquivalence (farkleGrammar: Grammar) (goldGrammar: Grammar) =
    let lalrStates = Dictionary()
    let checkProductionEquivalence pFarkle pGold =
        Expect.equal pFarkle.Head.Name pGold.Head.Name "The productions have a different head"
        Expect.sequenceEqual (Seq.map string pFarkle.Handle) (Seq.map string pGold.Handle) "The productions have a different handle"
    let checkActionEquivalence sFarkle sGold =
        match sFarkle, sGold with
        | LALRAction.Shift sFarkle, LALRAction.Shift sGold ->
            match lalrStates.TryGetValue(sFarkle) with
            | true, sGoldExpected ->
                Expect.equal sGold sGoldExpected "There is a LALR state mismatch"
            | false, _ -> lalrStates.Add(sFarkle, sGold)
        | LALRAction.Reduce pFarkle, LALRAction.Reduce pGold ->
            checkProductionEquivalence pFarkle pGold
        | LALRAction.Accept, LALRAction.Accept -> ()
        | _, _ -> failtestf "Farkle's LALR action %O is not equivalent with GOLD Parser's %O" sFarkle sGold
    let checkGotoEquivalence nont gFarkle gGold =
        match lalrStates.TryGetValue(gFarkle) with
        | true, gGoldExpected -> Expect.equal gGold gGoldExpected (sprintf "The GOTO actions for nonterminal %O are different" nont)
        | false, _ -> lalrStates.Add(gFarkle, gGold)
    lalrStates.Add(0u, 0u)
    Expect.hasLength farkleGrammar.LALRStates goldGrammar.LALRStates.Length "The grammars have a different number of LALR states"
    for i = 0 to farkleGrammar.LALRStates.Length - 1 do
        try
            let farkleState = farkleGrammar.LALRStates.[uint32 i]
            let goldState = goldGrammar.LALRStates.[lalrStates.[uint32 i]]
            Expect.hasLength farkleState.Actions goldState.Actions.Count "There are not the same number of LALR actions"
            let actionsJoined = query {
                for aFarkle in farkleState.Actions do
                join aGold in goldState.Actions on (aFarkle.Key.Name = aGold.Key.Name)
                select (aFarkle.Value, aGold.Value)
            }
            Expect.hasLength actionsJoined goldState.Actions.Count "Some terminals do not have a matching LALR action"
            actionsJoined |> Seq.iter (fun (aFrakle, aGoto) -> checkActionEquivalence aFrakle aGoto)

            Expect.hasLength farkleState.GotoActions goldState.GotoActions.Count "There are not the same number of LALR GOTO actions"
            let gotoJoined = query {
                for gFarkle in farkleState.GotoActions do
                join gGold in goldState.GotoActions on (gFarkle.Key.Name = gGold.Key.Name)
                select (gFarkle.Key, gFarkle.Value, gGold.Value)
            }
            Expect.hasLength gotoJoined goldState.GotoActions.Count "Some nonterminals have no matching LALR GOTO action"
            gotoJoined |> Seq.iter (fun (nont, gFarkle, gGold) -> checkGotoEquivalence nont gFarkle gGold)
        with
        | exn -> failtestf "Error in state %d: %s" i exn.Message

[<Tests>]
let tests =
    ["the calculator", extractGrammar SimpleMaths.int, extractGrammar SimpleMaths.mathExpression]
    |> List.map (fun (name, gFarkle, gGold) ->
        test (sprintf "Farkle and GOLD Parser generate an equivalent LALR parser for %s" name) {
            checkLALRStateEquivalence gFarkle gGold
        }
    )
    |> testList "Grammar equivalence tests"