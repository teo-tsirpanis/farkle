// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Collections

open Farkle
open LanguagePrimitives
open Operators.Checked
open System
open System.IO

/// An continuous range of characters that is
/// stored by its starting index and length.
type CharSpan = private CharSpan of idxFrom: uint64 * idxTo: uint64
with
    /// The span's zero-based index of the first character.
    member x.StartingIndex = match x with | CharSpan (x, _) -> x
    /// The span's zero-based index of the last character.
    member x.EndingIndex = match x with | CharSpan (_, x) -> x
    override x.ToString() = sprintf "[%d,%d]" x.StartingIndex x.EndingIndex

/// The internal structure to support `CharStreamSource.DynamicBlock`.
type private DynamicBlock =
    {
        /// The `TextReader` that powers the stream.
        Reader: TextReader
        /// A temporary buffer of characters that are read from the reader
        /// and are going to be read to be generated.
        mutable Buffer: char[]
        /// The index of the first element in the buffer.
        mutable BufferStartingIndex: uint64
        /// The index of the last element in the buffer.
        /// This field is stored because the buffer is
        /// not always full in cases like an end of input.
        mutable BufferEndingIndex: uint64
    }

/// A representation of a `CharStream`.
type private CharStreamSource =
    /// A representation of a `CharStream` that stores
    /// the characters in one continuous area of memory.
    /// It is not recommended for large files.
    | StaticBlock of ReadOnlyMemory<char>
    /// A representation of a `CharStream` that lazily
    /// loads input from a `TextReader` when it is needed
    /// and unloads it when it is not.
    | DynamicBlock of DynamicBlock
    /// Gets a specified character by index; if it exists in memory, or raises an exception.
    member x.Item
        with get idx =
            match x with
            | StaticBlock sb -> sb.Span.[int idx]
            | DynamicBlock db ->
                let position = int <| idx - db.BufferStartingIndex
                if position > 0 && position < db.Buffer.Length then
                    db.Buffer.[position]
                else
                    failwithf "Trying to get character #%d, while only characters from #%d to #%d have been loaded"
                        idx db.BufferStartingIndex <| db.BufferStartingIndex + uint64 db.Buffer.Length
    /// [omit]
    member x.LengthSoFar =
        match x with
        | StaticBlock x -> uint64 x.Length
        | DynamicBlock db -> db.BufferEndingIndex + GenericOne

/// A data structure that supports efficient and copy-free access to a read-only sequence of characters.
/// It is not thread-safe.
type CharStream = private {
    /// The stream's source.
    Source: CharStreamSource
    /// The index of the first element that _must_ be retained in memory
    /// because it is going to be used to generate a token.
    mutable StartingIndex: uint64
    /// [omit]
    mutable _Position: Position
}
with
    /// The stream's current position.
    member x.Position = x._Position
    /// The stream's character at its current position.
    member x.FirstCharacter = x.Source.[x.Position.Index]
    /// Returns the length of the input; or at least the
    /// length of the input that has ever crossed the memory.
    member x.LengthSoFar = x.Source.LengthSoFar

/// A type pointing to a character in a character stream.
type [<Struct>] CharStreamIndex = private CharStreamIndex of uint64
with
    /// The zero-based index this object points to, starting from the beginning of the stream.
    member x.Index = match x with CharStreamIndex idx -> idx

/// A .NET delegate that is the interface between the `CharStream` API and the post-processor.
/// It accepts a generic type (a `Symbol` usually), the `Position` of the symbol, and a
/// `ReadOnlySpan` of characters that are going to be converted into an object.
/// This type is not an F# native function type, because of limitations while handling `ReadOnlySpan`s.
type CharStreamCallback<'symbol> = delegate of 'symbol * Position * ReadOnlySpan<char> -> obj

