// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<AutoOpen>]
/// Some useful functions and types that could be used from many points from the library.
module Farkle.Common

open System

/// Raises an exception.
/// This function should be used when handling an impossible edge case is very tedious.
/// It should be used __very__ sparingly.
let impossible() = failwith "Hello there! I am a bug. Nice to meet You! If I am here, then something that was thought to be impossible did happen. And if You are (un)lucky enough to see me, can You please open a Github issue? Thank You very much and I am sorry for any inconvenience!"

/// Returns the value of an `Option`.
/// Raises an exception if the option was `None`.
/// Are you inside a `State` monad and don't want to spill your code with `StateResult`?
/// Are you definitely sure that your `Option` does _really_ contain a value, but the type system disagrees?
/// In this case, you should use me!
/// But use me carefully and __very__ sparingly.
/// That thing is like `unsafePerformIO`, but fortunately, not-so-destructive.
let mustBeSome x = x |> Option.defaultWith impossible

let (|RMCons|RMNil|) (x: ReadOnlyMemory<_>) =
    if not x.IsEmpty then
        RMCons (x.Span.Item 0, x.Slice 1)
    else
        RMNil

/// Ignores the parameter and returns `None`.
let inline none _ = None

/// Converts a function to curried form.
let inline curry f x y = f(x, y)

/// Curries and flips the arguments of a function.
let inline yrruc f y x = f(x, y)

/// Converts a function to uncurried form.
let inline uncurry f (x, y) = f x y

/// Flips the arguments of a two-parameter curried function.
let inline flip f x y = f y x

/// Swaps the elements of a pair.
let inline swap (x, y) = (y, x)

/// Faster functions to compare two objects.
module FastCompare =

    /// Compares the first object with another object of the same type.
    /// The types must implement the `IComparable<T>` generic interface.
    /// This function is faster than the F#'s compare methods because it is much less lightweight.
    let inline compare (x1: 'a) (x2: 'a) = (x1 :> IComparable<'a>).CompareTo(x2)

    let inline greater x1 x2 = compare x1 x2 > 0
    let inline greaterOrEqual x1 x2 = compare x1 x2 >= 0

    let inline smaller x1 x2 = compare x1 x2 < 0
    let inline smallerOrEqual x1 x2 = compare x1 x2 <= 0

/// Some more utilities to work with lists.
module List =

    /// Builds a character list from the given string.
    let inline ofString (x: string) =
        x.ToCharArray()
        |> List.ofArray

    /// Creates a string from the given character list.
    let inline toString x: string = x |> Array.ofList |> System.String

/// Some utilities to work with strings
module String =

    /// See `List.toString`.
    let ofList = List.toString

    /// See `List.ofString`.
    let toList = List.ofString

    /// Returns a string that contains the specific string a specified number of times.
    /// The function memoizes the results, so it is better to first give the string argument to the function, and reuse the curried function, if you plan to use it many times.
    let repeat input =
        let dict = System.Collections.Generic.Dictionary()
        let rec impl times =
            match dict.TryGetValue times with
            | true, x -> x
            | false, _ ->
                let x =
                    match times with
                    | 0u -> ""
                    | x when x % 2u = 0u ->
                        let x = impl (x / 2u)
                        x + x
                    | x -> input + impl (x - 1u)
                dict.Add (times, x)
                x
        impl

    let inline length x = x |> String.length |> uint32

/// Functions to work with the `FSharp.Core.Result` type.
[<AutoOpen>]
module Result =

    let inline tee fOk fError =
        function
        | Ok x -> fOk x
        | Error x -> fError x

    let apply =
        function
        | Ok f, x -> x |> Result.map f
        | Error x, _ -> Error x

    /// Converts an `option` into a `Result`.
    let failIfNone message =
        function
        | Some x -> Ok x
        | None -> Error message

    /// Returns the value of a `Result` or raises an exception.
    let inline returnOrFail result = tee id (failwithf "%O") result

    /// Returns if the given `Result` succeeded.
    let inline isOk x = match x with | Ok _ -> true | Error _ -> false

    /// Returns if the given `Result` failed.
    let inline isError x = match x with | Ok _ -> false | Error _ -> true

    /// A shorthand operator for `Result.bind`.
    let inline (>>=) m f = Result.bind f m

    /// Collects a sequence of Results and accumulates their values.
    /// If the sequence contains an error the first reported error will be returned.
    let collect xs =
        Seq.foldBack (fun next result ->
            match next, result with
            | Ok r, Ok rs -> Ok(r :: rs)
            | _, Error m -> Error m
            | Error m, _ -> Error m) xs (Ok []) 

    type EitherBuilder() =
        member __.Zero() = Ok ()
        member __.Bind(m, f) = Result.bind f m
        member __.Return(x) = Ok x
        member __.ReturnFrom(x) = x
        member __.Combine (a, b) = Result.bind b a
        member __.Delay f = f
        member __.Run f = f ()
        member __.TryWith (body, handler) =
            try
                body()
            with
            | e -> handler e
        member __.TryFinally (body, compensation) =
            try
                body()
            finally
                compensation()
        member x.Using(d:#IDisposable, body) =
            let result = fun () -> body d
            x.TryFinally (result, fun () ->
                match d with
                | null -> ()
                | d -> d.Dispose())
        member x.While (guard, body) =
            if not <| guard () then
                x.Zero()
            else
                Result.bind (fun () -> x.While(guard, body)) (body())
        member x.For(s:seq<_>, body) =
            x.Using(s.GetEnumerator(), fun enum ->
                x.While(enum.MoveNext,
                    x.Delay(fun () -> body enum.Current)))

    /// Wraps computations in an error handling computation expression.
    let either = EitherBuilder()
