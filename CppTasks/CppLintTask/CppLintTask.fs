// Learn more about F# at http://fsharp.net

namespace MSBuild.Tekla.Tasks.CppLint
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
open MSBuild.Tekla.Tasks.MsbuildTaskUtils
open MsbuildTasks

type CppLintErrorX(filename:string, line:string, severity:string, message:string, id:string) =
    member val filename = filename
    member val line = line
    member val severity = severity
    member val message = message
    member val id = id

type CppLintTask(executorIn : ICommandExecutor) as this =    
    inherit Task()

    let logger : TaskLoggingHelper = new TaskLoggingHelper(this)
    let _CppLintExec = "CppLint.exe"
    let syncLock = new System.Object()

    new() = CppLintTask(null)

    member val buildok : bool = true with get, set
    member val counter : int = 0 with get, set
    member val totalViolations : int = 0 with get, set 
    member val ToOutputData : string list = [] with get, set
    member val PathReplacementStrings = "" with get, set
    member val UseRelativePaths = false with get, set

    /// Solution Path, Required
    [<Required>]
    member val SolutionPathToAnalyse = "" with get, set

    member val ProjectNameToAnalyse = "" with get, set

    /// Optional result target file. Must be unique to each test run.
    [<Required>]
    member val CppLintOutputPath = "" with get, set

    [<Required>]
    member val PythonPath = "" with get, set

    /// path for CppLint executable, default expects CppLint in path
    member val CppLintOutputType = "vs7" with get, set
    member val CppLintPath = _CppLintExec with get, set
    member val CppLintOptions = "" with get, set
    member val CppLintIgnores = "" with get, set
    member val CppLintEnvironment = "" with get, set

    /// Verify Xml output
    member x.VerifyOutput(logger  : TaskLoggingHelper) =
        lazy(
            if String.IsNullOrWhiteSpace(x.CppLintOutputType) then logger.LogError("Output Type cannot be empty, use vs7 or xml")
            elif not("xml" = x.CppLintOutputType) && not("vs7" = x.CppLintOutputType) then logger.LogError("Output Type Invalid, use vs7 or xml")
            elif "xml" = x.CppLintOutputType && String.IsNullOrWhiteSpace(x.CppLintOutputType) then logger.LogError("CppLintOutputType: Output Report Path should be defined for xml reporting")
            )

    /// Verify CppLint is found on Path
    member x.verifyCppLintExecProperties(logger  : TaskLoggingHelper) =
        lazy(
            if String.IsNullOrWhiteSpace(x.CppLintPath) then logger.LogError("CppLintPath Cannot Be Empty, Remove to use from Path")
            elif System.IO.Path.IsPathRooted(x.CppLintPath) then 
                if not(System.IO.File.Exists(x.CppLintPath)) then logger.LogError(sprintf "CppLintPath: %s Cannot Be Found on System, Set Path Correctly" x.CppLintPath)
            elif not(Utils().ExistsOnPath(x.CppLintPath)) then logger.LogError(sprintf "CppLintPath: %s Cannot Be Found on PATH, Set PATH variable Correctly" x.CppLintPath)
            )

    /// Verify Python is found on Path
    member x.verifyPythonProperties(logger  : TaskLoggingHelper) =
        lazy(
            if String.IsNullOrWhiteSpace(x.PythonPath) then logger.LogError("Python Cannot Be Empty, Remove to use from Path")
            elif System.IO.Path.IsPathRooted(x.PythonPath) then 
                if not(System.IO.File.Exists(x.PythonPath)) then logger.LogError(sprintf "PythonPath: %s Cannot Be Found on System, Set Path Correctly" x.PythonPath)
            elif not(Utils().ExistsOnPath(x.PythonPath)) then logger.LogError(sprintf "PythonPath: %s Cannot Be Found on PATH, Set PATH variable Correctly" x.PythonPath)
            elif not(System.IO.File.Exists(x.SolutionPathToAnalyse)) then logger.LogError(sprintf "SolutionPathToAnalyse: %s not found, Set SolutionDir" x.SolutionPathToAnalyse)
            )            

    member x.generateCommandLineArgs(fileToAnalyse : string)=
        let builder = new CommandLineBuilder()

        // options
        builder.AppendSwitch(x.CppLintPath)

        if not(String.IsNullOrWhiteSpace(x.CppLintOptions)) then
            let values = x.CppLintOptions.Split(";".ToCharArray())
            for value in values do
                builder.AppendSwitch(value)

        builder.AppendSwitch(fileToAnalyse)

        builder.ToString()

    member x.ExecuteCppLint filepath =
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

                ouputFilePath <- Path.Combine(x.CppLintOutputPath, (sprintf "cpplint-result-%s-%i.xml" getReportName this.counter))
                this.counter <- this.counter + 1
            )
                            
        // set environment
        let mutable env = Map.ofList []
 
        try
            let entries = x.CppLintEnvironment.Split(";".ToCharArray())
            for entry in entries do
                let key = entry.Split('=').[0]
                let value = entry.Split('=').[1]
                if Environment.GetEnvironmentVariable(key) = null then
                    env <- env.Add(key, value)
        with
        | ex -> ()

        let mutable tries = 3
        let mutable returncode = 1

        while tries > 0  && returncode > 0 do          
            if this.BuildEngine <> null then  
                logger.LogMessage(sprintf "[CPPLINT : EXECUTE %i] %s %s in report: %s" tries x.PythonPath (x.generateCommandLineArgs(filepath)) ouputFilePath)

            returncode <- executor.ExecuteCommand(x.PythonPath, x.generateCommandLineArgs(filepath), env, Environment.CurrentDirectory)
            if not(executor.GetErrorCode = ReturnCode.Ok) then
                tries <- tries - 1
            else
                returncode <- 0
                

        if tries = 0 then
            this.buildok <- false
            if this.BuildEngine = null then
                Console.WriteLine("CppLint: Number of tries exceeded")
                executor.GetStdError |> fun s -> for i in s do Console.WriteLine(i)
            else
                logger.LogError("CppLint: Number of tries exceeded")
                executor.GetStdError |> fun s -> for i in s do logger.LogError(i)

            executor.GetStdOut |> fun s -> for i in s do  if this.BuildEngine = null then Console.WriteLine(i) else logger.LogError(i)
            false
        else
            let getCppLintWarningFromLine(line:string) =
                let linerelative = line.Replace(Directory.GetParent(x.SolutionPathToAnalyse).ToString(), "")
                let elems = linerelative.Split(':')
                let elemsForId = linerelative.Split(' ')
                let file = elems.[0]
                let line = elems.[1]
                let rest = elems.[2]
                let ruleid = elemsForId.[elemsForId.Length - 2]
                let indexofruleid = linerelative.IndexOf(ruleid)
                let messagewithoutids = linerelative.Substring(0, indexofruleid)
                let message = messagewithoutids.Replace(elems.[0] + ":", "").Replace(line + ":", "").Trim()
                this.totalViolations <- this.totalViolations + 1

                if x.UseRelativePaths then
                    let pathAbs = Directory.GetParent(x.SolutionPathToAnalyse).ToString()
                    let pathFile = "./" + filepath.Replace(pathAbs, "").Substring(1).Replace("\\", "/")
                    CppLintErrorX(pathFile, line, "warning", message, ruleid.Substring(1, elemsForId.[elemsForId.Length - 2].Length - 2))
                else
                    CppLintErrorX(filepath, line, "warning", message, ruleid.Substring(1, elemsForId.[elemsForId.Length - 2].Length - 2))
                    
            let addLine (line:string) =                  
                use wr = new StreamWriter(ouputFilePath, true)
                wr.WriteLine(line)

            if not(x.CppLintOutputType = "vs7") then
                let parentdir = Directory.GetParent(ouputFilePath).ToString()
                if File.Exists(ouputFilePath) then File.Delete(ouputFilePath)
                if not(Directory.Exists(parentdir)) then Directory.CreateDirectory(parentdir) |> ignore

                let writeError(line:string) =
                    try
                        let CppLintelement = getCppLintWarningFromLine(line)
                        let message = Utils().EscapeString(CppLintelement.message.Replace("\"",""))
                        let error = sprintf """<error file="%s" line="%s" id="cpplint.%s" severity="%s" msg="%s"/>""" CppLintelement.filename CppLintelement.line CppLintelement.id CppLintelement.severity message
                        addLine(error)
                    with
                    | ex -> ()
                    
                addLine("""<?xml version="1.0" encoding="UTF-8"?>""")
                addLine("""<results>""")
                executor.GetStdError |> Seq.iter (fun x -> writeError(x))
                addLine("""</results>""")
            else
                let WriteToVsOUtput(line:string)= 
                    try
                        let CppLintelement = getCppLintWarningFromLine(line)
                        if this.BuildEngine = null then
                            let data = sprintf "%s : %s : %s : %s" filepath CppLintelement.line CppLintelement.severity (CppLintelement.message  + " " + CppLintelement.id)
                            Console.WriteLine(data);
                        else
                            logger.LogWarning("", CppLintelement.severity, "", filepath, Convert.ToInt32(CppLintelement.line), 0, 0, 0, (CppLintelement.message  + " " + CppLintelement.id))
                    with
                    | ex -> ()

                let lines = executor.GetStdError

                if lines <> List.Empty then
                    lines |> Seq.iter (fun x -> WriteToVsOUtput(x))

            true

    override x.Execute() =

        this.verifyCppLintExecProperties(logger).Force()
        this.VerifyOutput(logger).Force()
        this.verifyPythonProperties(logger).Force()

        let mutable result = not(logger.HasLoggedErrors)
        if result then
            let stopWatchTotal = Stopwatch.StartNew()
            let solutionHelper = new VSSolutionUtils()
            let projectHelper = new VSProjectUtils()

            if not(Directory.Exists(x.CppLintOutputPath)) then
                Directory.CreateDirectory(x.CppLintOutputPath) |> ignore

            let iterateOverFiles (file : string) (projectPath : string) =
                let ignoreFiles = x.CppLintIgnores.Split(";".ToCharArray())
                let projectPathDir = Directory.GetParent(projectPath).ToString()
                let mutable skip = false

                if not(file.Contains(Directory.GetParent(x.SolutionPathToAnalyse).ToString())) then
                    skip <- true

                for ignore in ignoreFiles do
                    let pathignore = Path.Combine(Directory.GetParent(x.SolutionPathToAnalyse).ToString(), ignore.Trim())
                    if Path.GetFullPath(file) = Path.GetFullPath(pathignore) then skip <- true

                if not(skip) then
                    let arguments = x.generateCommandLineArgs(file)                   
                    let extension = Path.GetExtension(file).ToLower()
                    
                    if extension.Equals(".cpp") || extension.Equals(".hpp") || extension.Equals(".c") || extension.Equals(".h") || extension.Equals(".cxx") then
                        logger.LogMessage(sprintf "[ToolExecute] CppLint Command: %s %s" x.PythonPath arguments)
                        x.ExecuteCppLint file |> ignore                    
                ()

            let iterateOverProjectFiles(projectFile : ProjectFiles) =
                projectHelper.GetCompilationFiles(projectFile.path, "", x.PathReplacementStrings)  |> Seq.iter (fun x -> iterateOverFiles x projectFile.path)

            (Array.ofSeq (solutionHelper.GetProjectFilesFromSolutions(x.SolutionPathToAnalyse))) |>  Array.Parallel.map (fun x -> iterateOverProjectFiles x) |> ignore

            logger.LogMessage(sprintf "Total Violations: %u" this.totalViolations)
            logger.LogMessage(sprintf "CppLint End: %u ms" stopWatchTotal.ElapsedMilliseconds)

        if result && this.buildok then
            true
        else
            false

    interface ICancelableTask with
        member this.Cancel() =
            Environment.Exit(0)
            ()
