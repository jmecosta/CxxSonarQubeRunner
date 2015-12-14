module SonarRunnerPhases

open MsbuildTasksCommandExecutor
open System.Diagnostics
open System
open System.IO
open System.Threading
open FSharp.Data 

type StatusAnalysis = JsonProvider<"""
{"task":{"id":"AVEC7PZCplUol9a0gqLe","type":"REPORT","componentId":"AVECef1fplUol9a0gqAc","componentKey":"tekla.structures.core:Common","componentName":"Common","componentQualifier":"TRK","status":"SUCCESS","submittedAt":"2015-11-14T00:17:42+0200","startedAt":"2015-11-14T00:17:44+0200","executedAt":"2015-11-14T00:17:48+0200","executionTimeMs":4291,"logs":true}}
""">

let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
    if not(String.IsNullOrWhiteSpace(e.Data))  then
        printf  "%s\r\n" e.Data

let RunBuild(msbuild:string, solution:string, arguments:string, buildLog:string, sonarQubeTempPath : string, homePath : string) =
    let executor = new CommandExecutor(null, int64(1500000))
    let mutable buffer = ""

    let ProcessOutputDataReceivedMSbuild(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then      
            buffer <- buffer + "\r\n"                    
            if e.Data.Contains(">Done Building Project ")  || 
                e.Data.Contains(">Project ")  ||                
                e.Data.Contains("): error")  || 
                e.Data.Contains(">Build FAILED.") then
                if e.Data.Contains("): error")  then
                    let formatedstring = sprintf "%s" e.Data
                    HelpersMethods.cprintf(ConsoleColor.Red, formatedstring)
                else
                    printf  "%s\r\n" e.Data

    let sonarQubeTempPathProp = sprintf "/p:SonarQubeTempPath=%s" sonarQubeTempPath 
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "######## Build Solution ###########")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : msbuild /m %s \r\n" (solution + " /v:diag " + sonarQubeTempPathProp + " " + arguments)))
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(msbuild, solution + " /m /v:Detailed " + sonarQubeTempPathProp + " " + arguments, Map.empty, ProcessOutputDataReceivedMSbuild, ProcessOutputDataReceivedMSbuild, homePath)

    use outFile = new StreamWriter(buildLog, false)
    outFile.Write(buffer) |> ignore

    returncode

let BeginPhase(cmd : string, arguments : string, homePath : string, userName : string, userPass : string) =
    let executor = new CommandExecutor(null, int64(1500000))
    
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########## Begin Stage  ###########")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")

    if userPass <> "" then
        HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : %s begin /d:sonar.verbose=true %s\r\n" cmd (arguments.Replace(userPass, "xxxxxx"))))
    else
        HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : %s begin /d:sonar.verbose=true %s\r\n" cmd arguments))

    let returncode = (executor :> ICommandExecutor).ExecuteCommand(cmd, "begin /d:sonar.verbose=true " + arguments, Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, homePath)
    returncode

let EndPhase(cmd : string, username : string, password : string, homePath : string) =

    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### End Stage  ############") 
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")

    let executor = new CommandExecutor(null, int64(1500000))
    let mutable idAnalysis = ""
    let mutable urlForChecking = ""
    let ProcessEndPhaseData(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data

            if e.Data.Contains("INFO  - More about the report processing at") then
                urlForChecking <- e.Data.Split([|"INFO  - More about the report processing at"|], StringSplitOptions.RemoveEmptyEntries).[1].Trim()

    let rec loopTimerCheck() =
        let mutable status = 0
        let content = HelpersMethods.GetRequest(username, password, urlForChecking)
        let response = StatusAnalysis.Parse(content)
        idAnalysis <- response.Task.Id
        if response.Task.Status.Equals("IN_PROGRESS") || response.Task.Status.Equals("PENDING") then 
            printf "STATUS: %s \r\n" response.Task.Status
            Thread.Sleep(1000)
            loopTimerCheck()
        elif response.Task.Status.Equals("SUCCESS") then
            printf "STATUS: %s \r\n" response.Task.Status
            status <- 0
        else
            let content = HelpersMethods.GetRequest(username, password, urlForChecking.Replace("task?id=" + idAnalysis, "logs?taskId=" + idAnalysis))
            HelpersMethods.cprintf(ConsoleColor.Red, (sprintf "%s" content))
            raise(new Exception("Failed to execute server analysis"))
    

    HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[EndPhase] : %s end /d:sonar.login=%s /d:sonar.password=xxxxx" cmd username))
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(cmd, "end /d:sonar.login=" + username + " /d:sonar.password=" + password, Map.empty, ProcessEndPhaseData, ProcessEndPhaseData, homePath)
    
    if returncode = 0 then
        if urlForChecking <> "" then
            printf  "[EndPhase] : Check Analysis Results in Server every 2 seconds\r\n"
            loopTimerCheck()
            0
        else
            printf  "[EndPhase] : Cannot Check Analysis Resutls in Server, available only for 5.2 or above\r"
            0
    else
        HelpersMethods.cprintf(ConsoleColor.Red, "[EndPhase] : Failed. Check Log")
        returncode        
        
