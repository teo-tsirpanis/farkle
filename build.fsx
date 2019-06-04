// Copyright (c) 2018 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r "paket: groupref FakeBuild //"

#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Api
open Fake.BuildServer
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools.Git
open System
open System.IO
open System.Text.RegularExpressions

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "Farkle"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "A modern and easy-to-use parser library for F#"

// List of author names (for NuGet package)
let authors = [ "Theodore Tsirpanis" ]

// File system information
let solutionFile  = "./Farkle.sln"

// Default target configuration
let configuration = DotNet.BuildConfiguration.Release
let configurationAsString = sprintf "%A" configuration

[<Literal>]
let LibraryFramework = "netstandard2.0"

[<Literal>]
let DocumentationAssemblyFramework = "net45"

let exeFramework = "netcoreapp2.1"

let sourceProjects = !! "./src/**/*.??proj"

// The project to be tested
let testProject = "./tests/Farkle.Tests/Farkle.Tests.fsproj"
// Additional command line arguments passed to Expecto.
let testArguments = ""
let testFrameworks = ["netcoreapp2.1"]

let projects = !! "**/*.??proj" -- "**/*.shproj"

// Pattern specifying assemblies to be benchmarked
let benchmarkAssemblies = !! ("./bin/*Benchmarks*/" </> exeFramework </> "*Benchmarks*.dll")

// Additional command line arguments passed to BenchmarkDotNet.
let benchmarkArguments = "--memory true -e github"

let benchmarkReports =
    benchmarkAssemblies
    |> Seq.collect (fun x -> !!(Path.getDirectory x </> "BenchmarkDotNet.Artifacts/results/*-report-github.md"))

let benchmarkReportsDirectory = "./performance/"

let nugetPackages = !! "./bin/*.nupkg"

let releaseArtifacts = nugetPackages

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "teo-tsirpanis"
let gitHome = sprintf "%s/%s" "https://github.com" gitOwner

// The name of the project on GitHub
let gitName = "Farkle"

// The url for the raw files hosted
let gitRaw = Environment.environVarOrDefault "gitRaw" "https://raw.githubusercontent.com/teo-tsirpanis"

// Read additional information from the release notes document
let releaseInfo = ReleaseNotes.load "./RELEASE_NOTES.md"

let releaseNotes =
    let lines s = seq {
        use sr = new StringReader(if isNull s then "" else s)
        let mutable s = ""
        s <- sr.ReadLine()
        while not <| isNull s do
            yield s
            s <- sr.ReadLine()
    }
    match BuildServer.buildServer with
    | BuildServer.AppVeyor ->
        sprintf "This is a build from the commit with id: %s from branch %s/%s"
            AppVeyor.Environment.RepoCommit
            AppVeyor.Environment.RepoName
            AppVeyor.Environment.RepoBranch
        :: AppVeyor.Environment.RepoCommitMessage
        :: (AppVeyor.Environment.RepoCommitMessageExtended |> lines |> List.ofSeq)
    | _ -> releaseInfo.Notes

let nugetVersion =
    match BuildServer.buildServer with
    BuildServer.AppVeyor -> sprintf "%s-ci%s" releaseInfo.NugetVersion AppVeyor.Environment.BuildNumber
    | _ -> releaseInfo.NugetVersion

BuildServer.install [AppVeyor.Installer]

Target.description """Copies binaries from default VS location to expected bin folder, but keeps a subdirectory structure for each project in the src folder to support multiple project outputs"""
Target.create "CopyBinaries" (fun _ ->
    Shell.cleanDir "bin"
    projects
    |> Seq.map (fun f -> ((Path.GetDirectoryName f) </> "bin" </> configurationAsString, "bin" </> (Path.GetFileNameWithoutExtension f)))
    |> Seq.iter (fun (fromDir, toDir) -> Shell.copyDir toDir fromDir (fun _ -> true))
)

let vsProjFunc x = {x with DotNet.BuildOptions.Configuration = configuration}

let inline fCommonOptions x =
    [
        sprintf "/p:Version=%s" nugetVersion
        releaseNotes |> String.concat "%0A" |> sprintf "/p:PackageReleaseNotes=\"%s\""
    ] |> DotNet.Options.withAdditionalArgs <| x

let handleFailedTest (p: ProcessResult) =
    if p.ExitCode <> 0 then
        sprintf "Unit test failed with error code %d" p.ExitCode
        |> Fake.Testing.Common.FailedTestsException
        |> raise

Target.description "Cleans the output directories"
Target.create "Clean" (fun _ ->
    Shell.cleanDirs ["bin"; "temp"]
)

Target.description "Cleans the output documentation directory"
Target.create "CleanDocs" (fun _ -> Shell.cleanDir "docs")

// --------------------------------------------------------------------------------------
// Build library & test project

Target.description "Builds everything in Release mode"
Target.create "Build" (fun _ -> DotNet.build (vsProjFunc >> fCommonOptions) solutionFile)

