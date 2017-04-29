module SonarRunnerPhases

open MsbuildUtilityHelpers
open System.Diagnostics
open System
open System.IO
open System.Text.RegularExpressions
open System.Threading
open FSharp.Data 

open Options

type StatusAnalysis = JsonProvider<"""
{"task":{"organization":"default-organization","id":"AVtd7ZDTxV0p7xjZ0mSW","type":"REPORT","componentId":"52b3c171-c7b9-4e20-8374-545898863b0f","componentKey":"Tekla.Structures.DotApps:DotNetInterface","componentName":"DotNetInterface","componentQualifier":"TRK","analysisId":"AVtd7ZjSfRkk4M2OYRqg","status":"SUCCESS","submittedAt":"2017-04-11T19:51:42+0300","submitterLogin":"user","startedAt":"2017-04-11T19:51:43+0300","executedAt":"2017-04-11T19:56:16+0300","executionTimeMs":272925,"logs":false,"hasScannerContext":true}}
""">

type GateAnalysis = JsonProvider<"""
{
  "projectStatus": {
    "status": "ERROR",
    "conditions": [
      {
        "status": "ERROR",
        "metricKey": "new_coverage",
        "comparator": "LT",
        "periodIndex": 1,
        "errorThreshold": "85",
        "actualValue": "82.50562381034781"
      },
      {
        "status": "ERROR",
        "metricKey": "new_blocker_violations",
        "comparator": "GT",
        "periodIndex": 1,
        "errorThreshold": "0",
        "actualValue": "14"
      },
      {
        "status": "ERROR",
        "metricKey": "new_critical_violations",
        "comparator": "GT",
        "periodIndex": 1,
        "errorThreshold": "0",
        "actualValue": "1"
      },
      {
        "status": "OK",
        "metricKey": "new_sqale_debt_ratio",
        "comparator": "GT",
        "periodIndex": 2,
        "errorThreshold": "5",
        "actualValue": "0.6562109862671661"
      },
      {
        "status": "OK",
        "metricKey": "reopened_issues",
        "comparator": "GT",
        "periodIndex": 3,
        "warningThreshold": "0",
        "actualValue": "0"
      },
      {
        "status": "WARN",
        "metricKey": "open_issues",
        "comparator": "GT",
        "periodIndex": 3,
        "warningThreshold": "0",
        "actualValue": "17"
      },
      {
        "status": "OK",
        "metricKey": "skipped_tests",
        "comparator": "GT",
        "periodIndex": 5,
        "warningThreshold": "0",
        "actualValue": "0"
      }
    ],
    "periods": [
      {
        "index": 1,
        "mode": "last_period",
        "date": "2000-04-27T00:45:23+0200"
      },
      {
        "index": 2,
        "mode": "last_version",
        "date": "2000-04-27T00:45:23+0200",
        "parameter": "2015-12-07"
      },
      {
        "index": 3,
        "mode": "last_analysis"
      },
      {
        "index": 5,
        "mode": "last_30_days",
        "parameter": "2015-11-07"
      }
    ]
  }
}
""">

let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
    if not(String.IsNullOrWhiteSpace(e.Data))  then
        printf  "%s\r\n" e.Data

let flavours = [ "Enterprise"; "Community"; "Professional" ]

