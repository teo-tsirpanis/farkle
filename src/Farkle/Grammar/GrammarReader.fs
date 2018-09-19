// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.GOLDParser

open Farkle
open Farkle.Collections
open Farkle.Grammar
open Farkle.Grammar.GOLDParser
open Farkle.Monads.Maybe

module internal GrammarReader =

    module private Implementation =

        // This is a reminiscent of an older era when I used to use a custom monad to parse a simple binary file.
        // It should remind us to keep things simple. Hold "F" to pay your respect but remember not to commit anything in the repository.
        // FFFFFFfFFFFFFF
        let inline wantUInt16 x = match x with | UInt16 x -> Some x | _ -> None

        let inline readInChunks allowEmpty count fReadIt =
            function
            | RMNil when allowEmpty -> Some []
            | x when x.Length % count <> 0 -> None
            | x -> List.init (x.Length / count) (fun i -> x.Slice(i * count, count) |> fReadIt) |> List.allSome

        let readProperty =
            function
            | RMCons(UInt16 _index, RMCons(String name, RMCons(String value, RMNil))) -> (name, value) |> Some
            | _ -> None

        let readTableCounts =
            function
            | RMCons(UInt16 symbols, RMCons(UInt16 sets, RMCons(UInt16 rules, RMCons(UInt16 dfas, RMCons(UInt16 lalrs, RMCons(UInt16 groups, RMNil)))))) ->
                Some
                    {
                        SymbolTables = symbols
                        CharSetTables = sets
                        ProductionTables = rules
                        DFATables = dfas
                        LALRTables = lalrs
                        GroupTables = groups
                    }
            | _ -> None

        let readCharSet _ =
            let readRanges =
                function
                | RMCons(UInt16 start, RMCons(UInt16 theEnd, RMNil)) ->
                    RangeSet.create (char start) (char theEnd)
                    |> Some
                | _ -> None
                |> readInChunks false 2
                >> Option.map RangeSet.concat
            function
            | RMCons(UInt16 _unicodePlane, RMCons(UInt16 _rangeCount, RMCons(Empty, ranges))) ->
                ranges
                |> readRanges
            | _ -> None

        let defaultGroupIndex = Indexed.create System.UInt32.MaxValue // This is impossible to occur in a grammar file; it only goes up to 65536.

        let readSymbol index =
            function
            | RMCons(String name, RMCons(UInt16 0us, RMNil)) -> Nonterminal (uint32 index, name) |> Some
            | RMCons(String name, RMCons(UInt16 1us, RMNil)) -> Terminal (uint32 index, name) |> Some
            | RMCons(String name, RMCons(UInt16 2us, RMNil)) -> Noise name |> Some
            | RMCons(String _ , RMCons(UInt16 3us, RMNil)) -> EndOfFile |> Some
            | RMCons(String name, RMCons(UInt16 4us, RMNil)) -> GroupStart (defaultGroupIndex, name) |> Some
            | RMCons(String name, RMCons(UInt16 5us, RMNil)) -> GroupEnd name |> Some
            | RMCons(String _, RMCons(UInt16 7us, RMNil)) -> Unrecognized |> Some
            | _ -> None

        let readGroup fSymbol fGroup index =
            let readNestedGroups =
                (function | RMCons (UInt16 x, RMNil) -> fGroup x | _ -> None)
                |> readInChunks true 1
                >> Option.map set
            function
            | RMCons(String name, RMCons(UInt16 containerIndex, RMCons(UInt16 startIndex, RMCons(UInt16 endIndex, RMCons(UInt16 advanceMode, RMCons(UInt16 endingMode, RMCons(Empty, RMCons(UInt16 _nestingCount, xs)))))))) -> maybe {
                let! containerSymbol = fSymbol containerIndex
                let! startSymbol = fSymbol startIndex
                let! endSymbol = fSymbol endIndex
                let! advanceMode = match advanceMode with | 0us -> Some Token | 1us -> Some Character | _ -> None
                let! endingMode = match endingMode with | 0us -> Some Open | 1us -> Some Closed | _ -> None
                let! nesting = readNestedGroups xs
                return {
                    Name = name
                    Index = index
                    ContainerSymbol = containerSymbol
                    StartSymbol = startSymbol
                    EndSymbol = endSymbol
                    AdvanceMode = advanceMode
                    EndingMode = endingMode
                    Nesting = nesting
                }
                }
            | _ -> None

        let readProduction fSymbol index =
            let readChildrenSymbols =
                (function | RMCons (UInt16 x, RMNil) -> fSymbol x | _ -> None)
                |> readInChunks true 1
            function
            | RMCons(UInt16 headIndex, RMCons(Empty, xs)) -> maybe {
                let! headSymbol = fSymbol headIndex
                let! symbols = readChildrenSymbols xs
                return {Index = index; Head = headSymbol; Handle = symbols}
                }
            | _ -> None

        let readInitialStates fDFA fLALR =
            function
            | RMCons(UInt16 dfa, RMCons(UInt16 lalr, RMNil)) -> maybe {
                let! dfa = fDFA dfa
                let! lalr = fLALR lalr
                return dfa, lalr
                }
            | _ -> None

        let readDFAState fCharSet fSymbol fDFA index =
            let readDFAEdges =
                function
                | RMCons(UInt16 charSetIndex, RMCons(UInt16 targetIndex, RMCons(Empty, RMNil))) -> maybe {
                    let! charSet = fCharSet charSetIndex
                    let! target = fDFA targetIndex
                    return (charSet, target)
                    }
                | _ -> None
                |> readInChunks false 3
            function
            | RMCons(Boolean false, RMCons(UInt16 _, RMCons(Empty, xs))) ->
                xs
                |> readDFAEdges
                |> Option.map (fun edges -> (index, edges) |> DFAContinue)
            | RMCons(Boolean true, RMCons(UInt16 acceptIndex, RMCons(Empty, xs))) -> maybe {
                let! edges = readDFAEdges xs
                let! acceptSymbol = fSymbol acceptIndex
                return DFAAccept (index, (acceptSymbol, edges))
                }
            | _ -> None

        let readLALRState fSymbol fProduction fLALR index =
            let readLALRAction =
                function
                | RMCons(UInt16 symbolIndex, xs) -> maybe {
                    let! symbol = fSymbol symbolIndex
                    match xs with
                    | RMCons(UInt16 1us, RMCons(UInt16 targetStateIndex, RMCons(Empty, RMNil))) ->
                        let! targetState = fLALR targetStateIndex
                        return symbol, Shift targetState
                    | RMCons(UInt16 2us, RMCons(UInt16 targetProductionIndex, RMCons(Empty, RMNil))) ->
                        let! targetProduction = fProduction targetProductionIndex
                        return symbol, Reduce targetProduction
                    | RMCons(UInt16 3us, RMCons(UInt16 targetStateIndex, RMCons(Empty, RMNil))) ->
                        let! targetState = fLALR targetStateIndex
                        return symbol, Goto targetState
                    | RMCons(UInt16 4us, RMCons(UInt16 _, RMCons(Empty, RMNil))) -> return symbol, Accept
                    | _ -> return! None
                    }
                | _ -> None
                |> readInChunks false 4
                >> Option.map Map.ofSeq
            function
            | RMCons(Empty, xs) -> readLALRAction xs |> Option.map (fun actions -> {Index = index; Actions = actions})
            | _ -> None

        [<Literal>]
        let CGTHeader = "GOLD Parser Tables/v1.0"
        [<Literal>]
        let EGTHeader = "GOLD Parser Tables/v5.0"

        let inline zc x = x |> int |> Array.zeroCreate

        let inline itemTry arr idx = idx |> int |> flip Array.tryItem arr

        let readAndAssignIndexed fRead arr entries =
            match entries with
            | RMCons(UInt16 index, xs) when int index < Array.length arr ->
                xs |> fRead (uint32 index) |> Option.map (Array.set arr (int index))
            | _ -> None

        let inline changeOnce x newValue =
            match !x with
            | None ->
                x := Some newValue
                Some ()
            | Some _ -> None

        let fixGroupStartSymbols symbols groups =
            let mutable i = 0
            let mutable doContinue = true
            while i < Array.length symbols && doContinue do
                let sym = &symbols.[i]
                match sym with
                | GroupStart (_, name) as s ->
                    match Array.tryFindIndex (fun {StartSymbol = ss} -> ss = s) groups with
                    | Some idx -> sym <- GroupStart (Indexed.create <| uint32 idx, name)
                    | None -> doContinue <- false
                | _ -> do()
                i <- i + 1
            if doContinue then
                Ok ()
            else
                Error UnknownEGTFile

    open Implementation

    let read r =
        let properties = System.Collections.Generic.Dictionary()
        let mutable isTableCountsInitialized = false
        let mutable charSets = [| |]
        let mutable symbols = [| |]
        let mutable groups = [| |]
        let mutable productions = [| |]
        let mutable dfaStates = [| |]
        let mutable lalrStates = [| |]
        let initialStates = ref None
        let fHeaderCheck =
            function
            | CGTHeader -> Error ReadACGTFile
            | EGTHeader -> Ok ()
            | _ -> Error UnknownEGTFile
        let initTables (x: TableCounts) =
            charSets <- zc x.CharSetTables
            symbols <- zc x.SymbolTables
            groups <- zc x.GroupTables
            productions <- zc x.ProductionTables
            dfaStates <- zc x.DFATables
            lalrStates <- zc x.LALRTables
        let fRecord =
            function
            | RMCons(Byte 'p'B, xs) -> readProperty xs |> Option.map (properties.Add)
            // The table counts record must exist only once, and before the other records.
            | RMCons(Byte 't'B, xs) when not isTableCountsInitialized ->
                isTableCountsInitialized <- true
                readTableCounts xs |> Option.map initTables
            | RMCons(Byte 'c'B, xs) when isTableCountsInitialized ->
                readAndAssignIndexed readCharSet charSets xs
            | RMCons(Byte 'S'B, xs) when isTableCountsInitialized ->
                readAndAssignIndexed readSymbol symbols xs
            | RMCons(Byte 'g'B, xs) when isTableCountsInitialized ->
                readAndAssignIndexed (readGroup (itemTry symbols) (Indexed.createWithKnownLength groups)) groups xs
            | RMCons(Byte 'R'B, xs) when isTableCountsInitialized ->
                readAndAssignIndexed (readProduction (itemTry symbols)) productions xs
            | RMCons(Byte 'I'B, xs) when isTableCountsInitialized ->
                readInitialStates (Indexed.createWithKnownLength dfaStates) (Indexed.createWithKnownLength lalrStates) xs |> Option.bind (changeOnce initialStates)
            | RMCons(Byte 'D'B, xs) when isTableCountsInitialized ->
                readAndAssignIndexed (readDFAState (itemTry charSets) (itemTry symbols) (Indexed.createWithKnownLength dfaStates)) dfaStates xs
            | RMCons(Byte 'L'B, xs) when isTableCountsInitialized ->
                readAndAssignIndexed (readLALRState (itemTry symbols) (itemTry productions) (Indexed.createWithKnownLength lalrStates)) lalrStates xs
            | _ -> None
            >> failIfNone UnknownEGTFile
        either {
            do! EGTReader.readEGT fHeaderCheck fRecord r
            do! fixGroupStartSymbols symbols groups
            let! (initialDFA, initialLALR) = !initialStates |> failIfNone UnknownEGTFile
            let dfaStates = SafeArray.ofArrayUnsafe dfaStates
            let lalrStates = SafeArray.ofArrayUnsafe lalrStates
            return GOLDGrammar.create
                (properties |> Seq.map (fun p -> p.Key, p.Value) |> Map.ofSeq)
                (SafeArray.ofArrayUnsafe symbols)
                (SafeArray.ofArrayUnsafe charSets)
                (SafeArray.ofArrayUnsafe productions)
                {InitialState = dfaStates.Item initialDFA; States = dfaStates}
                {InitialState = lalrStates.Item initialLALR; States = lalrStates}
                (SafeArray.ofArrayUnsafe groups)
        }