Target.description "Runs the unit tests using test runner"
Target.create "RunTests" (fun _ ->
    testFrameworks
    |> Seq.iter (fun fx ->
        DotNet.exec
            (fun p -> {p with WorkingDirectory = Path.getDirectory testProject})
            "run"
            (sprintf "--framework %s --configuration %A -- %s" fx DotNet.BuildConfiguration.Debug testArguments)
        |> handleFailedTest))

Target.description "Runs all benchmarks"
Target.create "Benchmark" (fun _ ->
    benchmarkAssemblies
    |> Seq.iter (fun x ->
        DotNet.exec
            (fun p -> {p with WorkingDirectory = Path.GetDirectoryName x})
            x benchmarkArguments |> ignore))

Target.description "Adds the benchmark results to the appropriate folder"
Target.create "AddBenchmarkReport" (fun _ ->
    let reportFileName x = benchmarkReportsDirectory </> (sprintf "%s.%s.md" x nugetVersion)
    Directory.ensure benchmarkReportsDirectory
    Trace.logItems "Benchmark reports: " benchmarkReports
    benchmarkReports
    |> Seq.iter (fun x ->
        let newFn = Regex.Replace(Path.GetFileName x, @"Farkle\.Benchmarks\.(\w+)-report-github\.md", "$1") |> reportFileName
        Shell.copyFile newFn x
        File.applyReplace (String.replace ";" ",") newFn
    )
)

Target.description "Builds the NuGet packages"
Target.create "NuGet" (fun _ ->
    sourceProjects
    |> Seq.iter (
        DotNet.pack (fun p ->
            {p with
                Configuration = configuration
                OutputPath = __SOURCE_DIRECTORY__ @@ "bin" |> Some
            }
            |> fCommonOptions
        )
    )
)

Target.description "Publishes the NuGet packages"
Target.create "PublishNuGet" (fun _ ->
    Paket.push(fun p ->
        {p with
            PublishUrl = "https://www.nuget.org"
            WorkingDir = "bin" })
)

// --------------------------------------------------------------------------------------
// Generate the documentation

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "bin"
let content    = __SOURCE_DIRECTORY__ @@ "docsrc/content"
let output     = __SOURCE_DIRECTORY__ @@ "docs"
let files      = __SOURCE_DIRECTORY__ @@ "docsrc/files"
let templates  = __SOURCE_DIRECTORY__ @@ "docsrc/tools/templates"
let formatting = __SOURCE_DIRECTORY__ @@ "packages/formatting/FSharp.Formatting"
let toolpath = __SOURCE_DIRECTORY__ @@ "packages/formatting/FSharp.Formatting.CommandTool/tools/fsformatting.exe"
let docTemplate = "docpage.cshtml"

let github_release_user = Environment.environVarOrDefault "github_release_user" gitOwner
let githubLink = sprintf "https://github.com/%s/%s" github_release_user gitName

let root isRelease =
    match isRelease with
    | true -> "/Farkle"
    | false -> "file://" + (__SOURCE_DIRECTORY__ @@ "docs")

// Specify more information about your project
let info =
    [
        "project-name", project
        "project-author", String.concat ", " authors
        "project-summary", summary
        "project-github", githubLink
        "project-nuget", "http://nuget.org/packages/Farkle"
    ]

let referenceBinaries = []

let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[   templates;
                            formatting @@ "templates"
                            formatting @@ "templates/reference" ])

let referenceDocs isRelease =
    Directory.ensure (output @@ "reference")

    let binaries () =
        let manuallyAdded =
            referenceBinaries
            |> List.map (fun b -> bin @@ b)

        let conventionBased =
            bin
            |> DirectoryInfo.ofPath
            |> DirectoryInfo.getSubDirectories
            |> Array.filter (fun x -> x.FullName @@ LibraryFramework |> Directory.Exists)
            |> Array.map ((fun x -> x.FullName @@ DocumentationAssemblyFramework @@ (sprintf "%s.dll" x.Name)))
            |> Array.filter File.exists
            |> List.ofArray

        conventionBased @ manuallyAdded

    binaries()
    |> FSFormatting.createDocsForDlls (fun args ->
        { args with
            OutputDirectory = output @@ "reference"
            LayoutRoots =  layoutRootsAll.["en"]
            ProjectParameters =  ("root", root isRelease)::info
            SourceRepository = githubLink @@ "tree/master"
            ToolPath = toolpath}
    )


let copyFiles () =
    Shell.copyRecursive files output true
    |> Trace.logItems "Copying file: "
    Directory.ensure (output @@ "content")
    Shell.copyRecursive (formatting @@ "styles") (output @@ "content") true
    |> Trace.logItems "Copying styles and scripts: "