/// Functions to create and work with `CharStream`s.
/// They are not thread-safe.
module CharStream =

    /// Creates a `CharStreamIndex` from a `CharStream` that points to its current position.
    let getCurrentIndex (cs: CharStream) = CharStreamIndex cs.Position.Index

    /// Reads the `idx`th character of `cs`, places it in `c` and returns `true`, if there are more characters left to be read.
    /// Otherwise, returns `false`.
    let readChar cs (c: outref<_>) (idx: byref<CharStreamIndex>) =
        match cs.Source with
        | _ when idx.Index < cs.StartingIndex ->
            failwithf "Trying to view the %dth character of a stream, while the first %d have already been read." idx.Index cs.StartingIndex
        | StaticBlock sb when idx.Index < uint64 sb.Length ->
            c <- sb.Span.[int idx.Index]
            idx <- idx.Index + GenericOne |> CharStreamIndex
            true
        | StaticBlock _ -> false

    /// Creates a `CharSpan` that contains the next `idxTo` characters of a `CharStream` from its position.
    /// On the dynamic block character stream, it ensures that the characters in the span stay in memory.
    let pinSpan {_Position = {Index = idxFrom}} (CharStreamIndex idxTo) = CharSpan (idxFrom, idxTo)

    /// Creates a new `CharSpan` that spans one character more than the given one.
    /// A `CharStream` must also be given to validate its length so that out-of-bounds errors are prevented.
    let extendSpanByOne (cs: CharStream) (CharSpan (idxStart, idxEnd)) =
        if idxEnd < cs.LengthSoFar then
            CharSpan (idxStart, idxEnd + GenericOne)
        else
            failwith "Trying to extend a character span by one character past these that were already read."

    /// Creates a new `CharSpan` from two continuous spans, i.e. that starts at the first's start, and ends at the second's end.
    /// Returns `None` if they were not continuous.
    let extendSpans (CharSpan (start1, end1)) (CharSpan (start2, end2)) =
        if end1 = start2 then
            CharSpan (start1, end2)
        else
            failwithf "Trying to extend a character span that ends at %d, with one that starts at %d." end1 start2

    /// Advances a `CharStream`'s position by one character.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, this character and all before it can be marked to be released from memory.
    let consumeOne doUnpin (cs: CharStream) =
        if cs.Position.Index < uint64 cs.LengthSoFar then
            cs._Position <- Position.advance cs.FirstCharacter cs._Position
            if doUnpin then
                cs.StartingIndex <- cs._Position.Index
        else
            failwith "Cannot consume a character stream past its end."

    /// Advances a `CharStream`'s position by as many characters the given `CharSpan` indicates.
    /// These characters will not be shown again on new `CharStreamView`s.
    /// Calling this function does not affect the pinned `CharSpan`s.
    /// Optionally, the characters before the span can be marked to be released from memory.
    let consume doUnpin (cs: CharStream) (CharSpan (idxStart, idxEnd) as csp) =
        if cs.Position.Index = idxStart then
            for _i = int idxStart to int idxEnd do
                consumeOne doUnpin cs
        else
            failwithf "Trying to consume the character span %O, from a stream that was left at %d." csp cs.Position.Index

    /// Creates an arbitrary object out of the characters at the given `CharSpan` at the returned `Position`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    let unpinSpanAndGenerate symbol (fPostProcess: CharStreamCallback<'symbol>) cs (CharSpan (idxStart, idxEnd) as csp) =
        match cs.Source with
        | StaticBlock sb when cs.StartingIndex <= idxStart && uint64 sb.Length > idxEnd ->
            cs.StartingIndex <- idxEnd + 1UL
            let length = idxEnd - idxStart + 1UL |> int
            let span = sb.Span.Slice(int idxStart, length)
            fPostProcess.Invoke(symbol, cs.Position, span), cs.Position
        | StaticBlock _ ->
            failwithf "Trying to read the character span %O, from a stream that was last read at %d." csp cs.StartingIndex

    /// Creates a string out of the characters at the given `CharSpan` at the returned `Position`.
    /// After that call, the characters at and before the span might be freed from memory, so this function must not be used twice.
    /// It is recommended to use the `unpinSpanAndGenerate` function to avoid excessive allocations, unless you specifically want a string.
    let unpinSpanAndGenerateString cs c_span =
        let (s, pos) =
            unpinSpanAndGenerate
                null
                (CharStreamCallback(fun _ _ data -> box <| data.ToString()))
                cs
                c_span // Created by cable
        s :?> string, pos

    /// Creates a `CharStream` from a `ReadOnlyMemory` of characters.
    let ofReadOnlyMemory mem = {Source = StaticBlock mem; StartingIndex = 0UL; _Position = Position.initial}

    let ofString (x: string) = x.AsMemory() |> ofReadOnlyMemory
