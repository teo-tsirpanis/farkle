// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Aether
open Farkle
open Farkle.Grammar
open System
open System.Text

type Token =
    {
        Symbol: Symbol
        Position: Position
        Data: string
    }
    with
        static member Symbol_ :Lens<_, _> = (fun x -> x.Symbol), (fun v x -> {x with Symbol = v})
        static member Position_ :Lens<_, _> = (fun x -> x.Position), (fun v x -> {x with Position = v})
        static member Data_ :Lens<_, _> = (fun x -> x.Data), (fun v x -> {x with Data = v})
        static member Create pos sym data = {Symbol = sym; Position = pos; Data = data}
        static member AppendData data x = Optic.map Token.Data_ (fun x -> x + data) x

module Token =

    let dummy = {Symbol = {SymbolType = Nonterminal; Name = ""}; Position = Position.initial; Data = ""}

type Reduction =
    {
        Tokens: (Token * Reduction option) list
        Parent: Production
    }

module Reduction =

    let data {Tokens = tokens} =
        tokens
        |> List.map (fst >> Optic.get Token.Data_)
        |> String.concat ""

type ParseError =
    | IndexNotFound of uint16
    | GotoNotFoundAfterReduction
    | LALRStackEmpty

type LALRResult =
    | Accept of Reduction
    | Shift
    | ReduceNormal of Reduction
    | ReduceEliminated
    | SyntaxError of expected: Symbol list
    | InternalErrors of ParseError list

type ParseMessage =
    | EGTReadError of EGTReadError
    | TokenRead of Token
    | Reduction of Reduction
    | Accept of Reduction
    | LexicalError of Token
    | SyntaxError of expected: Symbol list
    | GroupError
    | InternalErrors of ParseError list
    | FatalError of ParseMessage

module ParseMessage =

    let isError =
        function
        | TokenRead _ | Reduction _ | Accept _ -> false
        | _ -> true

type ParserState =
    internal {
        Grammar: Grammar
        InputStream: char list
        CurrentLALRState: LALRState
        InputStack: Token list
        LALRStack: (Token * (LALRState * Reduction option)) list
        TrimReductions: bool
        CurrentPosition: Position
        GroupStack: Token list
    }
    with
        static member internal  grammar x = x.Grammar
        static member internal  InputStream_ :Lens<_, _> = (fun x -> x.InputStream), (fun v x -> {x with InputStream = v})
        static member internal  CurrentLALRState_ :Lens<_, _> = (fun x -> x.CurrentLALRState), (fun v x -> {x with CurrentLALRState = v})
        static member internal  InputStack_ :Lens<_, _> = (fun x -> x.InputStack), (fun v x -> {x with InputStack = v})
        static member internal  LALRStack_ :Lens<_, _> = (fun x -> x.LALRStack), (fun v x -> {x with LALRStack = v})
        static member internal  trimReductions x = x.TrimReductions
        static member internal  CurrentPosition_ :Lens<_, _> = (fun x -> x.CurrentPosition), (fun v x -> {x with CurrentPosition = v})
        static member internal  GroupStack_ :Lens<_, _> = (fun x -> x.GroupStack), (fun v x -> {x with GroupStack = v})

module ParserState =

    /// Creates a parser state.
    [<CompiledName("Create")>]
    let create trimReductions grammar input =
        {
            Grammar = grammar
            InputStream = input
            CurrentLALRState = grammar.InitialStates.LALR
            InputStack = []
            LALRStack = [Token.dummy, (grammar.InitialStates.LALR, None)]
            TrimReductions = trimReductions
            CurrentPosition = Position.initial
            GroupStack = []
        }