let EnvForBuild(vsVersion : string, useAmd64 : bool) = 
    let buildEnvironmentPlatform =
        if useAmd64 then
            "amd64"
        else
            ""
    let buildEnvironmentBatFile =
        if vsVersion = "vs10" then 
            if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio 10.0\\VC\\vcvarsall.bat") then
                "C:\\Program Files (x86)\\Microsoft Visual Studio 10.0\\VC\\vcvarsall.bat"
            else
                "C:\\Program Files\\Microsoft Visual Studio 10.0\\VC\\vcvarsall.bat"

        elif vsVersion = "vs12" then
            if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat") then
                "C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat"
            else
                "C:\\Program Files\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat"

        elif vsVersion = "vs13" then 
            if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat") then
                "C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat"
            else
                "C:\\Program Files\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat"
        elif vsVersion = "vs15" then
            if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat") then
                "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat"
            else
                "C:\\Program Files\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat"
        elif vsVersion = "vs17" then
            let mutable ret = ""
            for flavour in flavours do 
                if ret = "" then 
                    if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat") then
                        ret <- "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat"
                    elif File.Exists("C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat") then
                        ret <- "C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat"
            ret
        else
            if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat") then
                "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat"
            else
                "C:\\Program Files\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat"

    let processOutputDataReceived(e : DataReceivedEventArgs) =
        if not(String.IsNullOrWhiteSpace(e.Data)) then
            System.Diagnostics.Debug.WriteLine(e.Data)
        ()


    printf "[CxxSonarQubeMsbuidRunner] Capture Environment For Build cmd.exe /c \"%s\" && set \r\n" (buildEnvironmentBatFile + " " + buildEnvironmentPlatform)
    
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
    if vccompiler.Equals("vs12") then

        HelpersMethods.cprintf(ConsoleColor.DarkRed, "")
        HelpersMethods.cprintf(ConsoleColor.DarkRed, "######## Analysis will not be possible unless vs 2013 community or above is installed ###########")
        HelpersMethods.cprintf(ConsoleColor.DarkRed, "")

        let compilermsbuild = 
            if useMSBuild64 then
                if File.Exists(@"C:\Program Files (x86)\MSBuild\12.0\Bin\amd64\MSBuild.exe") then
                    @"C:\Program Files (x86)\MSBuild\12.0\Bin\amd64\MSBuild.exe"
                else
                    @"C:\Program Files\MSBuild\12.0\Bin\amd64\MSBuild.exe"
            else                
                if File.Exists(@"C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe") then
                    @"C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe"
                else
                    @"C:\Program Files\MSBuild\12.0\Bin\MSBuild.exe"

        if not(File.Exists(compilermsbuild)) then
            HelpersMethods.cprintf(ConsoleColor.DarkRed, "######## Analysis will failed, msbuild version not found  : " + compilermsbuild + " ##########")
            HelpersMethods.cprintf(ConsoleColor.DarkRed, "")

        compilermsbuild

    elif vccompiler.Equals("vs13") then
        if useMSBuild64 then
            if File.Exists(@"C:\Program Files (x86)\MSBuild\12.0\Bin\amd64\MSBuild.exe") then
                @"C:\Program Files (x86)\MSBuild\12.0\Bin\amd64\MSBuild.exe"
            else
                @"C:\Program Files\MSBuild\12.0\Bin\amd64\MSBuild.exe"
        else                
            if File.Exists(@"C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe") then
                @"C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe"
            else
                @"C:\Program Files\MSBuild\12.0\Bin\MSBuild.exe"

    elif vccompiler.Equals("vs15") then 
        if useMSBuild64 then
            if File.Exists(@"C:\Program Files (x86)\MSBuild\14.0\Bin\amd64\MSBuild.exe") then
                @"C:\Program Files (x86)\MSBuild\14.0\Bin\amd64\MSBuild.exe"
            else
                @"C:\Program Files\MSBuild\14.0\Bin\amd64\MSBuild.exe"
        else                
            if File.Exists(@"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe") then
                @"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
            else
                @"C:\Program Files\MSBuild\14.0\Bin\MSBuild.exe"

    elif vccompiler.Equals("vs17") then 
        let mutable ret = ""
        if useMSBuild64 then
            for flavour in flavours do 
                if ret = "" then 
                    if File.Exists(@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe") then
                        ret <- @"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe"
                    elif File.Exists(@"C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe") then
                        ret <- @"C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe"
        else                
            for flavour in flavours do 
                if ret = "" then 
                    if File.Exists(@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe") then
                        ret <- @"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe"
                    elif File.Exists(@"C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe") then
                        ret <- @"C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe"
        ret
    else
        if useMSBuild64 then
            @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"
        else
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe"


let RunBuild(options : OptionsData) =
    let arguments = options.PropsForMsbuild + " " + options.Target + " /p:VeraTaskEnabled=true /p:RatsTaskEnabled=true /p:CppLintTaskEnabled=true /p:CppCheckTaskEnabled=true "

    let executor = new CommandExecutor(null, int64(1500000))
    let mutable buffer = ""
    let msbuildexec = "\"" + GetMsbuildExec(options.VsVersion, (options.UseAmd64 = "amd64")) + "\""
    let environment = EnvForBuild(options.VsVersion, (options.UseAmd64 = "amd64"))

    let sonarQubeTempPathProp = sprintf "/p:SonarQubeTempPath=\"%s\"" options.SonarQubeTempPath 
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "######## Build Solution ###########")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : %s %s \r\n" msbuildexec ("\"" + options.Solution + "\" /v:Detailed " + sonarQubeTempPathProp + " " + arguments + " /l:FileLogger,Microsoft.Build.Engine;logfile=" + options.BuildLog + ";Encoding=UTF-8 /noconsolelogger " + options.ParallelMsbuildOption)))

    let currentprocess = (executor :> ICommandExecutor).GetProcessIdsRunning("msbuild")

    printf "[CxxSonarQubeMsbuidRunner] The following msbuild processes have been found: \r\n"
    for pro in currentprocess do
        printf "%s" (sprintf "%s : %s\r\n" (pro.Id.ToString()) pro.ProcessName)

    let returncode = (executor :> ICommandExecutor).ExecuteCommand(msbuildexec, "\"" + options.Solution + "\" /v:Detailed " + sonarQubeTempPathProp + " " + arguments + " /l:FileLogger,Microsoft.Build.Engine;logfile=" + options.BuildLog + ";Encoding=UTF-8 /noconsolelogger " + options.ParallelMsbuildOption, environment, options.HomePath)

    let newcurrentprocess = (executor :> ICommandExecutor).GetProcessIdsRunning("msbuild")

    printf "[CxxSonarQubeMsbuidRunner] The following msbuild processes have been found after running analysis: \r\n"
    for pro in newcurrentprocess do
        printf "%s" (sprintf "%s : %s\r\n" (pro.Id.ToString()) pro.ProcessName)

    for processdata in newcurrentprocess do
        let wasrunning = currentprocess |> Seq.tryFind (fun c -> processdata.Id = c.Id)
        match wasrunning with
        | Some c -> ()
        | _ -> 
            try
                printf "Will Kill : %s" (sprintf "%s : %s\r\n" (processdata.Id.ToString()) processdata.ProcessName)
                processdata.Kill()
                processdata.WaitForExit(2000) |> ignore
            with
            | ex -> printf "[CxxSonarQubeMsbuidRunner] unable to kill msbuild, compilation might not be possible later"

    let afterkillproc = (executor :> ICommandExecutor).GetProcessIdsRunning("msbuild")
    printf "[CxxSonarQubeMsbuidRunner] The following msbuild processes have been found after cleaning up: \r\n"
    for pro in afterkillproc do
        printf "%s" (sprintf "%s : %s\r\n" (pro.Id.ToString()) pro.ProcessName)

    if returncode <> 0 then
        let lines = File.ReadAllLines(options.BuildLog)
        for i in (lines.Length - 100) .. (lines.Length - 1) do
            printf "%s \r\n" lines.[i]

    returncode

