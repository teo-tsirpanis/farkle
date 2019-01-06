// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module Farkle.Tests.CharStreamTests

open Expecto
open Farkle.Collections.CharStream
open Farkle.Tests
open FsCheck

[<Tests>]
let tests =
    testList "Character stream tests" [
        testProperty "The first character of a character stream works as expected" (fun (CS(cs, _)) ->
            let mutable c2 = '\u0640'
            let mutable idx = getCurrentIndex cs
            Flip.Expect.isTrue "Unexpected end of input" <| readChar cs &c2 &idx
            Expect.equal cs.FirstCharacter c2 "Character mismatch")

        testProperty "Consuming a character stream by a specified number of characters works as expected"
            (fun (CS(cs, str)) steps -> (steps < str.Length && steps > 0) ==> (fun () ->
                use cs = cs
                let idx =
                    let rec impl idx n =
                        let mutable idxNext = idx
                        let mutable c = '\u0549'
                        match readChar cs &c &idxNext with
                        | true when n = steps -> idx
                        | true -> impl idxNext <| n + 1
                        | false -> failtestf "Unexpected end of file after %d iterations" n
                    impl (getCurrentIndex cs) 1
                let span = pinSpan cs idx
                consume false cs span
                Expect.equal steps (int cs.Position.Index) "An unexpected number of characters was consumed"
                let s = unpinSpanAndGenerateString cs span |> fst
                Expect.equal (str.Substring(0, steps)) s "The generated string is different from the original"
                Expect.throws (fun () -> unpinSpanAndGenerateString cs span |> ignore) "Generating a character span can be done more than once"))
    ]