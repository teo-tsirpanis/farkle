// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle.Grammar
open Farkle.Collections

/// A grammar, as needed by the parser.
/// This type is different from `Farkle.Grammar.GOLDGrammar`, because this describes a full grammar as generated by GOLD Parser.
/// A runtime grammar however can be created from anywhere.
type RuntimeGrammar =
    abstract DFA: StateTable<DFAState>
    abstract LALR: StateTable<LALRState>
    abstract Groups: SafeArray<Group>

/// Functions to work with `RuntimeGrammar`s.
[<RequireQualifiedAccess>]
module RuntimeGrammar =

    /// The grammar's DFA states.
    let dfaStates (x: #RuntimeGrammar) = x.DFA

    /// The grammar's lexical groups.
    let groups (x: #RuntimeGrammar) = x.Groups

    /// The grammar's LALR states.
    // I was going to make a separate type for this and the DFA, based on functions.
    // This would support auto-generated chained ifs, but I changed my mind after reading this:
    // https://softwareengineering.stackexchange.com/questions/193786/
    let lalrStates (x: #RuntimeGrammar) = x.LALR