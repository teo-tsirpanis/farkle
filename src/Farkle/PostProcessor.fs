// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle

open Farkle
open Farkle.Grammar
open System

/// An exception that gets thrown when a post-processor does not find the appropriate `Fuser` for a production.
/// This means that the post-processor is not properly configured.
exception internal FuserNotFound

/// <summary>Post-processors convert strings of a grammar into more
/// meaningful types for the library that uses the parser.</summary>
/// <typeparam name="T">The type of the final object this post-processor will return from a gramamr.</typeparam>
type PostProcessor<[<CovariantOut>] 'T> =
    /// <summary>Fuses the many members of a <see cref="Production"/> into one arbitrary object.</summary>
    /// <remarks>Fusing must always succeed. In very case of an error like
    /// an unrecognized production, the function has to throw an exception.</remarks>
    abstract Fuse: Production * obj[] -> obj
    inherit IO.ITransformer<Terminal>