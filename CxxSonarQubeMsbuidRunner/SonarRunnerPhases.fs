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
    use outFile = new StreamWriter(buildLog, false)

    let ProcessOutputDataReceivedMSbuild(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data
            outFile.WriteLine(e.Data)

    let sonarQubeTempPathProp = sprintf "/p:SonarQubeTempPath=%s" sonarQubeTempPath 
    printf  "[Execute] : msbuild %s \r\n" (solution + " /v:diag " + sonarQubeTempPathProp + " " + arguments)    
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(msbuild, solution + " /v:Detailed " + sonarQubeTempPathProp + " " + arguments, Map.empty, ProcessOutputDataReceivedMSbuild, ProcessOutputDataReceivedMSbuild, homePath)
    returncode

let BeginPhase(cmd : string, arguments : string, homePath : string, userName : string, userPass : string) =
    let executor = new CommandExecutor(null, int64(1500000))
    
    printf  "[Execute] : %s begin /d:sonar.verbose=true %s\r\n" cmd (arguments.Replace(userPass, "xxxxxx"))
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(cmd, "begin /d:sonar.verbose=true " + arguments, Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, homePath)
    returncode

let EndPhase(cmd : string, username : string, password : string, homePath : string) =
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
            printf "%s" content
            raise(new Exception("Failed to execute server analysis"))
    

    printf  "[EndPhase] : %s end /d:sonar.login=%s /d:sonar.password=xxxxx\r\n" cmd username
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(cmd, "end /d:sonar.login=" + username + " /d:sonar.password=" + password, Map.empty, ProcessEndPhaseData, ProcessEndPhaseData, homePath)
    
    if returncode = 0 then
        if urlForChecking <> "" then
            printf  "[EndPhase] : Check Analysis Results in Server every 2 seconds\r\n"
            loopTimerCheck()
            0
        else
            printf  "[EndPhase] : Cannot Check Analysis Resutls in Server, available only for 5.2 or above\r\n"
            0
    else
        printf  "[EndPhase] : Failed. Check Log\r\n"
        returncode        
        
