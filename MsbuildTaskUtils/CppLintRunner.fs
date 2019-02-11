module CppLintRunner

open MsbuildUtilityHelpers
open System.IO
open System
open Microsoft.Build.Utilities

let syncLock = new System.Object()
let mutable counter = 0
let mutable totalViolations = 0

type CppLintErrorX(filename:string, line:string, severity:string, message:string, id:string) =
    member val filename = filename
    member val line = line
    member val severity = severity
    member val message = message
    member val id = id

let generateCommandLineArgs(fileToAnalyse : string, cpplintPath:string, cpplintOptions:string)=
    let builder = new CommandLineBuilder()

    // options
    builder.AppendSwitch(cpplintPath)

    if not(String.IsNullOrWhiteSpace(cpplintOptions)) then
        let values = cpplintOptions.Split(";".ToCharArray())
        for value in values do
            builder.AppendSwitch(value)

    builder.AppendSwitch("\"" + fileToAnalyse + "\"")

    builder.ToString()

let ExecuteCppLint(executor:ICommandExecutor,
                    projectRoot:string,
                    filepath:string,
                    reportsOutPutPath:string,
                    envForCppLint:string,
                    pythonPath:string,
                    cpplintPath:string,
                    cpplintOptions:string,
                    logger:ICheckerLogger, isDebug:bool) =
    let mutable ouputFilePath = ""
        
    lock syncLock (
        fun () -> 
            ouputFilePath <- Path.Combine(reportsOutPutPath, (sprintf "cpplint-result-%i.xml" counter))
            counter <- counter + 1
        )

    // set environment
    let mutable env = Map.ofList []
 
    try
        let entries = envForCppLint.Split(";".ToCharArray())
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
        if isDebug then
            logger.ReportMessage(sprintf "[CPPLINT : EXECUTE %i] %s %s in report: %s" tries pythonPath (generateCommandLineArgs(filepath, cpplintPath, cpplintOptions)) ouputFilePath)

        returncode <- executor.ExecuteCommand(pythonPath, generateCommandLineArgs(filepath, cpplintPath, cpplintOptions), env, Environment.CurrentDirectory)
        if not(executor.GetErrorCode = ReturnCode.Ok) then
            tries <- tries - 1
        else
            returncode <- 0
                

    if tries = 0 then
        logger.ReportMessage("CppLint: Number of tries exceeded")
        executor.GetStdError |> fun s -> for i in s do logger.ReportMessage(i)
        executor.GetStdOut |> fun s -> for i in s do  logger.ReportMessage(i)
        false
    else
        let getCppLintWarningFromLine(line:string) =
            let linerelative = line.Replace(projectRoot, "")
            let elems = linerelative.Split(':')
            let elemsForId = linerelative.Split(' ')
            let file = elems.[0]
            let line = elems.[1]
            let rest = elems.[2]
            let ruleid = elemsForId.[elemsForId.Length - 2]
            let indexofruleid = linerelative.IndexOf(ruleid)
            let messagewithoutids = linerelative.Substring(0, indexofruleid)
            let message = messagewithoutids.Replace(elems.[0] + ":", "").Replace(line + ":", "").Trim()
            totalViolations <- totalViolations + 1
            CppLintErrorX(filepath, line, "warning", message, ruleid.Substring(1, elemsForId.[elemsForId.Length - 2].Length - 2))
                    
        let addLine (line:string) =
            use wr = new StreamWriter(ouputFilePath, true)
            wr.WriteLine(line)

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
        true