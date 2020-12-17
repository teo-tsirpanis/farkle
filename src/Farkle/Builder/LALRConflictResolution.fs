// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder.LALRConflictResolution

open Farkle.Builder
open Farkle.Builder.OperatorPrecedence
open Farkle.Grammar
open System
open System.Collections.Generic
open System.Collections.Immutable

module private TerminalKindComparers =
    let create (comparer: StringComparer) =
        {new EqualityComparer<obj>() with
            member _.Equals(x1, x2) =
                match x1, x2 with
                // Without parentheses, lit2 is inferred to be the tuple of (x1, x2).
                // Code still compiles but fails at runtime because objects of different
                // types are compared.
                | (:? string as lit1), (:? string as lit2) ->
                    comparer.Equals(lit1, lit2)
                | _ -> x1.Equals(x2)
            member _.GetHashCode x =
                match x with
                | :? string as lit -> 2 * comparer.GetHashCode(lit)
                | _ -> 2 * x.GetHashCode() + 1}

    let caseSensitive = create StringComparer.Ordinal

    let caseInsensitive = create StringComparer.OrdinalIgnoreCase

    let get isCaseSensitive = if isCaseSensitive then caseSensitive else caseInsensitive

/// <summary>The decision a <see cref="LALRConflictResolver"/> took.</summary>
type ConflictResolutionDecision =
    /// <summary>The resolver chose the first option.</summary>
    /// <remarks>In Shift-Reduce conflicts it means to shift.
    /// In Reduce-Reduce conflicts it means to reduce the first production.</remarks>
    | ChooseOption1
    /// The resolver chose the second option.
    /// <remarks>In Shift-Reduce conflicts it means to reduce.
    /// In Reduce-Reduce conflicts it means to reduce the second production.</remarks>
    | ChooseOption2
    /// The resolver chose neither option.
    /// <remarks>The parser will fail with a syntax error.</remarks>
    | ChooseNeither
    /// The resolver cannot choose an action. The reason is specified.
    | CannotChoose of Reason: LALRConflictReason
    /// Inverts the decision. Option 1 becomes Option 2 and vice versa.
    /// Otherwise the object is returned unchanged. This function allows
    /// easily handling Reduce-Shift conflicts.
    member internal x.Invert() =
        match x with
        | ChooseOption1 -> ChooseOption2
        | ChooseOption2 -> ChooseOption1
        | _ -> x

type private PrecedenceInfo = {
    GroupIndex: int
    Precedence: int
    Associativity: AssociativityType
}

/// An object that resolves LALR conflicts. By default
/// its virtual methods fail to perform any resolution.
type LALRConflictResolver() =
    static let defaultResolver = LALRConflictResolver()
    /// Tries to resolve a Shift-Reduce conflict.
    // Thankfully we don't have to bother with EOF because we can't shift on it.
    abstract ResolveShiftReduceConflict: shiftTerminal: Terminal -> reduceProduction: Production -> ConflictResolutionDecision
    default _.ResolveShiftReduceConflict _ _ = CannotChoose NoPrecedenceInfo
    /// Tries to resolve a Reduce-Reduce conflict.
    abstract ResolveReduceReduceConflict: production1: Production -> production2: Production -> ConflictResolutionDecision
    default _.ResolveReduceReduceConflict _ _ = CannotChoose NoPrecedenceInfo
    /// A default resolver that always fails.
    static member Default = defaultResolver

