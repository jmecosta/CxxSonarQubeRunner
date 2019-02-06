module RatsRunner

open MsbuildUtilityHelpers
open FSharp.Data
open Microsoft.Build.Utilities
open System
open System.IO

type RatsError = XmlProvider<"""<?xml version="1.0"?><rats_output>
<stats>
<dbcount lang="perl">33</dbcount>
<dbcount lang="ruby">46</dbcount>
<dbcount lang="python">62</dbcount>
<dbcount lang="c">334</dbcount>
<dbcount lang="php">55</dbcount>
</stats>
<analyzed>E:\TSSRC\Core\Common\libtools\test\tool_base64_test.cpp</analyzed>
<vulnerability>
  <severity>High</severity>
  <type>fixed size global buffer</type>
  <message>
    Extra care should be taken to ensure that character arrays that are
    allocated on the stack are used safely.  They are prime targets for
    buffer overflow attacks.
  </message>
  <file>
    <name>E:\TSSRC\Core\Common\libtools\test\tool_base64_test.cpp</name>
    <line>114</line>
    <line>115</line>
    <line>116</line>
  </file>
</vulnerability>
<vulnerability>
  <severity>Medium</severity>
  <type>srand</type>
  <message>
    Standard random number generators should not be used to 
generate randomness used for security reasons.  For security sensitive 
randomness a crytographic randomness generator that provides sufficient
entropy should be used.
  </message>
  <file>
    <name>E:\TSSRC\Core\Common\libtools\test\tool_base64_test.cpp</name>
    <line>121</line>
  </file>
</vulnerability>
<timing>
<total_lines>141</total_lines>
<total_time>0.000000</total_time>
<lines_per_second>-2147483648</lines_per_second>
</timing>
</rats_output>""">

let syncLock = new System.Object()
let mutable counter = 0

let generateCommandLineArgs(fileToAnalyse : string, ratsOptions:string)=
    let builder = new CommandLineBuilder()

    builder.AppendSwitch("--xml")

    // options
    if not(String.IsNullOrWhiteSpace(ratsOptions)) then
        let values = ratsOptions.Split(";".ToCharArray())
        for value in values do
            builder.AppendSwitch(value.Trim())

    builder.AppendSwitch("\"" + fileToAnalyse + "\"")

    builder.ToString()

let ExecuteRats(executor:ICommandExecutor,
                ratsPath:string,
                ratsOutputPath:string,
                fileToAnalyse:string,
                ratsOptions:string,
                logger:ICheckerLogger, isDebug:bool)=

    let mutable ouputFilePath = ""

    lock syncLock (
        fun () -> 
            ouputFilePath <- Path.Combine(ratsOutputPath, (sprintf "rats-result-%i.xml" counter))
            counter <- counter + 1
        )

    // set environment
    let env = Map.ofList [("RATS_INPUT", ratsPath)]

    let mutable tries = 3
    let mutable returncode = 1

    let cmdLineArgs = generateCommandLineArgs(fileToAnalyse, ratsOptions)

    while tries > 0  && returncode > 0 do
        executor.ResetData()
        if isDebug then
            logger.ReportMessage(sprintf "[RatsCommand] %s %s" ratsPath cmdLineArgs)

        returncode <- executor.ExecuteCommand(ratsPath, cmdLineArgs, env, Environment.CurrentDirectory)
        if not(executor.GetErrorCode = ReturnCode.Ok) || returncode > 0 then
            tries <- tries - 1

    if tries = 0 then
        logger.ReportMessage("Rats: Number of tries exceeded: Cannot Analyse: ")
        executor.GetStdError |> fun s -> for i in s do logger.ReportMessage(i)
        executor.GetStdOut |> fun s -> for i in s do logger.ReportMessage(i)
        false
    else
        let parentdir = Directory.GetParent(ouputFilePath).ToString()
        if File.Exists(ouputFilePath) then File.Delete(ouputFilePath)
        if not(Directory.Exists(parentdir)) then Directory.CreateDirectory(parentdir) |> ignore

        let addLine (line:string) =
            use wr = new StreamWriter(ouputFilePath, true)
            wr.WriteLine(line)

        executor.GetStdOut |> Seq.iter (fun x -> addLine(x))
        true
