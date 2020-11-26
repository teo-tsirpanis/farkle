// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

/// Functions to write a grammar to EGTneo files.
module internal Farkle.Grammar.EGTFile.EGTNeoWriter

open Farkle.Grammar
open Farkle.Grammar.EGTFile
open Farkle.Grammar.EGTFile.EGTHeaders
open System.Collections.Immutable

[<AutoOpen>]
module private Implementation =

    type IndexMap = ImmutableDictionary<uint32, uint32>

    let writeProperties (w: EGTWriter) (props: ImmutableDictionary<_,_>) =

        w.WriteString propertiesHeader
        for p in props do
            w.WriteString p.Key
            w.WriteString p.Value

        w.FinishPendingRecord()

    let inline writeLALRSymbols (w: EGTWriter) header (symbols: ImmutableArray<_>) =
        let dict = ImmutableDictionary.CreateBuilder()

        w.WriteString header
        for i = 0 to symbols.Length - 1 do
            let sym = symbols.[i]
            dict.[(^Symbol: (member Index: uint32) sym)] <- uint32 i
            w.WriteString (^Symbol: (member Name: string) sym)

        w.FinishPendingRecord()
        dict.ToImmutable()

    let writeTerminals w (terms: ImmutableArray<Terminal>) =
        writeLALRSymbols w terminalsHeader terms

    let writeNonterminals w (terms: ImmutableArray<Nonterminal>) =
        writeLALRSymbols w nonterminalsHeader terms

    let writeNoiseSymbols (w: EGTWriter) (noises: ImmutableArray<_>) =
        w.WriteString noiseSymbolsHeader
        for i = 0 to noises.Length - 1 do
            let (Noise sym) = noises.[i]
            w.WriteString sym

        w.FinishPendingRecord()

    let inline writeSingleValued (w: EGTWriter) header entry =
        w.WriteString header
        w.WriteEntry entry
        w.FinishPendingRecord()

    let indexOf message (xs: ImmutableArray<_>) x =
        match xs.IndexOf x with
        | -1 -> failwithf "%s %O not found" message x
        | idx -> uint32 idx

    let writeStartSymbol w (nonterminalMap: IndexMap) (startSymbol: Nonterminal) =
        nonterminalMap.[startSymbol.Index]
        |> Entry.UInt32
        |> writeSingleValued w startSymbolHeader

    let writeGroups (w: EGTWriter) noiseSymbols (terminalMap: IndexMap) (groups: ImmutableArray<Group>) =
        w.WriteString groupsHeader
        w.WriteInt groups.Length

        for i = 0 to groups.Length - 1 do
            let group = groups.[i]

            w.WriteString group.Name
            w.WriteBoolean group.IsTerminal
            w.WriteUInt32
                (match group.ContainerSymbol with
                | Choice1Of2(Terminal(idx, _)) -> terminalMap.[idx]
                | Choice2Of2 x -> indexOf "Noise symbol" noiseSymbols x)
            w.WriteString <| match group.Start with GroupStart(name, _) -> name
            w.WriteEntry
                (match group.End with
                | Some(GroupEnd name) -> Entry.String name
                | None -> Entry.Empty)
            w.WriteByte
                (match group.AdvanceMode with
                | AdvanceMode.Token -> 'T'B
                | AdvanceMode.Character -> 'C'B)
            w.WriteByte
                (match group.EndingMode with
                | EndingMode.Open -> 'O'B
                | EndingMode.Closed -> 'C'B)

            w.WriteInt group.Nesting.Count
            group.Nesting
            |> Seq.sort
            |> Seq.iter w.WriteUInt32

        w.FinishPendingRecord()

    let writeProductions (w: EGTWriter) (terminalMap: IndexMap) (nonterminalMap: IndexMap)
        (productions: ImmutableArray<Production>) =
        w.WriteString productionsHeader
        w.WriteInt productions.Length

        for i = 0 to productions.Length - 1 do
            let prod = productions.[i]

            w.WriteUInt32 nonterminalMap.[prod.Head.Index]
            w.WriteInt prod.Handle.Length
            prod.Handle
            |> Seq.iter (
                function
                | LALRSymbol.Terminal term ->
                    w.WriteByte 'T'B
                    w.WriteUInt32 terminalMap.[term.Index]
                | LALRSymbol.Nonterminal nont ->
                    w.WriteByte 'N'B
                    w.WriteUInt32 nonterminalMap.[nont.Index])

        w.FinishPendingRecord()

    let writeLALRAction action (w: EGTWriter) =
        match action with
        | LALRAction.Shift idx ->
            w.WriteByte 'S'B
            w.WriteUInt32 idx
        | LALRAction.Reduce {Index = idx} ->
            w.WriteByte 'R'B
            w.WriteUInt32 idx
        | LALRAction.Accept ->
            w.WriteByte 'A'B
            w.WriteEmpty()

    let writeLALRStates (w: EGTWriter) (terminalMap: IndexMap) (nonterminalMap: IndexMap) (states: ImmutableArray<LALRState>) =

        w.WriteString lalrHeader
        w.WriteInt states.Length

        for i = 0 to states.Length - 1 do
            let s = states.[i]

            match s.EOFAction with
            | Some x -> writeLALRAction x w
            | None ->
                w.WriteEmpty()
                w.WriteEmpty()

            w.WriteInt s.Actions.Count
            s.Actions
            |> Seq.iter (fun (KeyValue(term, action)) ->
                w.WriteUInt32 terminalMap.[term.Index]
                writeLALRAction action w)

            w.WriteInt s.GotoActions.Count
            s.GotoActions
            |> Seq.iter (fun (KeyValue(nont, idx)) ->
                w.WriteUInt32 nonterminalMap.[nont.Index]
                w.WriteUInt32 idx)

        w.FinishPendingRecord()

    let writeDFAStates (w: EGTWriter) (terminalMap: IndexMap) noiseSymbols (groups: ImmutableArray<_>) (states: ImmutableArray<DFAState>) =
        let writeUInt32Maybe x =
            match x with
            | Some x -> Entry.UInt32 x
            | None -> Entry.Empty
            |> w.WriteEntry

        w.WriteString dfaHeader
        w.WriteInt states.Length

        for i = 0 to states.Length - 1 do
            let s = states.[i]

            match s.AcceptSymbol with
            | None ->
                w.WriteEmpty()
                w.WriteEmpty()
            | Some (Choice1Of4 term) ->
                w.WriteByte 'T'B
                w.WriteUInt32 terminalMap.[term.Index]
            | Some (Choice2Of4 noise) ->
                w.WriteByte 'N'B
                indexOf "Noise symbol" noiseSymbols noise |> w.WriteUInt32
            | Some (Choice3Of4 gs) ->
                w.WriteByte 'G'B
                groups
                |> Seq.findIndex (fun g -> g.Start = gs)
                |> w.WriteInt
            | Some (Choice4Of4 ge) ->
                w.WriteByte 'g'B
                let ge = Some ge
                groups
                |> Seq.findIndex (fun g -> g.End = ge)
                |> w.WriteInt

            writeUInt32Maybe s.AnythingElse

            let elements = s.Edges.Elements
            w.WriteInt elements.Length
            for x in elements do
                w.WriteInt x.KeyFrom
                w.WriteInt x.KeyTo
                writeUInt32Maybe x.Value

        w.FinishPendingRecord()

let write w (grammar: Grammar) =
    // For symmetry with the reader, the header
    // will be written at the EGT module.
    writeProperties w grammar.Properties
    // In GOLD Parser's EGT files, the symbols do
    // not start from zero; we have to adjust them.
    let terminalMap = writeTerminals w grammar.Symbols.Terminals
    let nonterminalMap = writeNonterminals w grammar.Symbols.Nonterminals
    writeNoiseSymbols w grammar.Symbols.NoiseSymbols
    writeStartSymbol w nonterminalMap grammar.StartSymbol
    writeGroups w grammar.Symbols.NoiseSymbols terminalMap grammar.Groups
    writeProductions w terminalMap nonterminalMap grammar.Productions
    writeLALRStates w terminalMap nonterminalMap grammar.LALRStates
    writeDFAStates w terminalMap grammar.Symbols.NoiseSymbols grammar.Groups grammar.DFAStates