open System
open System.IO
open System.IO.Compression
open System.Text
open System.Net
open System.Text.RegularExpressions
open System.Diagnostics
open System.Reflection
open FSharp.Data

open Options



[<EntryPoint>]
let main argv = 
    let arguments = parseArgs(argv)
    let mutable ret = 0

    try
        if arguments.ContainsKey("h") then
            ShowHelp()
        else
            let options = new OptionsData(argv)
            
            HelpersMethods.cprintf(ConsoleColor.Cyan, "[CxxSonarQubeMsbuidRunner] Install MSBuild Runner")
            options.ConfigureMsbuildRunner(options)
            HelpersMethods.cprintf(ConsoleColor.Cyan, "[CxxSonarQubeMsbuidRunner] Install Static Analysis Tools")
            options.ConfigureInstallationOfTools()

            if not(options.InstallMode) then
                let skipBuild = options.ValidateSolutionOptions(options.UserSonarScannerCli)
                options.CreatOptionsForAnalysis()
                let solutionData = options.Setup(options, skipBuild)
                options.ProvisionProject()
           
                try
                    if SonarRunnerPhases.BeginPhase(options) <> 0 then
                        ret <- 1
                        printf "[CxxSonarQubeMsbuidRunner] Failed to execute Begin Phase, check log"
                        ShowHelp()
                        raise(new Exception())
                        
                    if not(options.UserSonarScannerCli) then
                        let targetFile = Path.Combine(options.SonarQubeTempPath, "bin", "Targets", "SonarQube.Integration.targets")
                        PatchMSbuildSonarRunnerTargetsFiles(targetFile, options)
                    
                    if not(skipBuild) && SonarRunnerPhases.RunBuild(options) <> 0 then
                        ret <- 1
                        printf "[CxxSonarQubeMsbuidRunner] Failed to build project, check log in .cxxresults\BuildLog.txt"
                        raise(new Exception())

                    if File.Exists(options.SolutionTargetFile) then
                        File.Delete(options.SolutionTargetFile)

                    if options.UserSonarScannerCli then
                        if SonarRunnerPhases.CLiPhase(options) <> 0 then
                            ret <- 1
                            printf "[CxxSonarQubeMsbuidRunner] Failed analyze project, check log"
                    else
                        // import shared projects if any
                        SharedProjectImporter.ImportSharedProjects(options.SonarQubeTempPath, options.ProjectKey.Replace("/k:", ""), solutionData)
                        if SonarRunnerPhases.EndPhase(options) <> 0 then
                            ret <- 1
                            printf "[CxxSonarQubeMsbuidRunner] Failed analyze project, check log"
                with
                | ex ->
                    printf "Exception During Run: %s \r\n %s" ex.Message ex.StackTrace            
                    ret <- 1

                if options.ApplyFalseAndPermissionTemplate then
                    options.Clean()
                    try
                        options.DuplicateFalsePositives()
                    with
                    | ex ->
                        printf "Exception During Run: %s \r\n %s" ex.Message ex.StackTrace
        with
        | ex ->
            printf "Exception During Run: %s \r\n %s" ex.Message ex.StackTrace
            ret <- 1
    ret
