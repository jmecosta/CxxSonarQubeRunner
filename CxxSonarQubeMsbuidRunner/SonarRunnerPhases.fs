module SonarRunnerPhases

open MsbuildTasksCommandExecutor
open System.Diagnostics
open System
open System.IO
open System.Text.RegularExpressions
open System.Threading
open FSharp.Data 

type StatusAnalysis = JsonProvider<"""
{"task":{"id":"AVEC7PZCplUol9a0gqLe","type":"REPORT","componentId":"AVECef1fplUol9a0gqAc","componentKey":"tekla.structures.core:Common","componentName":"Common","componentQualifier":"TRK","status":"SUCCESS","submittedAt":"2015-11-14T00:17:42+0200","startedAt":"2015-11-14T00:17:44+0200","executedAt":"2015-11-14T00:17:48+0200","executionTimeMs":4291,"logs":true}}
""">

let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
    if not(String.IsNullOrWhiteSpace(e.Data))  then
        printf  "%s\r\n" e.Data

let EnvForBuild(vsVersion : string, useAmd64 : bool) = 
    let buildEnvironmentPlatform =
        if useAmd64 then
            "amd64"
        else
            ""
    let buildEnvironmentBatFile =
        if vsVersion = "vs10" then "C:\\Program Files (x86)\\Microsoft Visual Studio 10.0\\VC\\vcvarsall.bat"
        elif vsVersion = "vs12" then "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\vcvarsall.bat"
        elif vsVersion = "vs13" then "C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat"
        elif vsVersion = "vs15" then "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat"
        else "C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat"

    let processOutputDataReceived(e : DataReceivedEventArgs) =
        if not(String.IsNullOrWhiteSpace(e.Data)) then
            System.Diagnostics.Debug.WriteLine(e.Data)
        ()


    let startInfo = ProcessStartInfo(FileName = "cmd.exe",
                                        Arguments = "/c \"" + buildEnvironmentBatFile + "\" " + buildEnvironmentPlatform + " && set",
                                        WindowStyle = ProcessWindowStyle.Normal,
                                        UseShellExecute = false,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        RedirectStandardInput = true,
                                        CreateNoWindow = true)

    let proc = new Process(StartInfo = startInfo)
    proc.Start() |> ignore

    let mutable map = Map.empty
    let output = proc.StandardOutput.ReadToEnd()
    for line in Regex.Split(output, "\r\n") do
        if line <> "" then
            let data = line.Split('=')
            if data.Length = 2 then
                map <- map.Add(data.[0], data.[1])

    map

let GetMsbuildExec(vccompiler : string, useMSBuild64 : bool) =
    if vccompiler.Equals("vs11") then
        if useMSBuild64 then
            @"C:\Program Files (x86)\MSBuild\11.0\Bin\amd64\MSBuild.exe"
        else
            @"C:\Program Files (x86)\MSBuild\11.0\Bin\MSBuild.exe"
    elif vccompiler.Equals("vs13") then
        if useMSBuild64 then
            @"C:\Program Files (x86)\MSBuild\12.0\Bin\amd64\MSBuild.exe"
        else
            @"C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe"
    elif vccompiler.Equals("vs15") then 
        if useMSBuild64 then
            @"C:\Program Files (x86)\MSBuild\14.0\Bin\amd64\MSBuild.exe"
        else
            @"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
    else
        @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"

let RunBuild(msbuildversion:string, useamd64:string, solution:string, arguments:string, buildLog:string, sonarQubeTempPath : string, homePath : string) =
    let executor = new CommandExecutor(null, int64(1500000))
    let mutable buffer = ""
    let msbuildexec = GetMsbuildExec(msbuildversion, (useamd64 = "amd64"))
    let environment = EnvForBuild(msbuildversion, (useamd64 = "amd64"))

    let ProcessOutputDataReceivedMSbuild(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then      
            buffer <- buffer + e.Data + "\r\n"                    
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
    HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : %s /m %s \r\n" msbuildexec (solution + " /v:diag " + sonarQubeTempPathProp + " " + arguments)))

    let currentprocess = (executor :> ICommandExecutor).GetProcessIdsRunning("msbuild")
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(msbuildexec, solution + " /m /v:Detailed " + sonarQubeTempPathProp + " " + arguments, environment, ProcessOutputDataReceivedMSbuild, ProcessOutputDataReceivedMSbuild, homePath)

    let newcurrentprocess = (executor :> ICommandExecutor).GetProcessIdsRunning("msbuild")

    for processdata in newcurrentprocess do
        let wasrunning = currentprocess |> Seq.tryFind (fun c -> processdata.Id = c.Id)
        match wasrunning with
        | Some c -> ()
        | _ -> processdata.Kill()

    use outFile = new StreamWriter(buildLog, false)
    outFile.Write(buffer) |> ignore

    returncode

let BeginPhase(cmd : string, arguments : string, homePath : string, userNameIn : string, userPassIn : string, hostUrlIn : string) =
    let executor = new CommandExecutor(null, int64(1500000))
    let hostUrl =
        if hostUrlIn = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "url not specified. using default: http://localhost:9000")
            "http://localhost:9000"
        else
            hostUrlIn

    let userName = 
        if userNameIn = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "login not specified. using default: admin")
            "admin"
        else
            userNameIn

    let userPass = 
        if userPassIn = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "password not specified. using default: admin")
            "admin"
        else
            userPassIn
    
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########## Begin Stage  ###########")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : %s begin /d:sonar.verbose=true /d:sonar.host.url=%s /d:sonar.login=%s /d:sonar.password=xxxxx %s\r\n" cmd hostUrl userName arguments))

    let returncode = (executor :> ICommandExecutor).ExecuteCommand(cmd, "begin /d:sonar.verbose=true " + "/d:sonar.host.url=" + hostUrl + " /d:sonar.login=" + userName + " /d:sonar.password=" + userPass + " " + arguments, Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, homePath)
    returncode

let EndPhase(cmd : string, usernamein : string, passwordin : string, homePath : string, hostUrlIn : string) =

    let hostUrl =
        if hostUrlIn = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "url not specified. using default: http://localhost:9000")
            "http://localhost:9000"
        else
            hostUrlIn

    let username = 
        if usernamein = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "login not specified. using default: admin")
            "admin"
        else
            usernamein

    let password = 
        if passwordin = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "password not specified. using default: admin")
            "admin"
        else
            passwordin

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
    

    HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[EndPhase] : %s end /d:sonar.login=%s /d:sonar.password=xxxxx /d:sonar.host.url=%s" cmd username hostUrl))
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
        
