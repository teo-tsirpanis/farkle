// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EgtReader

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Monads

module internal HighLevel =

    open StateResult
    open MidLevel

    type IndexedGetter<'a> = Indexed<'a> -> StateResult<'a, Entry list, EGTReadError>

    let liftFlatten x = x <!> liftResult |> flatten

    let getIndexedfromList x = Indexed.getfromList x >> liftResult >> mapFailure IndexNotFound

    let readProperty = sresult {
        do! wantUInt16 |> ignore /// We do not store based on index
        let! name = wantString
        let! value = wantString
        return (name, value)
    }

    let readTableCounts = sresult {
        let! symbols = wantUInt16
        let! charSets = wantUInt16
        let! productions = wantUInt16
        let! dfas = wantUInt16
        let! lalrs = wantUInt16
        let! groups = wantUInt16
        return
            {
                SymbolTables = symbols
                CharSetTables = charSets
                ProductionTables = productions
                DFATables = dfas
                LALRTables = lalrs
                GroupTables = groups
            }
    }

    let readCharSet = sresult {
        let! index = wantUInt16
        do! wantUInt16 |> ignore // Unicode plane is ignored; what exactly does it do?
        do! wantUInt16 |> ignore // Range count is ignored; we just read until the end.
        do! wantEmpty // Reserved field.
        let readCharSet = sresult {
            let! start = wantUInt16 <!> char
            let! finish = wantUInt16 <!> char
            return RangeSet.create start finish
        }
        return!
            whileM (List.hasItems() |> liftState) readCharSet
            <!> RangeSet.concat
            <!> Indexable.create index
    }

    let readSymbol = sresult {
        let! index = wantUInt16
        let! name = wantString
        let! stype =
            wantUInt16
            <!> SymbolType.ofUInt16
            |> liftFlatten
        return
            {
                Name = name
                SymbolType = stype
            }
            |> Indexable.create index
    }

    let readGroup fSymbols = sresult {
        let! index = wantUInt16
        let! name = wantString
        let symbolFunc = wantUInt16 <!> Indexed <!> fSymbols |> flatten
        let! contIndex = symbolFunc
        let! startIndex = symbolFunc
        let! endIndex = symbolFunc
        let! advMode = wantUInt16 <!> AdvanceMode.create |> liftFlatten
        let! endingMode = wantUInt16 <!> EndingMode.create |> liftFlatten
        do! wantEmpty // Reserved field.
        do! wantUInt16 |> ignore // Nesting count is ignored; we just read until the end.
        let nestingFunc = wantUInt16 <!> Indexed
        let! nesting = whileM (List.hasItems() |> liftState) nestingFunc <!> set
        return
            {
                Name = name
                ContainerSymbol = contIndex
                StartSymbol = startIndex
                EndSymbol = endIndex
                AdvanceMode = advMode
                EndingMode = endingMode
                Nesting = nesting
            }
            |> Indexable.create index
    }

    let readProduction (fSymbols: IndexedGetter<_>) = sresult {
        let! index = wantUInt16
        let symbolFunc = wantUInt16 <!> Indexed <!> fSymbols |> flatten
        let! nonTerminal = symbolFunc
        do! wantEmpty // Reserved field.
        let! symbols = whileM (List.hasItems() |> liftState) symbolFunc <!> List.ofSeq
        return
            {
                Nonterminal = nonTerminal
                Symbols = symbols
            }
            |> Indexable.create index
    }

    let readInitialStates (fDFA: IndexedGetter<_>) (fLALR: IndexedGetter<_>) = sresult {
        let! dfa = wantUInt16 <!> Indexed <!> fDFA |> flatten
        let! lalr = wantUInt16 <!> Indexed <!> fLALR |> flatten
        return
            {
                DFA = dfa
                LALR = lalr
            }
    }

    let readDFAState (fSymbols: IndexedGetter<_>) (fCharSets: IndexedGetter<_>) = sresult {
        let! index = wantUInt16
        let! isAccept = wantBoolean
        let! acceptIndex = wantUInt16 <!> Indexed
        let! acceptState =
            match isAccept with
            | true -> acceptIndex |> fSymbols <!> Some
            | false -> returnM None
        do! wantEmpty // Reserved field.
        let readEdges = sresult {
            let! charSet = wantUInt16 <!> Indexed <!> fCharSets |> flatten
            let! target = wantUInt16 <!> Indexed
            do! wantEmpty // Reserved field.
            return charSet, target
        }
        let! edges = whileM (List.hasItems() |> liftState) readEdges <!> set
        return
            {
                Index = index
                AcceptSymbol = acceptState
                Edges = edges
            }
            |> Indexable.create index
    }

    let readLALRState (fSymbols: IndexedGetter<_>) fProds = sresult {
        let! index = wantUInt16
        do! wantEmpty // Reserved field.
        let readActions = sresult {
            let! symbolIndex = wantUInt16 <!> Indexed <!> fSymbols |> flatten
            let! actionId = wantUInt16
            let! targetIndex = wantUInt16
            do! wantEmpty // Reserved field.
            let! action = LALRAction.create fProds targetIndex actionId |> liftResult
            return (symbolIndex, action)
        }
        let! states = whileM (List.hasItems() |> liftState) readActions <!> Map.ofSeq
        return {States = states; Index = index} |> Indexable.create index
    }

    let mapMatching magicChar f = sresult {
        let! x = wantByte
        if x = magicChar then
            return! f <!> Some
        else
            return None
    }

    let makeGrammar (EGTFile records) = trial {
        let mapMatching mc f = records |> List.map (fun (Record x) -> eval (mapMatching mc f) x) |> collect |> lift (List.choose id)
        let exactlyOne x = x |> List.exactlyOne |> Trial.mapFailure ListError
        let! properties = readProperty |> mapMatching 'p'B |> lift (Map.ofList >> Properties)
        let! tableCounts = readTableCounts |> mapMatching 't'B |> lift exactlyOne |> Trial.flatten
        let! charSets = readCharSet |> mapMatching 'c'B |> lift Indexable.collect
        let fCharSets = getIndexedfromList charSets
        let! symbols = readSymbol |> mapMatching 'S'B |> lift Indexable.collect
        let fSymbols = getIndexedfromList symbols
        let! groups = readGroup fSymbols |> mapMatching 'g'B |> lift Indexable.collect
        let! prods = readProduction fSymbols |> mapMatching 'R'B |> lift Indexable.collect
        let fProds x = eval (getIndexedfromList prods x) ()
        let! dfas = readDFAState fSymbols fCharSets |> mapMatching 'D'B |> lift Indexable.collect
        let fDFA = getIndexedfromList dfas
        let! lalrs = readLALRState fSymbols fProds |> mapMatching 'L'B |> lift Indexable.collect
        let fLALR = getIndexedfromList lalrs
        let! initialStates = readInitialStates fDFA fLALR |> mapMatching 'I'B |> lift exactlyOne |> Trial.flatten
        return! Grammar.create properties symbols charSets prods initialStates dfas lalrs groups tableCounts
    }
    // From 20/7/2017 until 24/7/2017, this file had _exactly_ 198 lines of code.
    // This Number should not be changed, unless it was absolutely neccessary.
    // Was that a coincidence? Highly unlikely.
    // Just look! Not even the dates were a coincidence! 🔺👁
