// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Grammar
open Farkle.Parser
open System
open System.Collections
open System.Collections.Generic
open System.Collections.Immutable
open System.Runtime.CompilerServices

/// The reason an LALR conflict failed to be resolved.
type LALRConflictReason =
    /// No prececence info were specified.
    | NoPrecedenceInfo
    /// Precedence info were specified in only one of the two objects.
    | PartialPrecedenceInfo
    /// The objects were specified in different operator scopes.
    | DifferentOperatorScope
    /// The objects have the same precedence but no associativity.
    | PrecedenceOnlySpecified
    /// The productions have the same precedence. This
    /// reason is specified only on Reduce-Reduce conflicts.
    | SamePrecedence
    /// The operator scope cannot resolve Reduce-Reduce conflicts.
    | CannotResolveReduceReduce
    override x.ToString() =
        match x with
        | NoPrecedenceInfo -> "no precedence info were specified"
        | PartialPrecedenceInfo -> "precedence info were specified in only one of the two objects"
        | DifferentOperatorScope -> "the objects were specified in different operator scopes"
        | PrecedenceOnlySpecified -> "the objects had the same precedence but were declared in a \
PrecedenceOnly associativity group"
        | SamePrecedence -> "the productions had the same precedence"
        | CannotResolveReduceReduce -> "the symbols' operator scope cannot resolve Reduce-Reduce conflicts. \
To enable it, pass a boolean value of true to the operator scope's constructor"

/// The type of an LALR conflict.
type LALRConflictType =
    /// A Shift-Reduce conflict
    | ShiftReduce of StateToShiftTo: uint32 * ProductionToReduce: Production
    /// A Reduce-Reduce conflict
    | ReduceReduce of Production1: Production * Production2: Production
with
    /// Creates an `LALRConflictType` from the given conflicted `LALRAction`s.
    /// An exception is raised if the actions are neither both "reduce" nor a "shift" and a "reduce".
    static member Create act1 act2 =
        match act1, act2 with
        | LALRAction.Shift state, LALRAction.Reduce prod
        | LALRAction.Reduce prod, LALRAction.Shift state ->
            ShiftReduce(state, prod)
        | LALRAction.Reduce prod1, LALRAction.Reduce prod2 ->
            ReduceReduce(prod1, prod2)
        | _ -> failwithf "A conflict between %A and %A is impossible" act1 act2
    override x.ToString() =
        match x with
        | ShiftReduce (idx, prod) ->
            sprintf "Shift-Reduce conflict between shifting to state %d and reducing production %O" idx prod
        | ReduceReduce (prod1, prod2) ->
            sprintf "Reduce-Reduce conflict between productions %O and %O" prod1 prod2

/// An LALR conflict.
/// It arises when the parser can take different
/// actions when encountering a `Terminal` or the end.
type LALRConflict = {
    /// The index of the `LALRState` the conflict is taking place.
    StateIndex: uint32
    /// The symbol upon whose encounter, the conflict happens.
    /// `None` means the conflict happens when the parser reaches the end of input.
    [<Nullable(2uy, 1uy)>] Symbol: Terminal option
    /// The type of the conflict.
    Type: LALRConflictType
    /// The reason Farkle could not resolve the conflict.
    Reason: LALRConflictReason
}
with
    /// Creates an `LALRConflict`.
    static member internal Create stateIndex symbol act1 act2 reason = {
        StateIndex = stateIndex
        Symbol = symbol
        Type = LALRConflictType.Create act1 act2
        Reason = reason
    }
    override x.ToString() =
        let symbolAsString =
            match x.Symbol with
            | Some term -> string term
            | None -> "(EOF)"
        sprintf "%O, while encountering the symbol %s at state %d. Farkle could not \
automatically resolve the conflict because %O" x.Type symbolAsString x.StateIndex x.Reason

/// Additional options to configure the building process,
/// intended to be used by advanced scenarios. This class
/// is currently empty; options will be added in future releases.
type BuildOptions private() =
    static let _default = BuildOptions()
    /// The default options.
    static member Default = _default

