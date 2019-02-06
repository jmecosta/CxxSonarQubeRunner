module VeraRunner

open Microsoft.Build.Utilities
open System
open MsbuildUtilityHelpers
open System.IO

type VeraErrorX(filename:string, line:string, severity:string, message:string, source:string) =
    member val filename = filename
    member val line = line
    member val severity = severity
    member val message = message
    member val source = source

let syncLock = new System.Object()
let mutable counter = 0

let generateCommandLineArgs(fileToAnalyse:string, veraOptions:string)=
    let builder = new CommandLineBuilder()

    // options
    if not(String.IsNullOrWhiteSpace(veraOptions)) then
        let values = veraOptions.Split(";".ToCharArray())
        for value in values do
            builder.AppendSwitch(value)

    builder.AppendSwitch("\"" + fileToAnalyse + "\"")
    builder.ToString()

let ExecuteVera(executor:ICommandExecutor, filepath:string, verPath:string, veraOutputPath:string, veraOptions:string, projectRoot:string, logger:ICheckerLogger, isDebug:bool) =
    let mutable ouputFilePath = ""
        
    lock syncLock (
        fun () -> 
            ouputFilePath <- Path.Combine(veraOutputPath, (sprintf "vera-result-%i.xml" counter))
            counter <- counter + 1
        )

    // set environment
    let mutable env = Map.ofList []
    let mutable returncode = 1
    executor.ResetData()
    if isDebug then
        logger.ReportMessage(sprintf "[VeraCommand:] %s %s" verPath (generateCommandLineArgs(filepath, veraOptions)))

    returncode <- executor.ExecuteCommand(verPath, generateCommandLineArgs(filepath, veraOptions), env, Environment.CurrentDirectory)
    if not(executor.GetErrorCode = ReturnCode.Ok) || returncode > 0 then
        logger.ReportMessage("Vera: Cannot Analyse: " + generateCommandLineArgs(filepath, veraOptions))
        executor.GetStdError |> fun s -> for i in s do logger.ReportMessage(i)
    else
        let getVeraWarningFromLine(line:string) =
            let linerelative = line.Replace(projectRoot, "")
            let elems = linerelative.Split(':')
            let file = elems.[0]
            let line = elems.[1]
            let ruleid = elems.[2].Trim().Split('(').[1].Split(')').[0]
            let message = elems.[2].Split(')').[1].Trim()
            VeraErrorX(file, line, "warning", message, ruleid)

        let addLine (line:string) =
            use wr = new StreamWriter(ouputFilePath, true)
            wr.WriteLine(line)

        let parentdir = Directory.GetParent(ouputFilePath).ToString()
        if File.Exists(ouputFilePath) then File.Delete(ouputFilePath)
        if not(Directory.Exists(parentdir)) then Directory.CreateDirectory(parentdir) |> ignore

        let writeError(line:string) =
            let veraelement = getVeraWarningFromLine(line)
            let message = Utils().EscapeString(veraelement.message)
            let error = sprintf """<error line="%s" severity="%s" message="%s" source="%s"/>""" veraelement.line veraelement.severity message veraelement.source
            addLine(error)
                
        addLine("""<?xml version="1.0" encoding="UTF-8"?>""")
        addLine("""<checkstyle version="5.0">""")
        let fileNameLine = sprintf """<file name="%s">""" filepath
        addLine(fileNameLine)
        executor.GetStdError |> Seq.iter (fun x -> writeError(x))
        addLine("""</file>""")
        addLine("""</checkstyle>""")


    true