let BeginPhase(options : OptionsData) =
    let arguments = options.ProjectKey + " " + options.ProjectName + " " + options.ProjectVersion + " " + options.PropsForBeginStage + " " + "/s:\"" + options.ConfigFile + "\""
    let executor = new CommandExecutor(null, int64(1500000))
    let hostUrl =
        if options.SonarHost = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "url not specified. using default: http://localhost:9000")
            "http://localhost:9000"
        else
            options.SonarHost

    let userName = 
        if options.SonarUserName = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "login not specified. using default: admin")
            "admin"
        else
            options.SonarUserName

    let userPass = 
        if options.SonarUserPassword = "" then
            ""
        else
            " /d:sonar.password=" + options.SonarUserPassword

    let branchtopass = 
        if options.Branch = "" then
            ""
        else
            "/d:sonar.branch=" + options.Branch
    
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########## Begin Stage  ###########")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")

    if options.IsVerboseOn then
        HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : %s begin /d:sonar.verbose=true /d:sonar.host.url=%s %s %s\r\n" options.MSBuildRunnerPath hostUrl arguments branchtopass))
        (executor :> ICommandExecutor).ExecuteCommand(options.MSBuildRunnerPath, "begin /d:sonar.verbose=true " + "/d:sonar.host.url=" + hostUrl + " /d:sonar.login=" + userName + userPass + " " + arguments + " " + branchtopass, Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, options.HomePath)
    else
        HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[Execute] : %s begin /d:sonar.host.url=%s %s %s\r\n" options.MSBuildRunnerPath hostUrl arguments branchtopass))
        (executor :> ICommandExecutor).ExecuteCommand(options.MSBuildRunnerPath, "begin " + "/d:sonar.host.url=" + hostUrl + " /d:sonar.login=" + userName + userPass + " " + arguments + " " + branchtopass, Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, options.HomePath)
    
