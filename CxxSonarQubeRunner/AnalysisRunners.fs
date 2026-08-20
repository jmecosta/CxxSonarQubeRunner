module AnalysisRunners

open Options
open MsbuildUtilityHelpers
open System.IO
open AntPathMatching

let RunCppCheck(options : OptionsData) =
    if Directory.Exists(options.CxxReportsCppCheckPath) then
        Directory.Delete(options.CxxReportsCppCheckPath, true)
    Directory.CreateDirectory(options.CxxReportsCppCheckPath) |> ignore

    let executor = new CommandExecutor(null, int64(1500000))
    if options.Solution <> "" then
        CppCheckRunner.ExecuteCppCheck(executor,
                            "",
                            "--inline-suppr;--enable=all;-j 8",
                            "",
                            options.Solution,
                            options.CppCheckPath,
                            options.CxxReportsCppCheckPath,
                            (options.Logger :> ICheckerLogger), options.IsVerboseOn)
    else
        CppCheckRunner.ExecuteCppCheck(executor,
                    "",
                    "--inline-suppr;--enable=all;-j 8",
                    "",
                    options.HomePath,
                    options.CppCheckPath,
                    options.CxxReportsCppCheckPath,
                    (options.Logger :> ICheckerLogger), options.IsVerboseOn)

let RunCppLint(options : OptionsData) =

    if Directory.Exists(options.CxxReportsCpplintPath) then
        Directory.Delete(options.CxxReportsCpplintPath, true)
    Directory.CreateDirectory(options.CxxReportsCpplintPath) |> ignore

    let RunTools(file:string) =
        
        let IsExcludedByAntPattern(patternIn:string) = 
            let pattern = "/" + patternIn
            let ant = new Ant(pattern)
            let relativePath = file.Replace(options.HomePath, "").Replace("\\", "/")
            let isMatch = ant.IsMatch(relativePath)
            isMatch

        let isExclude = 
            if options.SonarPropsToUse.ContainsKey("sonar.exclusions") then
                let matchOption = options.SonarPropsToUse.["sonar.exclusions"].Split(',')
                                    |> Seq.tryFind (fun element -> IsExcludedByAntPattern((element.Replace("\\n\\", "").Trim())))
                matchOption.IsSome
            else
                false
        
        if not(isExclude) then
            let executor = new CommandExecutor(null, int64(1500000))
            CppLintRunner.ExecuteCppLint(executor, options.HomePath, file, options.CxxReportsCpplintPath, "", options.PythonPath, options.CppLintPath, "", (options.Logger :> ICheckerLogger), options.IsVerboseOn) |> ignore

    let RunWithWithPattern(pattern:string) = 
        try
            let files = Directory.GetFiles(options.HomePath, pattern, SearchOption.AllDirectories)
            if files.Length > 0 then
                files |> Array.Parallel.map (fun file -> RunTools(file)) |> ignore
            else
                (options.Logger :> ICheckerLogger).ReportMessage(sprintf "No files found using %s in %s" pattern options.HomePath)
        with
        | ex -> (options.Logger :> ICheckerLogger).ReportMessage(sprintf "Some exception running %s in %s => %s" pattern options.HomePath ex.Message)
                (options.Logger :> ICheckerLogger).ReportMessage(sprintf "%s" ex.StackTrace)
    RunWithWithPattern("*.h"  )
    RunWithWithPattern("*.cpp")
    RunWithWithPattern("*.hpp")
    RunWithWithPattern("*.c"  )
    RunWithWithPattern("*.cc" )
    RunWithWithPattern("*.hh" )
