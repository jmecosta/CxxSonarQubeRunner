// Learn more about F# at http://fsharp.net

namespace MSBuild.Tekla.Tasks.Vera
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
open FSharp.Collections.ParallelSeq

type VeraErrorX(filename:string, line:string, severity:string, message:string, source:string) =
    member val filename = filename
    member val line = line
    member val severity = severity
    member val message = message
    member val source = source

type VeraTask(executorIn : ICommandExecutor) as this =
    inherit Task()
    let logger : TaskLoggingHelper = new TaskLoggingHelper(this)
    let _VeraExec = "Vera.exe"
    let syncLock = new System.Object()

    new() = VeraTask(null)

    member val totalViolations : int = 0 with get, set
    member val buildok : bool = true with get, set
    member val counter : int = 0 with get, set
    member val ToOutputData : string list = [] with get, set
    member val UseRelativePaths = false with get, set

    /// Solution Path, Required
    [<Required>]
    member val SolutionPathToAnalyse = "" with get, set

    member val ProjectNameToAnalyse = "" with get, set

    /// Optional result target file. Must be unique to each test run.
    [<Required>]
    member val VeraOutputPath = "" with get, set

    /// path for Vera executable, default expects Vera in path
    member val VeraOutputType = "vs7" with get, set
    member val VeraPath = _VeraExec with get, set
    member val VeraOptions = "" with get, set
    member val VeraIgnores = "" with get, set
    member val PathReplacementStrings = "" with get, set

    /// Verify Xml output
    member x.VerifyOutput(logger  : TaskLoggingHelper) =
        lazy(
            if String.IsNullOrWhiteSpace(x.VeraOutputType) then logger.LogError("Output Type cannot be empty, use vs7 or xml")
            elif not("xml" = x.VeraOutputType) && not("vs7" = x.VeraOutputType) then logger.LogError("Output Type Invalid, use vs7 or xml")
            elif "xml" = x.VeraOutputType && String.IsNullOrWhiteSpace(x.VeraOutputType) then logger.LogError("VeraOutputType: Output Report Path should be defined for xml reporting")
            )

    /// Verify Vera is found on Path
    member x.verifyVeraExecProperties(logger  : TaskLoggingHelper) =
        lazy(
            if String.IsNullOrWhiteSpace(x.VeraPath) then logger.LogError("VeraPath Cannot Be Empty, Remove to use from Path")
            elif System.IO.Path.IsPathRooted(x.VeraPath) then 
                if not(System.IO.File.Exists(x.VeraPath)) then logger.LogError(sprintf "VeraPath: %s Cannot Be Found on System, Set Path Correctly" x.VeraPath)
            elif not(Utils().ExistsOnPath(x.VeraPath)) then logger.LogError(sprintf "VeraPath: %s Cannot Be Found on PATH, Set PATH variable Correctly" x.VeraPath)
            elif not(System.IO.File.Exists(x.SolutionPathToAnalyse)) then logger.LogError(sprintf "SolutionPathToAnalyse: %s not found, Set SolutionDir" x.SolutionPathToAnalyse)
            )

    member x.generateCommandLineArgs(fileToAnalyse : string)=
        let builder = new CommandLineBuilder()

        // options
        if not(String.IsNullOrWhiteSpace(x.VeraOptions)) then
            let values = x.VeraOptions.Split(";".ToCharArray())
            for value in values do
                builder.AppendSwitch(value)

        builder.AppendSwitch(fileToAnalyse)

        builder.ToString()

    member x.ExecuteVera filepath =
        let mutable ouputFilePath = ""
        let executor : ICommandExecutor =
            if executorIn = null then
                (new CommandExecutor(logger, int64(1500000))) :> ICommandExecutor
            else
                executorIn
        
        lock syncLock (
            fun () -> 
                let getReportName = 
                    if not(x.ProjectNameToAnalyse = "") then 
                        x.ProjectNameToAnalyse
                    else
                        Path.GetFileNameWithoutExtension(x.SolutionPathToAnalyse)

                ouputFilePath <- Path.Combine(x.VeraOutputPath, (sprintf "vera-result-%s-%i.xml" getReportName this.counter))
                this.counter <- this.counter + 1
            )

        // set environment
        let mutable env = Map.ofList []
        let mutable returncode = 1
        executor.ResetData()
        returncode <- executor.ExecuteCommand(x.VeraPath, x.generateCommandLineArgs(filepath), env, Environment.CurrentDirectory)
        if not(executor.GetErrorCode = ReturnCode.Ok) || returncode > 0 then
            if this.BuildEngine = null then
                Console.WriteLine("Vera: Failed")
                executor.GetStdError |> fun s -> for i in s do Console.WriteLine(i)
            else
                logger.LogWarning("Vera: Cannot Analyse: " + x.generateCommandLineArgs(filepath))
                executor.GetStdError |> fun s -> for i in s do logger.LogWarning(i)
        else
            let getVeraWarningFromLine(line:string) =
                let linerelative = line.Replace(Directory.GetParent(x.SolutionPathToAnalyse).ToString(), "")
                let elems = linerelative.Split(':')
                let file = elems.[0]
                let line = elems.[1]
                let ruleid = elems.[2].Trim().Split('(').[1].Split(')').[0]
                let message = elems.[2].Split(')').[1].Trim()
                VeraErrorX(file, line, "warning", message, ruleid)

            let addLine (line:string) =
                use wr = new StreamWriter(ouputFilePath, true)
                if x.UseRelativePaths then
                    let pathAbs = Directory.GetParent(x.SolutionPathToAnalyse).ToString() + "\\"
                    let data = line.Replace(pathAbs, "./")
                    wr.WriteLine(data)
                else
                    wr.WriteLine(line)

            if not(x.VeraOutputType = "vs7") then
                let parentdir = Directory.GetParent(ouputFilePath).ToString()
                if File.Exists(ouputFilePath) then File.Delete(ouputFilePath)
                if not(Directory.Exists(parentdir)) then Directory.CreateDirectory(parentdir) |> ignore

                let writeError(line:string) =
                    let veraelement = getVeraWarningFromLine(line)
                    let message = Utils().EscapeString(veraelement.message)
                    let error = sprintf """<error line="%s" severity="%s" message="%s" source="%s"/>""" veraelement.line veraelement.severity message veraelement.source
                    addLine(error)
                    this.totalViolations <- this.totalViolations + 1
                
                addLine("""<?xml version="1.0" encoding="UTF-8"?>""")
                addLine("""<checkstyle version="5.0">""")
                let fileNameLine = sprintf """<file name="%s">""" filepath
                addLine(fileNameLine)
                executor.GetStdError |> Seq.iter (fun x -> writeError(x))
                addLine("""</file>""")
                addLine("""</checkstyle>""")
            else
                let WriteToVsOUtput(line:string)=
                    this.totalViolations <- this.totalViolations + 1
                    let veraelement = getVeraWarningFromLine(line)
                    if this.BuildEngine = null then
                        let data = sprintf "%s : %s : %s : %s" filepath veraelement.line veraelement.severity (veraelement.message  + " " + veraelement.source)
                        Console.WriteLine(data);
                    else
                        logger.LogWarning("", veraelement.severity, "", filepath, Convert.ToInt32(veraelement.line), 0, 0, 0, veraelement.message)

                let lines = executor.GetStdError

                if lines <> List.Empty then
                    lines |> Seq.iter (fun x -> WriteToVsOUtput(x))

        true

    override x.Execute() =

        this.verifyVeraExecProperties(logger).Force()
        this.VerifyOutput(logger).Force()

        let mutable result = not(logger.HasLoggedErrors)
        if result then
            let stopWatchTotal = Stopwatch.StartNew()
            let solutionHelper = new VSSolutionUtils()
            let projectHelper = new VSProjectUtils()

            if not(Directory.Exists(x.VeraOutputPath)) then
                Directory.CreateDirectory(x.VeraOutputPath) |> ignore

            let iterateOverFiles (file : string) (projectPath : string) =
                let ignoreFiles = x.VeraIgnores.Split(";".ToCharArray())
                let projectPathDir = Directory.GetParent(projectPath).ToString()
                let mutable skip = false

                if not(file.Contains(Directory.GetParent(x.SolutionPathToAnalyse).ToString())) then
                    skip <- true

                for ignore in ignoreFiles do
                    let pathignore = Path.Combine(Directory.GetParent(x.SolutionPathToAnalyse).ToString(), ignore.Trim())
                    if Path.GetFullPath(file) = Path.GetFullPath(pathignore) then skip <- true

                let IsSupported(file : string) = 
                    file.EndsWith(".cpp") || file.EndsWith(".c") || file.EndsWith(".cc") || file.EndsWith(".h") || file.EndsWith(".hpp")


                let getReportName = 
                    if not(x.ProjectNameToAnalyse = "") then 
                        x.ProjectNameToAnalyse
                    else
                        ""

                if not(skip) && IsSupported(file) then
                    let arguments = x.generateCommandLineArgs(file)
                    logger.LogMessage(sprintf "Vera++Command: %s %s" x.VeraPath arguments)
                    x.ExecuteVera file |> ignore            
                    ()

            let iterateOverProjectFiles(projectFile : ProjectFiles) =
                if x.ProjectNameToAnalyse = "" then
                    projectHelper.GetCompilationFiles(projectFile.path, "", x.PathReplacementStrings)  |> Seq.iter (fun x -> iterateOverFiles x projectFile.path)
                elif projectFile.name.ToLower().Equals(x.ProjectNameToAnalyse.ToLower()) then
                    projectHelper.GetCompilationFiles(projectFile.path, "", x.PathReplacementStrings)  |> Seq.iter (fun x -> iterateOverFiles x projectFile.path)

            solutionHelper.GetProjectFilesFromSolutions(x.SolutionPathToAnalyse) |> PSeq.iter (fun x -> iterateOverProjectFiles x)

            logger.LogMessage(sprintf "Total Violations: %u" this.totalViolations)
            logger.LogMessage(sprintf "Vera End: %u ms" stopWatchTotal.ElapsedMilliseconds)

        if result && this.buildok then
            true
        else
            false

    interface ICancelableTask with
        member this.Cancel() =
            Environment.Exit(0)
            ()
