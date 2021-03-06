// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Common

open System
open System.Collections.Generic
open System.Reflection
open System.Threading

/// A reference type whose value can only be set once.
type internal SetOnce< [<ComparisonConditionalOn; EqualityConditionalOn>] 'T> = private {
    mutable _IsSet: int
    mutable _Value: 'T
}
with
    /// Tries to set this `SetOnce`'s value to an object.
    /// Returns whether the value was changed.
    /// This method is thread-safe, in the sense that only
    /// one thread will ever be able to set a value to this object.
    member x.TrySet v =
        if Interlocked.Exchange(&x._IsSet, 1) = 0 then
            x._Value <- v
            Thread.MemoryBarrier()
            true
        else
            false
    /// Returns whether this `SetOnce` has a value set.
    member x.IsSet = x._IsSet <> 0
    /// Returns the `SetOnce`'s value - if it is set - or the given object otherwise.
    member x.ValueOrDefault(def) =
        match x._IsSet with
        | 0 -> def
        | _ -> x._Value
    /// Creates a `SetOnce` object whose value can be set at a later time.
    /// (try to guess how many times)
    static member Create() = {
        _IsSet = 0
        _Value = Unchecked.defaultof<_>
    }
    /// Creates a `SetOnce` object whose value is already set and cannot be changed.
    static member Create x = {
        _IsSet = 1
        _Value = x
    }
    override x.ToString() =
        match x._IsSet with
        | 0 -> "(not set)"
        | _ -> x._Value.ToString()

/// Functions to work with the `FSharp.Core.Result` type.
module internal Result =

    let tee fOk fError =
        function
        | Ok x -> fOk x
        | Error x -> fError x

    /// Consolidates a sequence of `Result`s into a `Result` of a list.
    /// Errors are consilidated into a list as well.
    let collect xs = Seq.foldBack (fun x xs ->
        match x, xs with
        | Ok x, Ok xs -> Ok <| x :: xs
        | Error x, Ok _ -> Error [x]
        | Ok _, (Error _ as xs) -> xs
        | Error x, Error xs -> Error <| x :: xs) xs (Ok [])

    /// Returns a `Result` that is successful if both given results
    /// are successful, and is failed if at least one of them is failed.
    /// In the former case the returned result will carry its parameters'
    /// values, and in the latter it will carry their combined errors.
    let combine x1 x2 =
        match x1, x2 with
        | Ok x1, Ok x2 -> Ok(x1, x2)
        | Ok _, Error x
        | Error x, Ok _ -> Error x
        | Error x1, Error x2 -> Error(x1 @ x2)

    /// Returns the value of a `Result` or raises an exception.
    let returnOrFail result = tee id (failwithf "%O") result

module internal Reflection =
    let getAssemblyInformationalVersion (asm: Assembly) =
        let versionString =
            asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
        match versionString.IndexOf('+') with
        | -1 -> versionString
        | plusPosition -> versionString.Substring(0, plusPosition)

[<AutoOpen>]
module internal ErrorHandling =

    /// Raises an exception if `x` is null.
    let inline nullCheck argName x =
        if isNull x then
            nullArg argName

module internal Delegate =

    #if NET
    open System.Diagnostics.CodeAnalysis
    #endif

    /// Creates a delegate from an arbitrary
    /// object's `Invoke` method. Useful turning
    /// optimized closures to delegates without
    /// an extra level of indirection.
    let ofInvokeMethod<'TDelegate,
                        #if NET
                        // We have to tell the IL Linker to spare the Invoke method.
                        [<DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)>]
                        #endif
                        'TFunction
        when 'TDelegate :> Delegate and 'TFunction : not struct> (func: 'TFunction) =
        Delegate.CreateDelegate(typeof<'TDelegate>, func, "Invoke", false, true) :?> 'TDelegate

    /// Returns whether the delegate is closed over its first argument.
    let isClosed (del: Delegate) =
        del.Method.GetParameters().Length <> del.GetType().GetMethod("Invoke").GetParameters().Length

/// Object comparers that compare strings in a specific way if both
/// objects are strings. Otherwise they use the default comparer.
module internal FallbackStringComparers =
    let private create (comparer: StringComparer) =
        {new EqualityComparer<obj>() with
            member _.Equals(x1, x2) =
                match x1, x2 with
                // Without parentheses, lit2 is inferred to be the tuple of (x1, x2).
                // Code still compiles but fails at runtime because objects of different
                // types are compared.
                | (:? string as lit1), (:? string as lit2) ->
                    comparer.Equals(lit1, lit2)
                | _ -> EqualityComparer.Default.Equals(x1, x2)
            member _.GetHashCode x =
                match x with
                | null -> 0
                | :? string as lit -> 2 * comparer.GetHashCode(lit)
                | _ -> 2 * x.GetHashCode() + 1}

    let caseSensitive = create StringComparer.Ordinal

    let caseInsensitive = create StringComparer.OrdinalIgnoreCase

    let get isCaseSensitive = if isCaseSensitive then caseSensitive else caseInsensitive
