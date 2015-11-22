// Learn more about F# at http://fsharp.net

namespace MSBuild.Tekla.Tasks.CppCheck
#if INTERACTIVE
#r "Microsoft.Build.Framework.dll";;
#r "Microsoft.Build.Utilities.v4.0.dll";;
#endif

open FSharp.Data
open System
open System.IO
open System.Xml.Linq
open System.Diagnostics
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open Microsoft.Win32
open MsbuildTasksUtils
open MsbuildTasks

type CppCheckError = XmlProvider<"""<error file="E:\\TSSRC\\Common.cpp" line="4" id="missingInclude" severity="style" msg="Not Found" />""">

type CppCheckTask() as this =
    inherit Task()
    let logger : TaskLoggingHelper = new TaskLoggingHelper(this)
    let executor : CommandExecutor = new CommandExecutor(logger, int64(1500000))
    let _cppCheckExec = "cppcheck.exe"
    let mutable userCancelCtrl : bool = false

    member val totalViolations : int = 0 with get, set
    member val ToOutputData : string list = [] with get, set

    /// Solution Path, Required
    [<Required>]
    member val SolutionPathToAnalyse = "" with get, set

    member val ProjectPathToAnalyse = "" with get, set

    member val ProjectNameToAnalyse = "" with get, set

    /// Optional result target file. Must be unique to each test run.
    [<Required>]
    member val CppCheckOutputPath = "" with get, set

    /// path for Cppcheck executable, default expects cppcheck in path
    member val CppCheckOutputType = "vs7" with get, set
    member val CppCheckPath = _cppCheckExec with get, set
    member val CppCheckOptions = "" with get, set
    member val CppCheckDefines = "" with get, set
    member val CppCheckIgnores = "" with get, set

    member val UseRelativePaths = false with get, set

    /// Verify Xml output
    member x.VerifyOutput(logger  : TaskLoggingHelper) =
        lazy(
            if String.IsNullOrWhiteSpace(x.CppCheckOutputType) then logger.LogError("Output Type cannot be empty, use vs or xml-version-1 or xml-version-2")
            elif not("xml-version-2" = x.CppCheckOutputType) && not("xml-version-1" = x.CppCheckOutputType) && not("vs7" = x.CppCheckOutputType) then logger.LogError("Output Type Invalid, use vs or xml-version-1 or xml-version-2")
            elif "xml-version-2" = x.CppCheckOutputType || "xml-version-1" = x.CppCheckOutputType && String.IsNullOrWhiteSpace(x.CppCheckOutputType) then logger.LogError("CppCheckOutputType: Output Report Path should be defined for xml reporting")
            )

    /// Verify Cppcheck is found on Path
    member x.verifyCppCheckExecProperties(logger  : TaskLoggingHelper) =
        lazy(
            if String.IsNullOrWhiteSpace(x.CppCheckPath) then logger.LogError("CppCheckPath Cannot Be Empty, Remove to use from Path")
            elif System.IO.Path.IsPathRooted(x.CppCheckPath) then 
                if not(System.IO.File.Exists(x.CppCheckPath)) then logger.LogError(sprintf "CppCheckPath: %s Cannot Be Found on System, Set Path Correctly" x.CppCheckPath)
            elif not(Utils().ExistsOnPath(x.CppCheckPath)) then logger.LogError(sprintf "CppCheckPath: %s Cannot Be Found on PATH, Set PATH variable Correctly" x.CppCheckPath)
            elif not(System.IO.File.Exists(x.SolutionPathToAnalyse)) then logger.LogError(sprintf "SolutionPathToAnalyse: %s not found, Set SolutionDir" x.SolutionPathToAnalyse)
            )

    member x.generateCommandLineArgs =
        let builder = new CommandLineBuilder()

        if x.CppCheckOutputType = "xml-version-1"  || x.CppCheckOutputType = "vs7" then
            builder.AppendSwitch("--xml")
        elif x.CppCheckOutputType = "xml-version-2" then
            builder.AppendSwitch("--xml-version=2")

        // options
        if not(String.IsNullOrWhiteSpace(x.CppCheckOptions)) then
            let values = x.CppCheckOptions.Split(";".ToCharArray())
            for value in values do
                builder.AppendSwitch(value)

        // defines
        if not(String.IsNullOrWhiteSpace(x.CppCheckDefines)) then
            let values = x.CppCheckDefines.Split(";".ToCharArray())
            for value in values do
                builder.AppendSwitchIfNotNull("-D", value)

        // ignore
        if not(String.IsNullOrWhiteSpace(x.CppCheckIgnores)) then
            let values = x.CppCheckIgnores.Split(";".ToCharArray())
            for value in values do
                builder.AppendSwitch(value.Trim())

        if x.ProjectPathToAnalyse = "" then
            builder.AppendSwitch(Directory.GetParent(x.SolutionPathToAnalyse).ToString())
        else
            builder.AppendSwitch(Directory.GetParent(x.ProjectPathToAnalyse).ToString())

        builder.ToString()

    member x.ExecuteCppCheck executor cmdLineArgs =
        // set environment
        let env = Map.ofList [("CPPCHECK_INPUT", x.CppCheckPath)]

        let getReportName = 
            if not(x.ProjectNameToAnalyse = "") then 
                sprintf "cppcheck-result-%s.xml" x.ProjectNameToAnalyse
            else
                "cppcheck-result-0.xml"

        let addLine (line:string) =
            
            use wr = new StreamWriter(Path.Combine(x.CppCheckOutputPath, getReportName), true)
            if x.UseRelativePaths then
                let pathAbs = Directory.GetParent(x.SolutionPathToAnalyse).ToString() + "\\"
                let data = line.Replace(pathAbs, "./")
                wr.WriteLine(data)
            else
                wr.WriteLine(line)
            this.totalViolations <- this.totalViolations + 1

        let returncode = (executor :> ICommandExecutor).ExecuteCommand(x.CppCheckPath, cmdLineArgs, env, Environment.CurrentDirectory)

        if returncode > 0 then
            if this.BuildEngine = null then
                Console.WriteLine("CppCheck Failed: Number of tries exceeded")
            else
                logger.LogWarning("CppCheck Failed: " + x.CppCheckPath + " " + cmdLineArgs)

            (executor :> ICommandExecutor).GetStdOut |> fun s -> for i in s do  if this.BuildEngine = null then Console.WriteLine(i) else logger.LogWarning(i)
            true
        else
            if userCancelCtrl then true
            else
                if not(x.CppCheckOutputType = "vs7") then

                    if not(Directory.Exists(x.CppCheckOutputPath)) then
                        Directory.CreateDirectory(x.CppCheckOutputPath) |> ignore

                    (executor :> ICommandExecutor).GetStdError |> fun s -> for i in s do addLine(i)

                else
                    let datastr = (executor :> ICommandExecutor).GetStdError
                    if datastr <> list.Empty then
                        for i in datastr do
                        try
                            let errorline = CppCheckError.Parse(i)
                            this.totalViolations <- this.totalViolations + 1
                            if this.BuildEngine = null then
                                Console.WriteLine(i);
                            else
                                logger.LogWarning("", errorline.Severity, "", errorline.File, errorline.Line, 0, 0, 0, errorline.Msg)
                        with
                        | ex -> printfn "Exception! %s " (ex.Message)
                true

    override x.Execute() =
        this.verifyCppCheckExecProperties(logger).Force()
        this.VerifyOutput(logger).Force()

        let mutable result = not(logger.HasLoggedErrors)
        if result then
            let stopWatchTotal = Stopwatch.StartNew()
            logger.LogMessage(sprintf "CppCheckCopCommand: %s %s" x.CppCheckPath x.generateCommandLineArgs)
            result <- x.ExecuteCppCheck executor x.generateCommandLineArgs
            logger.LogMessage(sprintf "Total Violations: %u" this.totalViolations)
            logger.LogMessage(sprintf "CppCheck End: %u ms" stopWatchTotal.ElapsedMilliseconds)

        result

    interface ICancelableTask with
        member this.Cancel() =
            userCancelCtrl <- true
            (executor :> ICommandExecutor).CancelExecution |> ignore
            ()
