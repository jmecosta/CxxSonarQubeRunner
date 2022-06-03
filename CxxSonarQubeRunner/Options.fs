module Options

open System
open System.IO
open System.Net
open System.Text.RegularExpressions
open System.Threading
open FSharp.Data
open InstallationModule
open MsbuildUtilityHelpers
open SonarRestService
open SonarRestServiceImpl
open SonarRestService.Types
open System.Diagnostics

type NotificationManager() =
    interface ICheckerLogger with
        member this.ReportMessage(message:string) = HelpersMethods.cprintf(ConsoleColor.Cyan, message)
        member this.ReportException(ex:Exception) = HelpersMethods.cprintf(ConsoleColor.Red, ex.Message)
                                                    HelpersMethods.cprintf(ConsoleColor.Red, ex.StackTrace)
    interface IRestLogger with
        member this.ReportMessage(message:string) = HelpersMethods.cprintf(ConsoleColor.Cyan, message)

let (|Command|_|) (s:string) =
    let r = new Regex(@"^(?:-{1,2}|\/)(?<command>\w+)[=:]*(?<value>.*)$",RegexOptions.IgnoreCase)
    let m = r.Match(s)
    if m.Success then 
        Some(m.Groups.["command"].Value.ToLower(), m.Groups.["value"].Value)
    else
        None

let parseArgs (args:string seq) =
    args 
    |> Seq.map (fun i -> 
                        match i with
                        | Command (n,v) -> (n,v) // command
                        | _ -> ("",i)            // data
                       )
    |> Seq.scan (fun (sn,_) (n,v) -> if n.Length>0 then (n,v) else (sn,v)) ("","")
    |> Seq.skip 1
    |> Seq.groupBy (fun (n,_) -> n)
    |> Seq.map (fun (n,s) -> (n, s |> Seq.map (fun (_,v) -> v) |> Seq.filter (fun i -> i.Length>0)))
    |> Map.ofSeq


let GetPropertyFromFile(content : string [], prop : string) =
    let data = content |> Seq.tryFind (fun c -> c.Trim().StartsWith("sonar." + prop + "="))
    match data with
    | Some(c) -> c.Trim().Split('=').[1]
    | _ -> ""

