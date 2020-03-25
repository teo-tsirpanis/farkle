// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar.EGTFile

open Farkle.Grammar
open System
open System.Collections.Immutable

[<AutoOpen>]
module private EGTNeoUtils =

    // I initially wanted a more fancy header, one that was readable
    // in both Base64 and ASCII, perhaps loaded with easter eggs. But
    // I settled to this, plain and boring header.
    let [<Literal>] egtNeoHeader = "Farkle Parser Tables/v6.0-alpha"

    // The headers for each section of the EGTneo file.
    // They must be present in the file in that order.

    let [<Literal>] propertiesHeader = "Properties"
    let [<Literal>] terminalsHeader = "Terminals"
    let [<Literal>] nonterminalsHeader = "Nonterminals"
    let [<Literal>] noiseSymbolsHeader = "Noise Symbols"
    let [<Literal>] startSymbolHeader = "Start Symbol"
    let [<Literal>] groupsHeader = "Groups"
    let [<Literal>] productionsHeader = "Productions"
    let [<Literal>] lalrHeader = "LALR"
    let [<Literal>] dfaHeader = "DFA"

/// Functions to read a grammar from EGTneo files.
/// EGTneo files are more compact and easier to read from Farkle.
module EGTNeoReader =

    open EGTReader

    module private Implementation =
        let checkHeader span hdr =
            lengthMustBeAtLeast span 1
            let h = wantString span 0
            if h <> hdr then
                span.Slice 1
            else
                invalidEGTf "Invalid EGTneo section header: expected '%s', got '%s'." hdr h
                ReadOnlySpan.Empty

        let readProperties span =
            let span = checkHeader span propertiesHeader
            let len = span.Length / 2

            let b = ImmutableDictionary.CreateBuilder()
            for i = 0 to len - 1 do
                b.Add(wantString span (2 * i), wantString span (2 * i + 1))

            b.ToImmutable()

        // Terminals, nonterminals and noise symbols are stored in
        // about the same format. The latter just ignore the index.
        let inline readLALRSymbols header fSymbol span =
            let span = checkHeader span header

            let b = ImmutableArray.CreateBuilder(span.Length)
            for i = 0 to span.Length - 1 do
                b.Add(fSymbol(uint32 i, wantString span i))

            b.MoveToImmutable()

        let readTerminals span = readLALRSymbols terminalsHeader Terminal span

        let readNonterminals span = readLALRSymbols terminalsHeader Nonterminal span

        let readNoiseSymbols span = readLALRSymbols noiseSymbolsHeader (snd >> Noise) span

        let inline readSingleValued header span =
            let span = checkHeader span header
            lengthMustBe span 1
            &span.[0]

        let readStartSymbol (nonterminals: ImmutableArray<Nonterminal>) span =
            match readSingleValued startSymbolHeader span with
            | Entry.UInt32 idx when int idx < nonterminals.Length ->
                nonterminals.[int idx]
            | x -> invalidEGTf "Cannot retrieve start symbol. Got %A." x

        let readGroups (terminals: ImmutableArray<_>) (noiseSymbols: ImmutableArray<_>) span =
            let span = checkHeader span groupsHeader
            let groupCount = wantUInt32 span 0 |> int
            let groups = ImmutableArray.CreateBuilder(groupCount)

            let mutable i = 1
            while i < span.Length do
                let name = wantString span (i + 0)
                let container =
                    let containerIndex = int <| wantUInt32 span (i + 2)
                    if wantBoolean span (i + 1) then
                        Choice1Of2 terminals.[containerIndex]
                    else
                        Choice2Of2 noiseSymbols.[containerIndex]
                let start = GroupStart(wantString span (i + 3), uint32 groups.Count)
                let gEnd =
                    match span.[i + 4] with
                    | Entry.Empty -> None
                    | Entry.String str -> Some <| GroupEnd str
                    | e -> failwithf "Cannot retrieve group end for %s. Got %A." name e
                let advanceMode =
                    match span.[i + 5] with
                    | Entry.Byte 'T'B -> AdvanceMode.Token
                    | Entry.Byte 'C'B -> AdvanceMode.Character
                    | e -> failwithf "Cannot retrieve group advance mode for %s. Got %A." name e
                let endingMode =
                    match span.[i + 6] with
                    | Entry.Byte 'O'B -> EndingMode.Open
                    | Entry.Byte 'C'B -> EndingMode.Closed
                    | e -> failwithf "Cannot retrieve group ending mode for %s. Got %A." name e
                let nesting =
                    let nestingCount = wantUInt32 span (i + 7) |> int
                    let nesting = ImmutableHashSet.CreateBuilder()
                    for j = 0 to nestingCount - 1 do
                        nesting.Add(wantUInt32 span (i + 8 + j)) |> ignore
                    nesting.ToImmutable()

                groups.Add {
                    Name = name
                    ContainerSymbol = container
                    Start = start
                    End = gEnd
                    AdvanceMode = advanceMode
                    EndingMode = endingMode
                    Nesting = nesting
                }
                i <- i + 8 + nesting.Count

            groups.MoveToImmutable()

