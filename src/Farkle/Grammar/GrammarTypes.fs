// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Grammar

open Farkle
open Farkle.Collections
open Farkle.Grammar.GOLDParser
open System

/// A record that stores how many of each tables exist in an EGT file.
/// It's needed only for verifying that the grammar was successfuly read.
type internal TableCounts =
    {
        /// How many symbols exist.
        SymbolTables: uint16
        /// How many character sets exist.
        CharSetTables: uint16
        /// How many productions exist.
        ProductionTables: uint16
        /// How many DFA states exist.
        DFATables: uint16
        /// How many LALR states exist.
        LALRTables: uint16
        /// How many groups exist.
        GroupTables: uint16
    }

/// Arbitrary metadata a grammar has.
/// A simple key-value collection.
type Properties = Map<string, string>

/// A set of characters. See `RangeSet` too.
type CharSet = RangeSet<char>

/// A type indicating how a group advances.
type AdvanceMode =
    /// The group advances by one token at a time.
    | Token
    /// The group advances by one character at a time.
    | Character

/// A type indicating how the ending symbol of a group is handled.
type EndingMode =
    /// The ending symbol is preserved on the input queue.
    | Open
    /// The ending symbol is consumed.
    | Closed

/// A structure that describes a lexical group.
/// In GOLD, lexical groups are used for situations where a number of recognized tokens should be organized into a single "group".
/// This mechanism is most commonly used to handle line and block comments.
/// However, it is not limited to "noise", but can be used for any content.
type Group =
    {
        /// The name of the group.
        Name: string
        /// The index of the group as it appears on the grammar file.
        Index: uint32
        /// The symbol that represents the group's content.
        ContainerSymbol: Symbol
        /// The symbol that represents the group's start.
        StartSymbol: Symbol
        /// The symbol that represents the group's end.
        EndSymbol: Symbol
        /// The way the group advances.
        AdvanceMode: AdvanceMode
        /// The way the group ends.
        EndingMode: EndingMode
        /// A set of indexes whose corresponding groups can be nested inside this group.
        Nesting: Set<Indexed<Group>>
    }
    interface Indexable with
        member x.Index = x.Index


/// A symbol of a grammar.
and Symbol =
    /// The symbol is a nonterminal.
    | Nonterminal of uint32 * string
    /// The symbol is a terminal.
    | Terminal of uint32 * string
    /// The symbol is noise (comments for example) and is discarded by the parser.
    | Noise of string
    /// The symbol signifies the end of input.
    | EndOfFile
    /// The symbol signifies the start of a group.
    | GroupStart of Indexed<Group> * string
    /// The symbol signifies the end of a group.
    | GroupEnd of string
    /// The symbol was not recognized by the tokenizer.
    | Unrecognized
    with

        /// The name of a symbol
        member x.Name =
            match x with
            | Nonterminal (_, x) -> x
            | Terminal (_, x) -> x
            | Noise x -> x
            | EndOfFile -> "EOF"
            | GroupStart (_, x) -> x
            | GroupEnd x -> x
            | Unrecognized -> "Unrecognized"
        override x.ToString() =
            let literalFormat x =
                let forceDelimiter =
                    x = ""
                    || x.[0] |> Char.IsLetter
                    || x |> String.forall (fun x -> Char.IsLetter x || x = '.' || x = '-' || x = '_')
                if forceDelimiter then
                    sprintf "'%s'" x
                else
                    x
            match x with
            | Nonterminal _ -> sprintf "<%s>" x.Name
            | Terminal _ -> literalFormat x.Name
            | _ -> sprintf "(%s)" x.Name

/// Functions to work with `Symbol`s.
module internal Symbol =

    /// Returns the index of a terminal symbol, or nothing.
    let tryGetTerminalIndex = function Terminal (index, _) -> Some index | _ -> None

/// Functions to work with `Group`s.
[<RequireQualifiedAccess>]
module internal Group =

    /// [omit]
    let inline nesting {Nesting = x} = x

/// The basic building block of a grammar's syntax.
/// It consists of a single nonterminal called the "head".
/// The head is defined to consist of multiple symbols making up the production's "handle".
type Production =
    {
        /// The index of the production as it appears on the grammar file.
        Index: uint32
        /// The head of the production.
        Head: Symbol
        /// The handle of the production.
        Handle: Symbol list
    }
    interface Indexable with
        member x.Index = x.Index
    override x.ToString() =
        let symbols = x.Handle |> List.map (string) |> String.concat " "
        sprintf "%O ::= %s" x.Head symbols

/// A DFA state. It defines the logic that produces `Tokens` out of strings.
/// It consists of edges that the tokenizer follows, depending on the character it encounters.
type DFAState =
    /// This state accepts a symbol. If the state graph cannot be further walked, the included `Symbol` is returned.
    | DFAAccept of index: uint32 * (Symbol * (CharSet * Indexed<DFAState>) list)
    /// This state does not accept a symbol. If the state graph cannot be further walked and an accepting state has not been found, tokenizing fails.
    | DFAContinue of index: uint32 * edges: (CharSet * Indexed<DFAState>) list
    interface Indexable with
        member x.Index =
            match x with
            | DFAAccept (x, _) -> x
            | DFAContinue (x, _) -> x
    /// Returns the edges of the DFA state.
    member x.Edges =
        match x with
        | DFAAccept (_, (_, e)) -> e
        | DFAContinue (_, e) -> e
    override x.ToString() = x |> Indexable.index |> string

/// An action to be taken by the parser.
type LALRAction =
    /// This action indicates the parser to shift to the specified `LALRState`..
    | Shift of Indexed<LALRState>
    /// This action indicates the parser to reduce a `Production`.
    | Reduce of Production
    /// This action is used when a production is reduced and the parser jumps to the state that represents the shifted nonterminal.
    | Goto of Indexed<LALRState>
    /// When the parser encounters this action for a given symbol, the input text is accepted as correct and complete.
    | Accept

/// A LALR state. Many of them define the parsing logic of a `Grammar`.
and LALRState =
    {
        /// The index of the state.
        Index: uint32
        /// The available `LALRAction`s of the state.
        /// Depending on the symbol, the next action to be taken is determined.
        Actions:Map<Symbol, LALRAction>
    }
    interface Indexable with
        member x.Index = x.Index
    override x.ToString() = string x.Index

/// Functions to work with `LALRState`s.
module internal LALRState =
    /// Returns all LALR actions to take when the corresponding symbol is encountered.
    let actions {Actions = actions} = actions