let GetArgumentClass(additionalArguments : seq<string>, content : string [], home : string, useCli:bool) = 
    
    let mutable arguments = Map.empty
    
    let GetPathFromHome(path : string) =
        if Path.IsPathRooted(path) then
            path
        else
            Path.Combine(home, path).Replace("\\", "/")           

    let mutable propertyovermultiplelines = false
    let mutable propdata = ""
    let mutable propname = ""
    let ProcessLine(c : string) =

        // end multiline prop and reset data
        if propertyovermultiplelines then
            if c.Contains("\\n\\") then
                propdata <- propdata + c.Trim()
            else
                propertyovermultiplelines <- false
                propdata <- propdata + c.Trim()
                arguments <- arguments.Add(propname, propdata)


        if c.StartsWith("sonar.") then

            let data = c.Split('=')

            if data.[0].Contains("sonar.cxx.cppcheck.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-cppcheck/*.xml"))
                arguments <- arguments.Add(data.[0] + "s", GetPathFromHome(".cxxresults/reports-cppcheck/*.xml"))
            elif data.[0].Contains("sonar.cxx.other.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-other/*.xml"))
                arguments <- arguments.Add(data.[0] + "s", GetPathFromHome(".cxxresults/reports-other/*.xml"))
            elif data.[0].Contains("sonar.cxx.rats.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-rats/*.xml"))
                arguments <- arguments.Add(data.[0] + "s", GetPathFromHome(".cxxresults/reports-rats/*.xml"))
            elif data.[0].Contains("sonar.cxx.vera.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-vera/*.xml"))
                arguments <- arguments.Add(data.[0] + "s", GetPathFromHome(".cxxresults/reports-vera/*.xml"))
            elif data.[0].Contains("sonar.cxx.xunit.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Contains("sonar.cxx.coverage.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Contains("sonar.cxx.coverage.itReportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Contains("sonar.cxx.coverage.overallReportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Contains("sonar.cxx.compiler.reportPath") then
                if (not(useCli)) then
                    arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/BuildLog.txt"))
                    arguments <- arguments.Add("sonar.cxx.msbuild.reportPaths", GetPathFromHome(".cxxresults/BuildLog.txt"))
            elif data.[1].Contains("\\n\\") then
                propertyovermultiplelines <- true
                propdata <- data.[1]
                propname <- data.[0]
            else
                arguments <- arguments.Add(data.[0], data.[1])

    content |> Seq.iter (fun c -> ProcessLine(c.Trim()))
    additionalArguments |> Seq.iter (fun c -> ProcessLine(c.Trim()))

    // ensure stuff that we run is included
    if not(arguments.ContainsKey("sonar.cxx.rats.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.rats.reportPath", GetPathFromHome(".cxxresults/reports-rats/*.xml"))
        arguments <- arguments.Add("sonar.cxx.rats.reportPaths", GetPathFromHome(".cxxresults/reports-rats/*.xml"))
    if not(arguments.ContainsKey("sonar.cxx.vera.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.vera.reportPath", GetPathFromHome(".cxxresults/reports-vera/*.xml"))
        arguments <- arguments.Add("sonar.cxx.vera.reportPaths", GetPathFromHome(".cxxresults/reports-vera/*.xml"))
    if not(arguments.ContainsKey("sonar.cxx.cppcheck.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.cppcheck.reportPath", GetPathFromHome(".cxxresults/reports-cppcheck/*.xml"))
        arguments <- arguments.Add("sonar.cxx.cppcheck.reportPaths", GetPathFromHome(".cxxresults/reports-cppcheck/*.xml"))
    if not(arguments.ContainsKey("sonar.cxx.other.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.other.reportPath", GetPathFromHome(".cxxresults/reports-other/*.xml"))
        arguments <- arguments.Add("sonar.cxx.other.reportPaths", GetPathFromHome(".cxxresults/reports-other/*.xml"))


    if not(arguments.ContainsKey("sonar.cxx.compiler.reportPath")) then
        if (not(useCli)) then
            arguments <- arguments.Add("sonar.cxx.compiler.reportPath", GetPathFromHome(".cxxresults/BuildLog.txt"))
            arguments <- arguments.Add("sonar.cxx.msbuild.reportPaths", GetPathFromHome(".cxxresults/BuildLog.txt"))
    arguments


           

let WriteFile(file : string, content : Array) =
    use outFile = new StreamWriter(file)
    for elem in content do
        outFile.WriteLine(elem)

let WriteUserSettingsFromSonarPropertiesFile(file: string, argsParsIn : Map<string, string>, sonarPropsIn : Map<string, string>, sonarVersion:float) =
    printf "Apply the following changes to sonar configuration Files >%s<\r\n" file
    let argsPars =
        if sonarVersion >= 7.9 && argsParsIn.ContainsKey("sonar.branch") then
            argsParsIn.Remove("sonar.branch")
        else
            argsParsIn

    let sonarProps =
        if sonarVersion >= 7.9 && sonarPropsIn.ContainsKey("sonar.branch") then
            sonarPropsIn.Remove("sonar.branch")
        else
            sonarPropsIn

    argsPars |> Map.iter (fun c d ->
            let line = sprintf """"%s : %s""" c d
            printf "%s\r\n" line
        )

    sonarProps |> Map.iter (fun c d -> 
            let line = sprintf """"%s : %s""" c d
            printf "%s\r\n" line
        )
    use outFile = new StreamWriter(file)
    outFile.WriteLine("""<?xml version="1.0" encoding="utf-8" ?>""")
    outFile.WriteLine("""<SonarQubeAnalysisProperties  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">""")
    argsPars |> Map.iter (fun c d -> outFile.WriteLine((sprintf """<Property Name="%s">%s</Property>""" c d)))
    sonarProps |> Map.iter (fun c d -> if not(argsPars.ContainsKey(c)) then outFile.WriteLine((sprintf """<Property Name="%s">%s</Property>""" c d)))
    outFile.WriteLine("""</SonarQubeAnalysisProperties>""")

let WriteUserSettingsFromSonarPropertiesFileCli(file: string, argsParsIn : Map<string, string>, sonarPropsIn : Map<string, string>, sonarVersion:float) =
    printf "Apply the following changes to sonar configuration Files >%s<\r\n" file
    let argsPars =
        if sonarVersion >= 7.9 && argsParsIn.ContainsKey("sonar.branch") then
            argsParsIn.Remove("sonar.branch")
        else
            argsParsIn

    let sonarProps =
        if sonarVersion >= 7.9 && sonarPropsIn.ContainsKey("sonar.branch") then
            sonarPropsIn.Remove("sonar.branch")
        else
            sonarPropsIn

    argsPars |> Map.iter (fun c d ->
            let line = sprintf """"%s : %s""" c d
            printf "%s \r\n" line
        )
    sonarProps |> Map.iter (fun c d -> 
            let line = sprintf """"%s : %s""" c d
            printf "%s \r\n" line
        )
    use outFile = new StreamWriter(file, true)
    argsPars |> Map.iter (fun c d -> outFile.WriteLine((sprintf """%s=%s""" c d)))
    sonarProps |> Map.iter (fun c d -> if not(argsPars.ContainsKey(c)) then outFile.WriteLine((sprintf """%s=%s""" c d)))

type UserSettingsFileType = XmlProvider<"""
<SonarQubeAnalysisProperties xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">
  <Property Name="sonar.host.url">http://filehost</Property>
  <Property Name="sonar.host.url2">http://filehost2</Property>
</SonarQubeAnalysisProperties>
""">

type CxxSettingsType = XmlProvider<"""
<CxxUserProperties>
  <CppCheck>c:\path</CppCheck>
  <Rats>c:\path</Rats>
  <Vera>c:\path</Vera>
  <Python>c:\path</Python>
  <Cpplint>c:\path</Cpplint>
  <MsbuildRunnerPath>c:\path</MsbuildRunnerPath>
  <SonarCliRunnerPath>c:\path</SonarCliRunnerPath>
</CxxUserProperties>
""">

let flavours = [ "Enterprise"; "Community"; "Professional"; "BuildTools" ]

let EnvForBuild() = 
    let mutable buildEnvAvailable = List.Empty
    
    if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat") then
        buildEnvAvailable <- buildEnvAvailable @ ["C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat", "/x:vs15",""]
    
    if File.Exists("C:\\Program Files\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat") then
        buildEnvAvailable <- buildEnvAvailable @ ["C:\\Program Files\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat", "/x:vs10","/a"]
        
    for flavour in flavours do 
        if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat") then
            buildEnvAvailable <- buildEnvAvailable @ ["C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat", "/x:vs17",""]
        if File.Exists("C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat") then
            buildEnvAvailable <- buildEnvAvailable @ ["C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat", "/x:vs17","/a"]

        if File.Exists("C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat") then
            buildEnvAvailable <- buildEnvAvailable @ ["C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat", "/x:vs19",""]

        if File.Exists("C:\\Program Files\\Microsoft Visual Studio\\2019\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat") then
            buildEnvAvailable <- buildEnvAvailable @ ["C:\\Program Files\\Microsoft Visual Studio\\2019\\" + flavour + "\\Common7\\Tools\\vsdevcmd\\core\\vsdevcmd_start.bat", "/x:vs19","/a"]

    let mutable msbuildEnvAvailable = List.Empty    
    if File.Exists(@"C:\Program Files (x86)\MSBuild\14.0\Bin\amd64\MSBuild.exe") then
        msbuildEnvAvailable <- msbuildEnvAvailable @ ["C:\Program Files (x86)\MSBuild\14.0\Bin\amd64\MSBuild.exe", "/x:vs15", "/a"]
    if File.Exists(@"C:\Program Files\MSBuild\14.0\Bin\amd64\MSBuild.exe") then
        msbuildEnvAvailable <- msbuildEnvAvailable @ ["C:\Program Files\MSBuild\14.0\Bin\amd64\MSBuild.exe", "/x:vs15", "/a"]
                    
    if File.Exists(@"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe") then
        msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe", "/x:vs15", ""]
        
    if File.Exists(@"C:\Program Files\MSBuild\14.0\Bin\MSBuild.exe") then
        msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\Program Files\MSBuild\14.0\Bin\MSBuild.exe", "/x:vs15", ""]
        
    for flavour in flavours do 
        if File.Exists(@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe", "/x:vs17", "/a"]
        if File.Exists(@"C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\amd64\\MSBuild.exe", "/x:vs17", "/a"]
        if File.Exists(@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe", "/x:vs17", ""]
        elif File.Exists(@"C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files\\Microsoft Visual Studio\\2017\\" + flavour + "\\MSBuild\\15.0\\Bin\\MSBuild.exe", "/x:vs17", ""]

        if File.Exists(@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe", "/x:vs19", "/a"]
        elif File.Exists(@"C:\\Program Files\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe", "/x:vs19", "/a"]

        if File.Exists(@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\MSBuild.exe", "/x:vs19", ""]

        if File.Exists(@"C:\\Program Files\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\MSBuild.exe") then
            msbuildEnvAvailable <- msbuildEnvAvailable @ [@"C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" + flavour + "\\MSBuild\\Current\\Bin\\MSBuild.exe", "/x:vs19", ""]

    buildEnvAvailable, msbuildEnvAvailable

let ShowHelp() =
        let assembly = System.Reflection.Assembly.GetExecutingAssembly()
        let fvi = FileVersionInfo.GetVersionInfo(assembly.Location)
        let version = fvi.FileVersion

        Console.WriteLine (sprintf "Usage: CxxSonarQubeMsbuidRunner [OPTIONS] ===> %s" version)
        Console.WriteLine ("Runs MSbuild Runner with Cxx Support")
        Console.WriteLine ()
        Console.WriteLine ("Options:")
        Console.WriteLine ("    /A|/a:<amd64, disabled>")
        Console.WriteLine ("    /B|/b:<parent_branch  : in multi branch confiuration. Its parent branch>")
        Console.WriteLine ("    /C|/c:<Permission template to apply when using feature branches>")
        Console.WriteLine ("    /D|/d:<property to pass : /d:sonar.host.url=http://localhost:9000>")
        Console.WriteLine ("    /E|/e reuse reports mode, cxx  static tools will not run. Ensure reports are placed in default locations.")
        Console.WriteLine ("    /F|/f disable code analysis in solution.")
        Console.WriteLine ("    /G|/g enable verbose mode.")

        Console.WriteLine ("    /H|/h and capabilitites.")

        Console.WriteLine ("    /I|/i wrapper will install tools only. No analysis is performed")
        Console.WriteLine ("    /J|/j:<number of processor used for msbuild : /j:1 is default. 0 uses all processors>")
        Console.WriteLine ("    /L|/L:Run static analyis options only")
        Console.WriteLine ("    /K|/k:<key : key>")
        
        Console.WriteLine ("    /M|/m:<solution file or workdir : optional>")
        Console.WriteLine ("    /N|/n:<name : name>")

        Console.WriteLine ("    /O|/o delete legacy properties file")

        Console.WriteLine ("    /P|/p:<additional settings for msbuild - /p:Configuration=Release>")
        Console.WriteLine ("    /Q|/q:<SQ msbuild runner path>")
        Console.WriteLine ("    /R|/r:<msbuild sonarqube runner -> 1.1 or path to runner>")
        Console.WriteLine ("    /S|/s:<additional settings filekey>")
        Console.WriteLine ("    /T|/t:<msbuild target, default is /t:Rebuild>")
        Console.WriteLine ("    /U|/u:<dont build solution>")

        Console.WriteLine ("    /V|/v:<version : version>")
        Console.WriteLine ("    /W|/w:<skip copy false positives and apply permission template, when not defined it will copy>")
        Console.WriteLine ("    /X|/x:<version of msbuild : vs10, vs12, vs13, vs15, vs17, default is vs15>")

        Console.WriteLine ("    /Y|/y:<skip provision during branch analysis stage>")
        Console.WriteLine ("    /Z|/z:<fail build if Gate fails>")

        printf "\r\n Additional settings file cxx-user-options.xml in user home folder can be used with following format: \r\n"
        printf "\r\n%s\r\n" (CxxSettingsType.GetSample().XElement.ToString())


        let cppDevEnv, msbuildOptoins = EnvForBuild()
        printf "##### AGENT CAPABILITIES ####"
        printf "### C++ Development Env:"
        for cppDevOption in cppDevEnv do
            let cpp, cppoptionX, cppoptionA = cppDevOption 
            printf "\r\n%s ARGS: %s %s \r\n" cpp cppoptionX cppoptionA

        printf "### MSBuild Development Env:" 
        for msbuildOptoin in msbuildOptoins do
            let cpp, cppoptionX, cppoptionA = msbuildOptoin 
            printf "\r\n%s ARGS: %s %s \r\n" cpp cppoptionX cppoptionA



type OptionsData(args : string []) =
    let arguments = parseArgs(args)
    let logger = NotificationManager()

    let installMode = arguments.ContainsKey("i")

    let disableCodeAnalysis = arguments.ContainsKey("f")

    let failedOnFailedGate = arguments.ContainsKey("z")

    let reuseMode = arguments.ContainsKey("e")

    let verboseModeTrue = arguments.ContainsKey("g")

    let skipBuildSolution = arguments.ContainsKey("u")

    let deleteLegacyPropsFile = arguments.ContainsKey("o")

    let runStaticAnalysisOnly = arguments.ContainsKey("l")

    let skipGateValidation = not(arguments.ContainsKey("y"))

    let vsVersion =
        if arguments.ContainsKey("x") then
            arguments.["x"] |> Seq.head
        else
            "vs15"

    let useAmd64 = 
        if arguments.ContainsKey("a") then
            arguments.["a"] |> Seq.head
        else
            ""

    let cliRunnerVersion = 
        if arguments.ContainsKey("r") then
            arguments.["r"] |> Seq.head
        else
            "4.2.0.1873"

    let msbuildRunnerVersion = 
        if arguments.ContainsKey("r") then
            arguments.["r"] |> Seq.head
        else
            "4.8.0.12008"

    let parentBranch = 
        if arguments.ContainsKey("b") then
            arguments.["b"] |> Seq.head
        else
            ""
            
    let sqRunnerPathFromCommandLine = 
        if arguments.ContainsKey("q") then
            arguments.["q"] |> Seq.head
        else
            ""

    let permissiontemplatename = 
        if arguments.ContainsKey("c") then
            arguments.["c"] |> Seq.head
        else
            ""

    let parallelBuilds = 
        if arguments.ContainsKey("j") then
            let data = arguments.["j"] |> Seq.head
            if data = "0" then
                "/m"
            else
                "/m:" + data
        else
            "/m:1"

    let provision = 
        if arguments.ContainsKey("j") then
            true
        else
            false

    let userCxxSettings =
        if File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "cxx-user-options.xml")) then
            let data = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "cxx-user-options.xml"))
            CxxSettingsType.Parse(data)
        else
            CxxSettingsType.Parse("""<CxxUserProperties></CxxUserProperties>""")

    member val SonarHost : string = "" with get, set
    member val InstallMode : bool = installMode
    member val UserSonarScannerCli : bool = false with get, set
    member val ApplyFalseAndPermissionTemplate : bool = true with get, set
    member val SonarUserName : string = "" with get, set
    member val SonarUserPassword : string = "" with get, set
    member val Branch : string = "" with get, set
    member val UseNewBranch : bool = false with get, set
    member val Provision : bool = provision with get, set

    member val ProjectKey : string = "" with get, set
    member val ProjectName : string = "" with get, set
    member val ProjectVersion : string = "" with get, set
    member val PropsForBeginStage : string = "" with get, set
    member val PropsForMsbuild : string = "" with get, set
    member val PropsInSettingsFile : Map<string,string> = Map.empty with get, set
    member val DepracatedSonarPropsContent : string [] = [||] with get, set
    member val DeprecatedPropertiesFile : string = "" with get, set
    member val ConfigFile : string = "" with get, set

    member val SkipBuildSolution : bool = skipBuildSolution with get, set
    member val RunStaticAnalysisOnly : bool = runStaticAnalysisOnly with get, set
    member val SkipGateValidation : bool = skipGateValidation with get, set    
    
    member val Solution : string = "" with get, set
    member val SolutionName : string = "" with get, set
    member val HomePath : string = "" with get, set
    member val CxxResultsPath : string = "" with get, set
    member val CxxReportsCppCheckPath : string = "" with get, set
    member val CxxReportsVeraPath : string = "" with get, set
    member val CxxReportsRatsPath : string = "" with get, set
    member val CxxReportsCpplintPath : string = "" with get, set
    member val SolutionTargetFile : string = "" with get, set
    member val SonarQubeTempPath : string = "" with get, set

    member val VsVersion : string = vsVersion with get, set
    member val UseAmd64 : string = useAmd64 with get, set
    member val MSBuildTarget : string = "" with get, set
    member val TargetBranch : string = parentBranch with get, set
    member val ParallelMsbuildOption = "/m:1" with get, set

    member val CppCheckPath : string = "" with get, set
    member val RatsPath : string = "" with get, set
    member val VeraPath : string = "" with get, set
    member val PythonPath : string = "" with get, set
    member val CppLintPath : string = "" with get, set
    member val MSBuildRunnerPath : string = "" with get, set
    member val CliRunnerPath : string = "" with get, set
    member val BuildLog : string = "" with get, set
    member val Logger : NotificationManager = logger with get, set
    member val SonarPropsToUse : Map<string, string> = Map.empty with get, set
    member val DisableCodeAnalysis = disableCodeAnalysis
    member val IsVerboseOn = verboseModeTrue
    member val FailOnFailedGate = failedOnFailedGate
    member val AuthToken : Types.ConnectionConfiguration = null with get, set 

    member this.CreateAuthToken() =
        let GetConnectionToken(service : ISonarRestService, address : string , userName : string, password : string) = 
            let pass =
                if this.SonarUserPassword = "" && this.SonarUserName = "" then
                    "admin"
                else
                    this.SonarUserPassword

            let user =
                if this.SonarUserName = "" then
                    "admin"
                else
                    this.SonarUserName

            let token = new Types.ConnectionConfiguration(address, user, pass, 4.5)
            token.SonarVersion <- float (service.GetServerInfo(token))
            token

        let rest = new SonarRestServiceImpl.SonarService(new JsonSonarConnector())        
        this.AuthToken <- GetConnectionToken(rest, this.SonarHost, this.SonarUserName, this.SonarUserPassword)

    member this.ValidateSolutionOptions(useCli:bool) = 
        let mutable skipBuild = false
        if not(arguments.ContainsKey("m")) then
            this.HomePath <- Environment.CurrentDirectory
            this.ConfigFile <- Path.GetTempFileName()
            this.UserSonarScannerCli <- true
            skipBuild <- true
        else
            let data = arguments.["m"] |> Seq.head

            this.Solution <- 
                if data.EndsWith(".sln") then
                    if Path.IsPathRooted(data) then
                        data
                    else
                        Path.Combine(Environment.CurrentDirectory, data)
                else
                    this.HomePath <- data
                    this.UserSonarScannerCli <- true
                    skipBuild <- true
                    ""

        if this.Solution <> "" then
            this.HomePath <- Directory.GetParent(this.Solution).ToString()

        if not(Directory.Exists(this.HomePath)) then
            raise (ArgumentException("HomePath not Found: "  + this.HomePath))

        this.DeprecatedPropertiesFile <- Path.Combine(this.HomePath, "sonar-project.properties")

        if deleteLegacyPropsFile then
            if File.Exists(this.DeprecatedPropertiesFile) then
                printf "Delete %s\r\n" this.DeprecatedPropertiesFile
                File.Delete(this.DeprecatedPropertiesFile)

        if File.Exists(this.DeprecatedPropertiesFile) then
            this.DepracatedSonarPropsContent <- File.ReadAllLines(this.DeprecatedPropertiesFile)

        if not(skipBuild) then
            if not(File.Exists(this.Solution)) then
                let errorMsg = sprintf "/m used does not point to existent solution: %s" this.Solution
                printf "%s\r\n\r\n" errorMsg
                ShowHelp()
                raise(new Exception())

        
            // setup properties paths
            this.ParallelMsbuildOption <- parallelBuilds
            this.SolutionName <- Path.GetFileNameWithoutExtension(this.Solution)

            this.ConfigFile <- Path.GetTempFileName()
            this.SolutionTargetFile <- Path.Combine(this.HomePath, "after." + this.SolutionName + ".sln.targets")   

            if Environment.GetEnvironmentVariable("AGENT_BUILDDIRECTORY") <> null then
                this.SonarQubeTempPath <- Path.Combine(Environment.GetEnvironmentVariable("AGENT_BUILDDIRECTORY"), ".sonarqube")
                
            else
                this.SonarQubeTempPath <- Path.Combine(this.HomePath, ".sonarqube")

        this.CxxResultsPath <- Path.Combine(this.HomePath, ".cxxresults")
        this.CxxReportsCppCheckPath <- Path.Combine(this.CxxResultsPath, "reports-cppcheck")
        this.CxxReportsVeraPath <- Path.Combine(this.CxxResultsPath, "reports-vera")
        this.CxxReportsRatsPath <- Path.Combine(this.CxxResultsPath, "reports-rats")
        this.CxxReportsCpplintPath <- Path.Combine(this.CxxResultsPath, "reports-other")
        skipBuild

    // get first from command line, second from user settings file and finally from web
    member this.ConfigureMsbuildRunner(options:OptionsData) =
        if sqRunnerPathFromCommandLine <> "" && File.Exists(sqRunnerPathFromCommandLine) then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "[CxxSonarQubeMsbuidRunner] Use commandline switch: " + sqRunnerPathFromCommandLine)
            this.MSBuildRunnerPath <- sqRunnerPathFromCommandLine
        else 
            try
                this.MSBuildRunnerPath <- userCxxSettings.MsbuildRunnerPath
                HelpersMethods.cprintf(ConsoleColor.Yellow, "[CxxSonarQubeMsbuidRunner] Use user settings file switch: " + userCxxSettings.MsbuildRunnerPath)
            with
            | _ -> this.MSBuildRunnerPath <- 
                    if File.Exists(msbuildRunnerVersion) then
                        msbuildRunnerVersion
                    else
                        InstallScannerRunner(msbuildRunnerVersion, cliRunnerVersion)

        let scanner =
            if InstallationModule.isWindowSystem then
                Directory.GetFiles(Directory.GetParent(this.MSBuildRunnerPath).FullName, "sonar-scanner.bat", SearchOption.AllDirectories)
            else
                Directory.GetFiles(Directory.GetParent(this.MSBuildRunnerPath).FullName, "sonar-scanner", SearchOption.AllDirectories)

        this.CliRunnerPath <- scanner.[0]

        if vsVersion = "dotnet" then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "[CxxSonarQubeMsbuidRunner] Will use dotnet sonarscanner tooling")
        elif options.UserSonarScannerCli then
            HelpersMethods.cprintf(ConsoleColor.Yellow, "[CxxSonarQubeMsbuidRunner] Will use scanner cli: " + this.CliRunnerPath)
        else
            HelpersMethods.cprintf(ConsoleColor.Yellow, "[CxxSonarQubeMsbuidRunner] Will use sonar msbuild scanner: " + this.MSBuildRunnerPath)


    member this.ConfigureInstallationOfTools() =
        
        try
            this.CppCheckPath <- userCxxSettings.CppCheck
        with
        | _ -> this.CppCheckPath <- InstallCppCheck()

        try
            this.RatsPath <- userCxxSettings.Rats
        with
        | _ -> this.RatsPath <- InstallRats()

        try
            this.VeraPath <- userCxxSettings.Vera
        with
        | _ -> this.VeraPath <- InstallVera()

        try
            this.PythonPath <- userCxxSettings.Python
        with
        | _ -> this.PythonPath <- InstallPython()

        try
            this.CppLintPath <- userCxxSettings.Cpplint
        with
        | _ -> this.CppLintPath <- InstallCppLint()

        printf "Tools Installed\r\n\r\n"


    member this.CreatOptionsForAnalysis() =        
        this.ProjectKey <-
            if arguments.ContainsKey("k") then
                "/k:" + (arguments.["k"] |> Seq.head)
            else
                "/k:" + (GetPropertyFromFile(this.DepracatedSonarPropsContent, "projectKey"))

        let name = 
            if arguments.ContainsKey("n") then
                (arguments.["n"] |> Seq.head)
            else
                (GetPropertyFromFile(this.DepracatedSonarPropsContent, "projectName"))

        if name.Contains(" ") then
            this.ProjectName <- "/n:\"" + name + "\""
        else
            this.ProjectName <- "/n:" + name
        
        this.ProjectVersion <- 
            if arguments.ContainsKey("v") then
                "/v:" + (arguments.["v"] |> Seq.head)
            else
                "/v:" + (GetPropertyFromFile(this.DepracatedSonarPropsContent, "projectVersion"))

        if this.ProjectKey = "/k:" then
            this.ProjectKey <- ""

        if this.ProjectName = "/n:" then
            this.ProjectName <- ""

        if this.ProjectVersion = "/v:" then
            this.ProjectVersion <- ""


        this.MSBuildTarget <-
            if arguments.ContainsKey("t") then
                "/t:" + (arguments.["t"] |> Seq.head)
            else
                "/t:Clean;Build"

        

        // read settings from installation folder
        try
            let data = UserSettingsFileType.Parse(File.ReadAllText(Path.Combine(Directory.GetParent(this.MSBuildRunnerPath).ToString(), "SonarQube.Analysis.xml")))

            for prop in data.Properties do
                if prop.Name.Equals("sonar.host.url") then
                    this.SonarHost <- prop.Value
                if prop.Name.Equals("sonar.login") then
                    this.SonarUserName <- prop.Value
                if prop.Name.Equals("sonar.password") then
                    this.SonarUserPassword <- prop.Value
                if prop.Name.Equals("sonar.branch") then
                    this.Branch <- prop.Value
                if prop.Name.Equals("sonar.branch.name") then
                    this.Branch <- prop.Value
                    this.UseNewBranch <- true
        with
        | _ -> ()

        this.ApplyFalseAndPermissionTemplate <- not(arguments.ContainsKey("w"))

        // options that are passed with /s
        this.PropsInSettingsFile <- 
            if arguments.ContainsKey("s") then
                
                let configFile = arguments.["s"] |> Seq.head
                let elements = UserSettingsFileType.Parse(configFile)
                let elems = (List.ofSeq elements.Properties)
                elements.Properties |> Seq.map (fun f -> f.Name, f.Value) |> Map.ofSeq
            else
                Map.empty

        if this.PropsInSettingsFile.ContainsKey("sonar.host.url") then
            this.SonarHost <- this.PropsInSettingsFile.["sonar.host.url"]
        if this.PropsInSettingsFile.ContainsKey("sonar.login") then
            this.SonarUserName <- this.PropsInSettingsFile.["sonar.login"]
        if this.PropsInSettingsFile.ContainsKey("sonar.password") then
            this.SonarUserPassword <- this.PropsInSettingsFile.["sonar.password"]
        if this.PropsInSettingsFile.ContainsKey("sonar.branch") then
            this.Branch <- this.PropsInSettingsFile.["sonar.branch"]
        if this.PropsInSettingsFile.ContainsKey("sonar.branch.name") then
            this.Branch <- this.PropsInSettingsFile.["sonar.branch.name"]
            this.UseNewBranch <- true

        this.PropsForBeginStage <- 
            let mutable args = ""
            if arguments.ContainsKey("d") then
                for arg in arguments.["d"] do
                    if arg <> "" then
                        if arg.StartsWith("sonar.login") || arg.StartsWith("sonar.password") ||  arg.StartsWith("sonar.host.url") ||  arg.StartsWith("sonar.branch") then
                            if arg.StartsWith("sonar.login") then
                                this.SonarUserName <- arg.Replace("sonar.login=", "")
                            if arg.StartsWith("sonar.password") then
                                this.SonarUserPassword <- arg.Replace("sonar.password=", "")
                            if arg.StartsWith("sonar.host.url") then
                                this.SonarHost <- arg.Replace("sonar.host.url=", "")
                            if arg.StartsWith("sonar.branch") then
                                this.Branch <- arg.Replace("sonar.branch=", "")
                            if arg.StartsWith("sonar.branch.name") then
                                this.Branch <- arg.Replace("sonar.branch.name=", "")
                                this.UseNewBranch <- true
                        else
                            args <- args + " /d:" + arg

            args.Trim()

        this.PropsForMsbuild <-
            let mutable args = ""
            if arguments.ContainsKey("p") then
                for arg in arguments.["p"] do
                    if arg <> "" then
                        if arg.Contains(" ") then
                            let elems = arg.Split('=')
                            args <- args + " /p:" + elems.[0] + "=\"" + elems.[1] + "\""
                        else
                            args <- args + " /p:" + arg

            args.Trim()


    member this.DuplicateFalsePositives() = 
        if parentBranch <> "" && this.Branch <> "" then
            let key = this.ProjectKey.Replace("/k:", "")
            let rest = new SonarRestServiceImpl.SonarService(new JsonSonarConnector())        
            let masterProject = (rest :> ISonarRestService).GetResourcesData(this.AuthToken, key + ":" + parentBranch).[0]
            let branchProject = (rest :> ISonarRestService).GetResourcesData(this.AuthToken, key + ":" + this.Branch).[0]

            if permissiontemplatename <> "" then
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Apply Permission Template ############") 
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
                let errormsg = (rest :> ISonarRestService).ApplyPermissionTemplateToProject(this.AuthToken, branchProject.Key, permissiontemplatename)
                if errormsg <> "" then
                    printf "[CxxSonarQubeMsbuidRunner] Failed to apply permission template %s : %s\r\n" permissiontemplatename errormsg
                else
                    printf "[CxxSonarQubeMsbuidRunner] permission template %s : applied correctly to %s \r\n" permissiontemplatename  branchProject.Key

            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Duplicate False Positives ############") 
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")

            let filter = "?componentRoots=" + masterProject.Key.Trim() + "&resolutions=FALSE-POSITIVE,WONTFIX"
            let falsePositivesInMaster = (rest :> ISonarRestService).GetIssues(this.AuthToken, filter, masterProject.Key, CancellationToken(), logger).Result

            printf "[CxxSonarQubeMsbuidRunner] : Filter: %s  -> False Positives and Wont Fix : %i \r\n" filter falsePositivesInMaster.Count

            let issuesByComp = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Issue>>()

            for issue in falsePositivesInMaster do
                if issuesByComp.ContainsKey(issue.Component) then
                    let comp = issuesByComp.[issue.Component]
                    comp.Add(issue)
                else
                    let newIssueList = System.Collections.Generic.List<Issue>()
                    newIssueList.Add(issue)
                    issuesByComp.Add(issue.Component, newIssueList)


            for comp in issuesByComp do
                let issuesInComponentBranch = (rest :> ISonarRestService).GetIssuesInResource(this.AuthToken, comp.Key.Replace(parentBranch, this.Branch), CancellationToken(), logger).Result
                
                printf "[CxxSonarQubeMsbuidRunner] : Try to apply false positives in : %s  -> Issues Found : %i \r\n" (comp.Key.Replace(parentBranch, this.Branch)) issuesInComponentBranch.Count

                for issuetochange in comp.Value do
                    let isMatch = List.ofSeq issuesInComponentBranch |> Seq.tryFind (fun c -> c.Rule.Equals(issuetochange.Rule) && c.Line.Equals(issuetochange.Line) && not(c.Resolution.Equals(Resolution.FALSE_POSITIVE)) && not(c.Resolution.Equals(Resolution.WONTFIX)))
                    match isMatch with
                    | Some c -> let issuelist = new System.Collections.Generic.List<Issue>()
                                issuelist.Add(c)
                                if issuetochange.Resolution.Equals(Resolution.WONTFIX) then
                                    (rest :> ISonarRestService).MarkIssuesAsWontFix(this.AuthToken, issuelist, "", logger, (new CancellationTokenSource()).Token).GetAwaiter().GetResult() |> ignore
                                else
                                    (rest :> ISonarRestService).MarkIssuesAsFalsePositive(this.AuthToken, issuelist, "", logger, (new CancellationTokenSource()).Token).GetAwaiter().GetResult() |> ignore
                    | _ -> ()
            ()

    member this.ProvisionProject() =
        
        if parentBranch <> "" && this.Branch <> "" && this.Provision then

            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##########################################")
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Provision Project ############") 
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##########################################")

            let GetConnectionToken(service : ISonarRestService, address : string , userName : string, password : string) = 
                let pass = this.SonarUserPassword


                let user =
                    if this.SonarUserName = "" then
                        "admin"
                    else
                        this.SonarUserName

                let token = new Types.ConnectionConfiguration(address, user, pass, 4.5)
                token.SonarVersion <- float (service.GetServerInfo(token))
                token

            let rest = new SonarService(new JsonSonarConnector())
            let token = GetConnectionToken(rest, this.SonarHost, this.SonarUserName, this.SonarUserPassword)

            let key = this.ProjectKey.Replace("/k:", "")

            let projectParent = 
                try (rest :> ISonarRestService).GetResourcesData(token, key + ":" + parentBranch) with | _ -> raise(new Exception("Cannot provision with current settings, unable to find main branch : " + parentBranch))
                            
            let branchProject = 
                try (rest :> ISonarRestService).GetResourcesData(token, key + ":" + this.Branch).[0]
                with
                | _ ->
                    // provision project
                    let returndata = (rest :> ISonarRestService).ProvisionProject(token, key, this.ProjectName.Replace("/n:", ""), this.Branch)
                    if returndata.Contains("Could not create Project, key already exists:") then
                        printf "[CxxSonarQubeMsbuidRunner] Project was provisioned already, skip %s \r\n" key
                    elif returndata = "" then
                        printf "[CxxSonarQubeMsbuidRunner] New project was provisioned correctly %s \r\n" key
                    else
                        raise(new Exception("Cannot provision current branch : " + returndata))

                    new Resource(Key = key + ":" + this.Branch, BranchName = this.Branch)

            if permissiontemplatename <> "" then
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Apply Permission Template ############") 
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
                let errormsg = (rest :> ISonarRestService).ApplyPermissionTemplateToProject(token, branchProject.Key, permissiontemplatename)
                if errormsg <> "" then
                    printf "[CxxSonarQubeMsbuidRunner] Failed to apply permission template %s : %s\r\n" permissiontemplatename errormsg
                else
                    printf "[CxxSonarQubeMsbuidRunner] permission template %s : applied correctly to %s \r\n" permissiontemplatename  branchProject.Key

            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "#######################################################")
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Duplicate Settings from Master ############") 
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "#######################################################")
            // duplicate main branch props to branch
            let propertiesofMainBranch = (rest :> ISonarRestService).GetSettings(token, projectParent.[0]) |> Seq.toList
            printf "[CxxSonarQubeMsbuidRunner] Duplicating %i properties from master\r\n" propertiesofMainBranch.Length
            for prop in propertiesofMainBranch do
                try
                    let errormsg = (rest :> ISonarRestService).SetSetting(token, prop, branchProject)
                    if errormsg <> "" then
                        printf "[CxxSonarQubeMsbuidRunner] failed to set: %s : %s \r\n" prop.Key errormsg
                    else
                        printf "[CxxSonarQubeMsbuidRunner] %s : set with Value: %s \r\n" prop.Key prop.Value
                with
                | ex -> printf "[CxxSonarQubeMsbuidRunner] failed to set: %s : %s \r\n" prop.Key ex.Message
            
            // clean any prop that is not in main
            //let propertiesofBranch = (rest :> ISonarRestService).GetSettings(token, branchProject).ToList()
            //for prop in propertiesofBranch do
            //    if propertiesofMainBranch.ContainsKey(prop.Key) && not(propertiesofMainBranch.[prop.Key].Equals(prop.Value)) then
            //        let errormsg = (rest :> ISonarRestService).UpdateProperty(token, prop.Key, propertiesofMainBranch.[prop.Key], branchProject)
            //        if errormsg <> "" then
            //            printf "[CxxSonarQubeMsbuidRunner] Failed to apply updated value from main branch : prop %s : %s\r\n" prop.Key errormsg
            //        else
            //            printf "[CxxSonarQubeMsbuidRunner] Applied updated value from main branch : prop %s : %s\r\n" prop.Key prop.Value

            //    if not(propertiesofMainBranch.ContainsKey(prop.Key)) then
            //        let errormsg = (rest :> ISonarRestService).UpdateProperty(token, prop.Key, "", branchProject)
            //        if errormsg <> "" then
            //            printf "[CxxSonarQubeMsbuidRunner] Failed to clear prop %s : %s\r\n" prop.Key errormsg

            // ensure same quality profiles are in used by both branches
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "#######################################################")
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Applying Quality Profiles #################") 
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "#######################################################")
            let profiles = (rest :> ISonarRestService).GetQualityProfilesForProject(token, projectParent.[0])
            let profilesBranch = (rest :> ISonarRestService).GetQualityProfilesForProject(token, branchProject)

            for profile in profiles do
                let branchProfile = List.ofSeq profilesBranch |> Seq.find (fun c -> c.Language.Equals(profile.Language))

                if branchProfile.Name <> profile.Name then
                    let errormsg = (rest :> ISonarRestService).AssignProfileToProject(token, profile.Key, branchProject.Key)
                    if errormsg <> "" then
                        printf "[CxxSonarQubeMsbuidRunner] Failed to apply profile %s : %s\r\n" profile.Name errormsg
                    else
                        printf "[CxxSonarQubeMsbuidRunner] Profile %s : applied correctly\r\n" profile.Name
                else
                    printf "[CxxSonarQubeMsbuidRunner] Profile %s : already correct\r\n" profile.Name

    member this.Setup(options:OptionsData, skipBuild:bool) =
        // read sonar project files
        if File.Exists(this.DeprecatedPropertiesFile) then
            if not(options.UserSonarScannerCli) then
                printf "Wipe sonar properties file: %s\r\n" this.DeprecatedPropertiesFile
                File.Delete(this.DeprecatedPropertiesFile)

        if options.UserSonarScannerCli then
            this.ConfigFile <- this.DeprecatedPropertiesFile

        options.SonarPropsToUse <- GetArgumentClass(arguments.["d"], this.DepracatedSonarPropsContent, this.HomePath, options.UserSonarScannerCli)
        if options.UserSonarScannerCli then
            WriteUserSettingsFromSonarPropertiesFileCli(this.ConfigFile, this.PropsInSettingsFile, options.SonarPropsToUse, options.AuthToken.SonarVersion)
        else
            WriteUserSettingsFromSonarPropertiesFile(this.ConfigFile, this.PropsInSettingsFile, options.SonarPropsToUse, options.AuthToken.SonarVersion)
        
        Directory.CreateDirectory(Path.Combine(this.HomePath, ".cxxresults")) |> ignore
        this.BuildLog <- Path.Combine(this.HomePath, ".cxxresults", "BuildLog.txt")

        if not(skipBuild) then
            MSBuildHelper.CreateSolutionData(this.Solution)
        else
            null

    member this.Clean() =
        if this.DepracatedSonarPropsContent <> Array.empty then
            WriteFile(this.DeprecatedPropertiesFile, this.DepracatedSonarPropsContent)

        if File.Exists(this.ConfigFile) && this.ConfigFile <> this.DeprecatedPropertiesFile then
            File.Delete(this.ConfigFile)

        if File.Exists(this.SolutionTargetFile) then
            File.Delete(this.SolutionTargetFile)

        try
            if Directory.Exists(Path.Combine(this.SonarQubeTempPath, "bin")) then
                Directory.Delete(Path.Combine(this.SonarQubeTempPath, "bin"), true)
        with
        | _ -> printf "Failed to clean target files, compilation might not be possible. Kill any msbuild processes before compilation"