[<RequireQualifiedAccess>]
/// An error the builder encountered.
type BuildError =
    /// Some symbols cannot be distinguished from each other.
    | IndistinguishableSymbols of Symbols: DFASymbol Set
    /// A symbol can contain zero characters.
    /// If many symbols are nullable, they will
    /// be marked as indistinguishable instead.
    | NullableSymbol of Symbol: DFASymbol
    /// An LALR conflict did occur.
    | LALRConflict of Conflict: LALRConflict
    /// A nonterminal has no productions.
    | EmptyNonterminal of Name: string
    /// A production is defined twice.
    | DuplicateProduction of Head: Nonterminal * Handle: ImmutableArray<LALRSymbol>
    /// An error occurred while parsing a regular expression.
    | RegexParseError of Symbol: DFASymbol * Error: ParserError
    /// The grammar has more symbols than the supported limit.
    | SymbolLimitExceeded
    /// A precompilable designtime Farkle was attempted to be
    /// built with its grammar having not been precompiled.
    | GrammarNotPrecompiled
    /// The maximum number of terminals and nonterminals
    /// a grammar built by Farkle can have.
    /// Currently set to 2^20; sixteen times more of what
    /// GOLD Parser can handle. To be more specific, a
    /// grammar can have at most 2^20 terminals _and_ 2^20
    /// nonterminals.
    // This limitation was imposed to be able to store
    // more information in the upper bits of an index,
    // in optimized operations. Still, it is a very large
    // number for symbols in a grammar.
    static member SymbolLimit = 1 <<< 20
    override x.ToString() =
        match x with
        | IndistinguishableSymbols xs ->
            let symbols = xs |> Seq.map DFASymbol.toString |> String.concat ", "
            sprintf "Cannot distinguish between symbols: %s. \
The conflict is caused when two or more regular expressions can match the same text." symbols
        | NullableSymbol x ->
            sprintf "The symbol %s can contain zero characters." <| DFASymbol.toString x
        | LALRConflict xs -> xs.ToString()
        | EmptyNonterminal xs ->
            sprintf "Nonterminal <%s> has no productions. \
If you want to define a nonterminal with an empty production you can use \
the production builder called 'empty' (or 'ProductionBuilder.Empty' in C#)." xs
        | DuplicateProduction (head, handle) ->
            sprintf "Production %s is defined more than once." (Production.Format(head, handle))
        | RegexParseError (sym, err) ->
            sprintf "Error while parsing the regex of %s: %O" (DFASymbol.toString sym) err
        | SymbolLimitExceeded ->
            sprintf "A grammar built by Farkle cannot have \
more than %d terminals or more than %d nonterminals."
                BuildError.SymbolLimit BuildError.SymbolLimit
        | GrammarNotPrecompiled -> "The grammar has not been precompiled. \
Make sure that the package Farkle.Tools.MSBuild is installed. Learn more at \
https://teo-tsirpanis.github.io/Farkle/the-precompiler.html"

/// A commonly used set of characters.
type PredefinedSet = private {
    _Name: string
    _CharacterRanges: (char * char) list
    CharactersThunk: Lazy<char Set>
}
with
    static member private CharactersImpl x =
        Seq.collect (fun (cFrom, cTo) -> {cFrom .. cTo}) x |> set
    /// Creates a `PredefinedSet` with the specified name and character ranges.
    static member Create name ranges = {
        _Name = name
        _CharacterRanges = ranges
        CharactersThunk = lazy (PredefinedSet.CharactersImpl ranges)
    }
    /// The set's name. Used for informative purposes.
    member x.Name = x._Name
    /// A sequence of tuples that show the inclusive ranges of characters that belong to this set.
    member x.CharacterRanges = Seq.ofList x._CharacterRanges
    /// The set's characters.
    member x.Characters = x.CharactersThunk.Value
    /// The set's character count.
    member x.Count = x.Characters.Count
    interface IEnumerable with
        member x.GetEnumerator() = (x.Characters :> IEnumerable).GetEnumerator()
    interface IEnumerable<char> with
        member x.GetEnumerator() = (x.Characters :> IEnumerable<_>).GetEnumerator()
    interface IReadOnlyCollection<char> with
        member x.Count = x.Count
