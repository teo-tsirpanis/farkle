// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Benchmarks

open BenchmarkDotNet.Attributes
open Farkle
open System.IO
open System.Text
open System.Runtime.InteropServices
open Farkle.PostProcessor
open System.Security

[<MemoryDiagnoser>]
/// This benchmark measures the performance of Farkle (in both lazy and eager mode),
/// and a native Pascal GOLD Parser engine I had written in the past.
/// Their task is to both read an EGT file describing the GOLD Meta Language, and then parse its source file.
type InceptionBenchmark() =
    let isWindows64 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSArchitecture = Architecture.X64

    [<DllImport("goldparser_win64.dll", CallingConvention = CallingConvention.StdCall); SuppressUnmanagedCodeSecurity>]
    static extern int ParseFile([<MarshalAs(UnmanagedType.LPStr)>] string _EGTFile, [<MarshalAs(UnmanagedType.LPStr)>] string _InputFile)

    member inline __.doIt lazyLoad =
        let rf = RuntimeFarkle.createFromPostProcessor PostProcessor.ast "inception.egt"
        use f = File.OpenRead "inception.grm"
        RuntimeFarkle.parseStream rf ignore lazyLoad Encoding.UTF8 f
        |> returnOrFail

    [<Benchmark>]
    member __.InceptionBenchmarkFarkleEager() = __.doIt false

    [<Benchmark>]
    member __.InceptionBenchmarkFarkleLazy() = __.doIt true

    [<Benchmark(Baseline=true)>]
    member __.InceptionBenchmarkLazarus() =
        if isWindows64 then
            if ParseFile("inception.egt", "inception.grm") <> 0 then
                failwith "Native GOLD Parser failed"