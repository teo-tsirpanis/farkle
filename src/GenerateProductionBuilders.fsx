// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

#if !FAKE
#load "./.fake/build.fsx/intellisense_lazys.fsx"
#endif

open Scriban
open Scriban.Runtime

let private template = """// This file was generated by Farkle's build system, from a Scriban template.
{{~ func gen_type_params_impl
        x = ""
        for i in 1..$0
            x = x + $1 + i
            if !for.last
                x = x + $2
            end
        end
        ret x
    end
    func type_params
        ret gen_type_params_impl $0 "'T" ", "
    end
    func type_indices
        ret gen_type_params_impl $0 "idx" ", "
    end
    func finish_signature
        ret (gen_type_params_impl $0 "'T" " -> ") + "-> 'TOutput"
    end ~}}
{{~ func gen_builder
        n = $0 ~}}
[<Sealed>]
/// [omit]
type ProductionBuilder<{{ type_params n }}>(members, {{ type_indices n }}) =
    member __.Append(sym) = ProductionBuilder<{{ type_params n }}>(Symbol.append members sym, {{type_indices n }})
    {{~ if n != capacity ~}}
    member __.Extend(df: DesigntimeFarkle<'T{{ n }}>) = ProductionBuilder<{{ type_params n + 1 }}>(Symbol.append members df, {{type_indices n }}, members.Count)
    {{~ else ~}}
    [<Obsolete("Cannot support more than {{ capacity }} significant symbols. Replace calls to Extend with Append, and use FinishRaw.")>]
    member __.Extend _ =
        failwith "Cannot support more than {{ capacity }} significant symbols. Replace calls to Extend with Append, and use FinishRaw."
        |> ignore
    {{~ end ~}}
    member __.Finish(f: {{ finish_signature n }}) : Production<'TOutput> = {
        Members = members.ToImmutableArray()
        Fuse =
            fun arr ->
                f
                    {{~ for i in 1..n ~}}
                    (downcast arr.[idx{{ i }}])
                    {{~ end ~}}
                |> box
    }

{{~ end ~}}

namespace Farkle.Builder

open System
open System.Collections.Immutable

{{~ for i in 1..capacity reversed
        gen_builder i
    end ~}}
"""

let private capacity = 16

let generateProductionBuilders() =
    let template = Template.Parse(template, "Production Builders F# template")
    let so = ScriptObject()
    so.SetValue("capacity", capacity, true)

    let tc = TemplateContext()
    tc.StrictVariables <- true
    tc.PushGlobal so

    template.Render(tc)