/// Functions to write a grammar to EGTneo files.
module EGTNeoWriter =

    open EGTWriter

    module private Implementation =

        let writeProperties w (props: ImmutableDictionary<_,_>) =
            let len = 1 + 2 * props.Count
            let arr = Array.zeroCreate len

            arr.[0] <- Entry.String propertiesHeader
            let mutable i = 1
            for p in props do
                arr.[i] <- Entry.String p.Key
                arr.[i + 1] <- Entry.String p.Value
                i <- i + 2

            writeRecord w (ReadOnlySpan arr)

        let inline writeLALRSymbols w header (symbols: ImmutableArray<_>) =
            let len = 1 + symbols.Length
            let arr = Array.zeroCreate len

            arr.[0] <- Entry.String header
            for i = 0 to symbols.Length - 1 do
                let sym = symbols.[i]
                if (^Symbol: (member Index: uint32) (sym)) <> uint32 i then
                    failwithf "%A is out of order (found at position %d)." sym i
                arr.[i + 1] <- Entry.String (^Symbol: (member Name: string) (sym))

            writeRecord w (ReadOnlySpan arr)

        let writeTerminals w (terms: ImmutableArray<Terminal>) =
            writeLALRSymbols w terminalsHeader terms

        let writeNonterminals w (terms: ImmutableArray<Nonterminal>) =
            writeLALRSymbols w nonterminalsHeader terms

        let writeNoiseSymbols w (noises: ImmutableArray<_>) =
            let len = 1 + noises.Length
            let arr = Array.zeroCreate len

            arr.[0] <- Entry.String noiseSymbolsHeader
            for i = 0 to noises.Length - 1 do
                let (Noise sym) = noises.[i]
                arr.[i + 1] <- Entry.String sym

            writeRecord w (ReadOnlySpan arr)

        let inline writeSingleValued w header entry =
            let arr = [|
                Entry.String header
                entry
            |]

            writeRecord w (ReadOnlySpan arr)

        let indexOf message (xs: ImmutableArray<_>) x =
            match xs.IndexOf x with
            | -1 -> failwithf "%s %O not found" message x
            | idx -> Entry.Int idx

        let writeStartSymbol w (nonterminals: ImmutableArray<Nonterminal>) startSymbol =
            indexOf "Start symbol" nonterminals startSymbol
            |> writeSingleValued w startSymbolHeader

        let writeGroups w noiseSymbols (groups: ImmutableArray<Group>) =
            let arr = ResizeArray()

            arr.Add <| Entry.String groupsHeader
            arr.Add <| Entry.Int groups.Length

            for i = 0 to groups.Length - 1 do
                let group = groups.[i]
                arr.Add <| Entry.String group.Name
                arr.Add <| Entry.Boolean group.IsTerminal
                arr.Add <|
                    match group.ContainerSymbol with
                    | Choice1Of2(Terminal(idx, _)) -> Entry.UInt32 idx
                    | Choice2Of2 x -> indexOf "Noise symbol" noiseSymbols x
                arr.Add <| match group.Start with GroupStart(name, _) -> Entry.String name
                arr.Add <|
                    match group.End with
                    | Some(GroupEnd name) -> Entry.String name
                    | None -> Entry.Empty
                arr.Add
                    (match group.AdvanceMode with
                    | AdvanceMode.Token -> 'T'B
                    | AdvanceMode.Character -> 'C'B
                    |> Entry.Byte)
                arr.Add
                    (match group.EndingMode with
                    | EndingMode.Open -> 'O'B
                    | EndingMode.Closed -> 'C'B
                    |> Entry.Byte)
                arr.Add <| Entry.Int group.Nesting.Count
                group.Nesting
                |> Seq.sort
                |> Seq.iter(Entry.UInt32 >> arr.Add)

            let arr = arr.ToArray()
            writeRecord w (ReadOnlySpan arr)