let EndPhase(options : OptionsData) =

    let hostUrl =
        if options.SonarHost = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "url not specified. using default: http://localhost:9000")
            "http://localhost:9000"
        else
            options.SonarHost

    let username = 
        if options.SonarUserName = "" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "login not specified. using default: admin")
            "admin"
        else
            options.SonarUserName

    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### End Stage  ############") 
    HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")

    let executor = new CommandExecutor(null, int64(1500000))
    let mutable idAnalysis = ""
    let mutable urlForChecking = ""
    
    let ProcessEndPhaseData(e : DataReceivedEventArgs) =         
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data
            if e.Data.Contains("EXECUTION FAILURE") then 
                printf "##teamcity[buildProblem description='Analysis Failed: Check Build Log']]\r\n"

            if e.Data.Contains("More about the report processing at") then
                urlForChecking <- e.Data.Split([|"More about the report processing at"|], StringSplitOptions.RemoveEmptyEntries).[1].Trim()

    let rec loopTimerCheck() =
        let content = HelpersMethods.GetRequest(username, options.SonarUserPassword, urlForChecking)
        let response = StatusAnalysis.Parse(content)
        idAnalysis <- response.Task.Id
        if response.Task.Status.Equals("IN_PROGRESS") || response.Task.Status.Equals("PENDING") then 
            printf "STATUS: %s \r\n" response.Task.Status
            Thread.Sleep(1000)
            loopTimerCheck()
        elif response.Task.Status.Equals("SUCCESS") then
            printf "CE STATUS: %s \r\n" response.Task.Status
            
            let mutable gateurl = ""
            let content = 
                try
                    printf "CHECK GATE ON 5.3: %s \r\n" response.Task.Status
                    let mutable gateurl = options.SonarHost + "/api/qualitygates/project_status?analysisId=" + response.Task.AnalysisId
                    HelpersMethods.GetRequest(username, options.SonarUserPassword, gateurl)
                
                with
                | ex -> printf "GATE CHECK ONLY ON 5.3 or ABOVE: %s \r\n" ex.Message
                        ""

            if content <> "" then
                let response = GateAnalysis.Parse(content)

                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Gate Check  ###########") 
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "###################################")
                
                printf "\r\n"

                if response.ProjectStatus.Status = "ERROR" then
                    HelpersMethods.cprintf(ConsoleColor.Red, (sprintf "STATUS: %s \r\n" response.ProjectStatus.Status))
                    if options.FailOnFailedGate then
                        printf "##teamcity[buildProblem description='Gate Failing']]\r\n"

                elif response.ProjectStatus.Status = "WARN" then
                    HelpersMethods.cprintf(ConsoleColor.Yellow, (sprintf "STATUS: %s \r\n" response.ProjectStatus.Status))
                elif response.ProjectStatus.Status = "OK" then
                    HelpersMethods.cprintf(ConsoleColor.Green, (sprintf "STATUS: %s \r\n" response.ProjectStatus.Status))
                else
                    ()

                if response.ProjectStatus.Status = "NONE" then
                    printf "GATE NOT DEFINED FOR PROJECT\r\n"
                else

                    for condition in response.ProjectStatus.Conditions do
                        let perioddata =
                            try
                                sprintf "=> During Period %s" (condition.PeriodIndex.ToString())
                            with
                            | _ -> ""

                        printf "\r\n"
                        printf "%s %s %s   %s \r\n" condition.MetricKey condition.Comparator (condition.ErrorThreshold.ToString()) perioddata
                        printf "    Status : %s\r\n" condition.Status
                        printf "    Actual Value : %s\r\n" (condition.ActualValue.ToString())

                printf "\r\n"
                if response.ProjectStatus.Status = "ERROR" then
                    HelpersMethods.cprintf(ConsoleColor.Red, (sprintf "STATUS: %s Project did not pass the defined quality gate.\r\n" response.ProjectStatus.Status))
                    if options.FailOnFailedGate then
                        printf "##teamcity[buildProblem description='Gate Failing']]\r\n"
                        raise(new Exception("Project did not pass the defined quality gate."))
        else
            let content = HelpersMethods.GetRequest(username, options.SonarUserPassword, urlForChecking.Replace("task?id=" + idAnalysis, "logs?taskId=" + idAnalysis))
            HelpersMethods.cprintf(ConsoleColor.Red, (sprintf "%s" content))
            raise(new Exception("Failed to execute server analysis"))
    
    let password = 
        if options.SonarUserPassword = "" then
            ""
        else
            " /d:sonar.password=" + options.SonarUserPassword

    HelpersMethods.cprintf(ConsoleColor.Blue, (sprintf "[EndPhase] : %s end" options.MSBuildRunnerPath))
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(options.MSBuildRunnerPath, "end /d:sonar.login=" + username + password, Map.empty, ProcessEndPhaseData, ProcessEndPhaseData, options.HomePath)
    
    if returncode = 0 then
        if urlForChecking <> "" then
            printf  "[EndPhase] : Check Analysis Results in Server every 2 seconds\r\n"
            loopTimerCheck()
            0
        else
            printf  "[EndPhase] : Cannot Check Analysis results in Server, available only for 5.2 or above\r"
            0
    else
        HelpersMethods.cprintf(ConsoleColor.Red, "[EndPhase] : Failed. Check Log")
        printf "##teamcity[buildProblem description='sonar-scanner return non 0 error code']]\r\n"
        1
