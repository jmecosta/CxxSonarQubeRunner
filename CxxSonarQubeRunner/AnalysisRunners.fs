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

let RunVeraRatsAndCppLint(options : OptionsData) =

    if Directory.Exists(options.CxxReportsRatsPath) then
        Directory.Delete(options.CxxReportsRatsPath, true)
    Directory.CreateDirectory(options.CxxReportsRatsPath) |> ignore

    if Directory.Exists(options.CxxReportsCpplintPath) then
        Directory.Delete(options.CxxReportsCpplintPath, true)
    Directory.CreateDirectory(options.CxxReportsCpplintPath) |> ignore

    if Directory.Exists(options.CxxReportsVeraPath) then
        Directory.Delete(options.CxxReportsVeraPath, true)

    Directory.CreateDirectory(options.CxxReportsVeraPath) |> ignore

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
            if options.VeraPath <> "" then
                let executor = new CommandExecutor(null, int64(1500000))
                VeraRunner.ExecuteVera(executor, file, options.VeraPath, options.CxxReportsVeraPath, "", options.HomePath, (options.Logger :> ICheckerLogger), options.IsVerboseOn) |> ignore
            let executor = new CommandExecutor(null, int64(1500000))
            CppLintRunner.ExecuteCppLint(executor, options.HomePath, file, options.CxxReportsCpplintPath, "", options.PythonPath, options.CppLintPath, "", (options.Logger :> ICheckerLogger), options.IsVerboseOn) |> ignore
            if options.RatsPath <> "" then
                let executor = new CommandExecutor(null, int64(1500000))
                RatsRunner.ExecuteRats(executor, options.RatsPath, options.CxxReportsRatsPath, file, "", (options.Logger :> ICheckerLogger), options.IsVerboseOn) |> ignore

    Directory.GetFiles(options.HomePath, "*.h", SearchOption.AllDirectories) |> Seq.iter (fun file -> RunTools(file))
    Directory.GetFiles(options.HomePath, "*.cpp", SearchOption.AllDirectories) |> Array.Parallel.map (fun file -> RunTools(file)) |> ignore
    Directory.GetFiles(options.HomePath, "*.hpp", SearchOption.AllDirectories) |> Seq.iter (fun file -> RunTools(file))
    Directory.GetFiles(options.HomePath, "*.c", SearchOption.AllDirectories) |> Seq.iter (fun file -> RunTools(file))
    Directory.GetFiles(options.HomePath, "*.cc", SearchOption.AllDirectories) |> Seq.iter (fun file -> RunTools(file))
    Directory.GetFiles(options.HomePath, "*.hh", SearchOption.AllDirectories) |> Seq.iter (fun file -> RunTools(file))
