module CppCheckRunner

open Microsoft.Build.Utilities
open System
open System.IO
open MsbuildUtilityHelpers

let generateCommandLineArgs(defines:string, options:string, ignores:string, basePath:string, reportPath:string) =
    let builder = new CommandLineBuilder()

    builder.AppendSwitch("--xml-version=2")

    // options
    if not(String.IsNullOrWhiteSpace(options)) then
        let values = options.Split(";".ToCharArray())
        for value in values do
            builder.AppendSwitch(value)

    // defines
    if not(String.IsNullOrWhiteSpace(defines)) then
        let values = defines.Split(";".ToCharArray())
        for value in values do
            builder.AppendSwitchIfNotNull("-D", value)

    // ignore
    if not(String.IsNullOrWhiteSpace(ignores)) then
        let values = ignores.Split(";".ToCharArray())
        for value in values do
            builder.AppendSwitch(value.Trim())

    // report Path
    if not(String.IsNullOrWhiteSpace(reportPath)) then
        builder.AppendSwitch("--output-file=" + reportPath)

    if Directory.Exists(basePath) then
        builder.AppendSwitch(basePath)
    else
        builder.AppendSwitch("--project=" + basePath)

    builder.ToString()

let ExecuteCppCheck(executor:ICommandExecutor,
                            defines:string,
                            options:string,
                            ignores:string,
                            pathToAnalyseSlnPrjOrDir:string,
                            cppCheckPath:string,
                            reportPath:string,
                            logger:ICheckerLogger, isDebug:bool) =
    // set environment
    let reportName = "cppcheck-report.xml"
    if Directory.Exists(reportPath) then
        Directory.Delete(reportPath, true)

    Directory.CreateDirectory(reportPath) |> ignore
    let cmdLineArgs = generateCommandLineArgs(defines, options, ignores, pathToAnalyseSlnPrjOrDir, Path.Combine(reportPath, reportName))
    let env = Map.ofList [("CPPCHECK_INPUT", cppCheckPath)]

    if isDebug then
        logger.ReportMessage("CppCheck Command: " + cppCheckPath + " " + cmdLineArgs)

    let returncode = executor.ExecuteCommand(cppCheckPath, cmdLineArgs, env, Environment.CurrentDirectory)

    if returncode > 0 then
        logger.ReportMessage("CppCheck Failed...")
        executor.GetStdOut |> fun s -> for i in s do logger.ReportMessage(i)
    
    returncode = 0