let PatchMSbuildSonarRunnerTargetsFiles(targetFile : string, options : OptionsData) =
    let content = File.ReadAllLines(targetFile)
    use outFile = new StreamWriter(targetFile, false)
    
    for line in content do
        if line.Contains("""<SQAnalysisFileItemTypes Condition=" $(SQAnalysisFileItemTypes) == '' ">""") then
            outFile.WriteLine("""<SQAnalysisFileItemTypes Condition=" $(SQAnalysisFileItemTypes) == '' ">Compile;Content;EmbeddedResource;None;ClCompile;ClInclude;Page;TypeScriptCompile</SQAnalysisFileItemTypes>""")

        elif line.Contains("""<RunCodeAnalysisOnThisProject>$(SonarQubeRunMSCodeAnalysis)</RunCodeAnalysisOnThisProject>""") then
            if options.DisableCodeAnalysis then
                outFile.WriteLine("""<RunCodeAnalysisOnThisProject>false</RunCodeAnalysisOnThisProject>""")
            else
                outFile.WriteLine(line)

        elif line.Contains("""<RunCodeAnalysisOnce>true</RunCodeAnalysisOnce>""") then
            if options.DisableCodeAnalysis then
                outFile.WriteLine("""<RunCodeAnalysisOnce>false</RunCodeAnalysisOnce>""")
            else
                outFile.WriteLine(line)

        elif line.Contains("""<SonarQubeDisableRoslynCodeAnalysis Condition="$(SonarQubeExclude) == 'true' OR $(SonarQubeTestProject) == 'true' ">true</SonarQubeDisableRoslynCodeAnalysis>""") then
            if options.DisableCodeAnalysis then
                outFile.WriteLine("""<SonarQubeDisableRoslynCodeAnalysis>true</SonarQubeDisableRoslynCodeAnalysis>""")
            else
                outFile.WriteLine(line)

        else
            outFile.WriteLine(line)


        if line.Contains("""<SonarQubeAnalysisFiles Include="@(%(SonarQubeAnalysisFileItems.Identity))" />""") then
            outFile.WriteLine("""<SonarQubeAnalysisFiles Include="$(MSBuildProjectFullPath)" />""")            

