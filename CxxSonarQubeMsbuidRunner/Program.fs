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
            options.ConfigureInstallationOfTools()

            if not(options.InstallMode) then
                options.ValidateSolutionOptions()
                options.ConfigureMsbuildRunner()
            
                options.CreatOptionsForAnalysis()
                let solutionData = options.Setup()
                options.ProvisionProject()
           
                try
                    if SonarRunnerPhases.BeginPhase(options) <> 0 then
                        ret <- 1
                        printf "[CxxSonarQubeMsbuidRunner] Failed to execute Begin Phase, check log"
                    else
                        let targetFile = Path.Combine(options.HomePath, ".sonarqube", "bin", "Targets", "SonarQube.Integration.targets")
                        PatchMSbuildSonarRunnerTargetsFiles(targetFile)
                    
                        if SonarRunnerPhases.RunBuild(options) <> 0 then
                            ret <- 1
                            printf "[CxxSonarQubeMsbuidRunner] Failed to build project, check log in .cxxresults\BuildLog.txt"
                        else
                            // import shared projects if any
                            SharedProjectImporter.ImportSharedProjects(options.Solution, options.ProjectKey.Replace("/k:", ""), solutionData)
                            if SonarRunnerPhases.EndPhase(options) <> 0 then
                                ret <- 1
                                printf "[CxxSonarQubeMsbuidRunner] Failed analyze project, check log"            
                with
                | ex ->
                    printf "Exception During Run: %s \r\n %s" ex.Message ex.StackTrace            
                    ret <- 1

            
                options.Clean()
                options.DuplicateFalsePositives()

        with
        | ex ->
            printf "Exception During Run: %s %s\r\n" ex.Message ex.StackTrace
            ret <- 1
        
    ret