/// A conflict resolver that uses Farkle's operator precedence infrastructure.
type internal PrecedenceBasedConflictResolver(operatorGroups: OperatorGroup seq, terminalMap: IReadOnlyDictionary<_,_>,
        productionMap: IReadOnlyDictionary<_,_>, caseSensitive) =
    inherit LALRConflictResolver()

    static let ChooseShift = ChooseOption1
    static let ChooseReduce = ChooseOption2
    static let ChooseReduce1 = ChooseOption1
    static let ChooseReduce2 = ChooseOption2

    let comparer = TerminalKindComparers.get caseSensitive

    let groupLookup =
        let dict = ImmutableDictionary.CreateBuilder(comparer)
        let mutable i = 0
        for x in operatorGroups do
            for x in x.AssociativityGroups do
                for x in x.Symbols do
                    dict.TryAdd(x, i) |> ignore
            i <- i + 1
        dict.ToImmutable()

    let precInfoLookups =
        operatorGroups
        |> Seq.mapi (fun i x ->
            let dict = ImmutableDictionary.CreateBuilder(comparer)
            let mutable prec = 1
            for x in x.AssociativityGroups do
                let precInfo = {GroupIndex = i; Precedence = prec; Associativity = x.AssociativityType}
                for x in x.Symbols do
                    dict.TryAdd(x, precInfo) |> ignore
                prec <- prec + 1

            dict.ToImmutable()
        )
        |> Array.ofSeq

    let canResolveReduceReduce =
        operatorGroups
        |> Seq.map (fun x -> x.ResolvesReduceReduceConflict)
        |> Array.ofSeq

    let hasPrecInfo =
        let dict = Dictionary(comparer)
        let rec impl (x: obj) idx =
            idx < precInfoLookups.Length && (precInfoLookups.[idx].ContainsKey x || impl x (idx + 1))
        fun (x: obj) ->
            match dict.TryGetValue x with
            | true, result -> result
            | false, _ ->
                let result = impl x 0
                dict.Add(x, result)
                result

    let getTerminalPrecInfo term =
        // We don't need to memoize this function; it runs in constant time.
        match terminalMap.TryGetValue term with
        | true, termObj ->
            match groupLookup.TryGetValue termObj with
            | true, groupIdx -> ValueSome precInfoLookups.[groupIdx].[termObj]
            | false, _ -> ValueNone
        // This line does not execute under normal circumstances; the
        // terminal map has a corresponding object for each terminal.
        | false, _ -> ValueNone

    let getProductionObj =
        let dict = Dictionary(comparer)
        fun ({Handle = handle} as prod) ->
            // Unless the production has a contextual precedence token,
            // it assumes the P&A of the last terminal it has.
            // This is what (Fs)Yacc does.
            match productionMap.TryGetValue prod with
            | true, prodObj -> ValueSome prodObj
            | false, _ ->
                match dict.TryGetValue prod with
                | true, memoizedResult -> memoizedResult
                | false, _ ->
                    let mutable lastTerminalPrecInfo = null
                    let mutable i = handle.Length - 1
                    while lastTerminalPrecInfo = null && i >= 0 do
                        match handle.[i] with
                        | LALRSymbol.Terminal term when hasPrecInfo terminalMap.[term] ->
                            lastTerminalPrecInfo <- terminalMap.[term]
                        | _ -> ()
                        i <- i - 1

                    let result = ValueOption.ofObj lastTerminalPrecInfo
                    dict.Add(prod, result)
                    result

    let getProductionPrecInfo prod =
        match getProductionObj prod with
        | ValueSome prodObj ->
            match groupLookup.TryGetValue prodObj with
            | true, prodIdx -> ValueSome precInfoLookups.[prodIdx].[prodObj]
            | false, _ -> ValueNone
        | ValueNone -> ValueNone

    override _.ResolveShiftReduceConflict term prod =
        match getTerminalPrecInfo term, getProductionPrecInfo prod with
        | ValueSome termPrecInfo, ValueSome prodPrecInfo
            when termPrecInfo.GroupIndex = prodPrecInfo.GroupIndex ->
            // The symbols surely exist in the group.
            let {Precedence = termPrec; Associativity = assoc} = termPrecInfo
            let {Precedence = prodPrec} = prodPrecInfo

            if termPrec > prodPrec then
                ChooseShift
            elif termPrec = prodPrec then
                match assoc with
                | AssociativityType.NonAssociative -> ChooseNeither
                | AssociativityType.LeftAssociative -> ChooseReduce
                | AssociativityType.RightAssociative -> ChooseShift
                | AssociativityType.PrecedenceOnly -> CannotChoose PrecedenceOnlySpecified
            else
                ChooseReduce

        | ValueSome _, ValueSome _ -> CannotChoose DifferentOperatorGroup
        | ValueSome _, ValueNone | ValueNone, ValueSome _ -> CannotChoose PartialPrecedenceInfo
        | ValueNone, ValueNone -> CannotChoose NoPrecedenceInfo

    override _.ResolveReduceReduceConflict prod1 prod2 =
        match getProductionPrecInfo prod1, getProductionPrecInfo prod2 with
        | ValueSome prod1PrecInfo, ValueSome prod2PrecInfo
            when prod1PrecInfo.GroupIndex = prod2PrecInfo.GroupIndex ->
            if canResolveReduceReduce.[prod1PrecInfo.GroupIndex] then
                let {Precedence = prod1Prec} = prod1PrecInfo
                let {Precedence = prod2Prec} = prod2PrecInfo

                if prod1Prec > prod2Prec then
                    ChooseReduce1
                elif prod1Prec = prod2Prec then
                    CannotChoose SamePrecedence
                else
                    ChooseReduce2
            else
                CannotChoose CannotResolveReduceReduce

        | ValueSome _, ValueSome _ -> CannotChoose DifferentOperatorGroup
        | ValueSome _, ValueNone | ValueNone, ValueSome _ -> CannotChoose PartialPrecedenceInfo
        | ValueNone, ValueNone -> CannotChoose NoPrecedenceInfo