let docs isRelease =
    File.delete "docsrc/content/release-notes.md"
    Shell.copyFile "docsrc/content/" "RELEASE_NOTES.md"
    Shell.rename "docsrc/content/release-notes.md" "docsrc/content/RELEASE_NOTES.md"

    File.delete "docsrc/content/license.md"
    Shell.copyFile "docsrc/content/" "LICENSE.txt"
    Shell.rename "docsrc/content/license.md" "docsrc/content/LICENSE.txt"


    DirectoryInfo.getSubDirectories (DirectoryInfo.ofPath templates)
    |> Seq.iter
        (fun d ->
            let name = d.Name
            if name.Length = 2 || name.Length = 3 then
                layoutRootsAll.Add(
                    name,
                    [
                        templates @@ name
                        formatting @@ "templates"
                        formatting @@ "templates/reference"
                    ]
                )
        )
    copyFiles ()

    for dir in  [ content; ] do
        let langSpecificPath(lang, path:string) =
            path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
            |> Array.exists(fun i -> i = lang)
        let layoutRoots =
            let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
            match key with
            | Some lang -> layoutRootsAll.[lang]
            | None -> layoutRootsAll.["en"] // "en" is the default language

        FSFormatting.createDocs (fun args ->
            { args with
                Source = content
                OutputDirectory = output
                LayoutRoots = layoutRoots
                ProjectParameters  = ("root", root isRelease)::info
                Template = docTemplate
                ToolPath = toolpath})

Target.description "Watches the documentation source folder and regenerates it on every file change"
Target.create "KeepRunning" (fun _ ->
    use __ = !! "docsrc/content/**/*.*" |> ChangeWatcher.run (fun _ ->
        docs false
    )

    Trace.traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore
)

Target.create "GenerateDocs" (fun _ -> !! "./docs/**" |> Zip.zip "docs" "docs.zip")
Target.description "Generates the website for the project, except for the API documentation - for release"
Target.create "GenerateHelp" (fun _ -> docs true)
Target.description "Generates the website for the project, except for the API documentation - for local use"
Target.create "GenerateHelpDebug" (fun _ -> docs false)

Target.description "Generates the API documentation for the project - for release"
Target.create "GenerateReferenceDocs" (fun _ -> referenceDocs true)
Target.description "Generates the API documentation for the project - for local use"
Target.create "GenerateReferenceDocsDebug" (fun _ -> referenceDocs false)

Target.description "Releases the documentation to GitHub Pages."
Target.create "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    Shell.cleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    Shell.copyRecursive "docs" tempDocsDir true |> Trace.tracefn "Copied %A"
    Staging.stageAll tempDocsDir
    Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" nugetVersion)
    Branches.push tempDocsDir
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target.description "Makes a draft GitHub release ready for final review. Before that, it publishes the documentation, the GitHub packages, and the benchmark report."
Target.create "Release" (fun _ ->
    let user =
        match Environment.environVarOrDefault "github-user" String.Empty with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserInput "Username: "
    let pw =
        match Environment.environVarOrDefault "github-pw" String.Empty with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserPassword "Password: "
    let remote =
        CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (String.endsWith "(push)")
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Staging.stageAll ""
    Commit.exec "" (sprintf "Bump version to %s" nugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" nugetVersion
    Branches.pushTag "" remote nugetVersion

    // release on github
    GitHub.createClient user pw
    |> GitHub.createRelease gitOwner gitName nugetVersion
        (fun x ->
            {x with
                Draft = true
                Name = sprintf "Version %s" nugetVersion
                Prerelease = releaseInfo.SemVer.PreRelease.IsSome
                Body = String.concat Environment.NewLine releaseNotes})
    |> GitHub.uploadFiles releaseArtifacts
    |> GitHub.publishDraft
    |> Async.RunSynchronously
)

Target.description "The CI generates the documentation, the NuGet packages, and uploads them as artifacts, along with the benchmark report."
Target.create "CI" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build target <Target>' to override

"Clean"
    ==> "Build"
    ==> "CopyBinaries"
    ==> "RunTests"
    ==> "NuGet"
    ==> "CI"

"CopyBinaries" ==> "Benchmark"

[""; "Debug"]
|> Seq.map (sprintf "GenerateReferenceDocs%s")
|> Seq.iter ((==>) "CopyBinaries" >> ignore)

"CleanDocs"
    ==> "GenerateHelp"
    ==> "GenerateReferenceDocs"
    ==> "GenerateDocs"
    ==> "ReleaseDocs"

"GenerateDocs"
    ==> "CI"

"CleanDocs"
    ==> "GenerateHelpDebug"
    ==> "GenerateReferenceDocsDebug"
    ==> "KeepRunning"

"Benchmark"
    ==> "AddBenchmarkReport"
    ==> "Release"

"Benchmark"
    ==> "CI"

"ReleaseDocs"
    ==> "Release"

"NuGet"
    ==> "PublishNuGet"
    ==> "Release"

Target.runOrDefault "NuGet"
