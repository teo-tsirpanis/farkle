// Copyright (c) 2018 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.EGTFile

open Farkle

/// What can go wrong with reading an EGT file.
type EGTReadError =
    /// A [sequence error](Farkle.SeqError) did happen.
    | ListError of ListError
    /// Boolean values should only be `0` or `1`.
    /// If they are not, thet it's undefined by the documentation.
    /// But we will call it an error.
    | InvalidBoolValue of byte
    /// An invalid entry code was encountered.
    /// Valid entry codes are these letters: `EbBIS`.
    | InvalidEntryCode of byte
    /// An entry of `expected` type was requested, but something else was returned instead.
    | InvalidEntryType of expected: string
    /// The string you asked for is not terminated
    | UnterminatedString
    /// takeString has a bug. The developer _should_ be contacted
    /// in case this type of error is encountered
    | TakeStringBug
    /// Records should start with `M`, but this one started with something else.
    | InvalidRecordTag of byte
    /// The file's structure is not recognized. This is a generic error.
    | UnknownEGTFile
    /// You have tried to read a CGT file instead of an EGT file.
    /// The former is _not_ supported.
    | ReadACGTFile
    /// The file you specified does not exist.
    | FileNotExist of string
    /// The item at the given index of a list was not found.
    | IndexNotFound of uint32
    with
        override x.ToString() =
            match x with
            | ListError x -> sprintf "List error: %O" x
            | InvalidBoolValue x -> sprintf "Invalid boolean value (neither 0 nor 1): %d." x
            | InvalidEntryCode x -> x |> char |> sprintf "Invalid entry code: '%c'."
            | InvalidEntryType x -> sprintf "Unexpected entry type. Expected a %s." x
            | UnterminatedString -> "String terminator was not found."
            | TakeStringBug -> "The function takeString exhibited a very unlikely bug. If you see this error, please file an issue on GitHub."
            | InvalidRecordTag x -> x |> char |> sprintf "The record tag '%c' is not 'M', as it should have been."
            | UnknownEGTFile -> "The given grammar file is not recognized."
            | ReadACGTFile ->
                "The given file is a CGT file, not an EGT one."
                + " You should update to the latest version of GOLD Parser Builder (at least over Version 5.0.0)"
                + " and save the tables as \"Enhanced Grammar tables (Version 5.0)\"."
            | FileNotExist x -> sprintf "The given file (%s) does not exist." x
            | IndexNotFound x -> sprintf "The index %d was not found in a list." x

type Entry =
    | Empty
    | Byte of byte
    | Boolean of bool
    | UInt16 of uint16
    | String of string

type Record = Record of Entry list

type EGTFile = EGTFile of Record list