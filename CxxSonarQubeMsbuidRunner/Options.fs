module Options

open System
open System.IO
open System.IO.Compression
open System.Text
open System.Net
open System.Text.RegularExpressions
open System.Diagnostics
open System.Reflection
open FSharp.Data
open InstallationModule
open VSSonarPlugins
open VSSonarPlugins.Types
open SonarRestService

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

let GetArgumentClass(additionalArguments : string, content : string [], home : string) = 
    
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

            if data.[0].Equals("sonar.cxx.cppcheck.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-cppcheck/*.xml"))
            elif data.[0].Equals("sonar.cxx.other.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-other/*.xml"))
            elif data.[0].Equals("sonar.cxx.rats.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-rats/*.xml"))
            elif data.[0].Equals("sonar.cxx.vera.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/reports-vera/*.xml"))
            elif data.[0].Equals("sonar.cxx.xunit.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Equals("sonar.cxx.coverage.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Equals("sonar.cxx.coverage.itReportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Equals("sonar.cxx.coverage.overallReportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(data.[1]))
            elif data.[0].Equals("sonar.cxx.compiler.reportPath") then
                arguments <- arguments.Add(data.[0], GetPathFromHome(".cxxresults/BuildLog.txt"))
            elif data.[1].Contains("\\n\\") then
                propertyovermultiplelines <- true
                propdata <- data.[1]
                propname <- data.[0]
            else
                arguments <- arguments.Add(data.[0], data.[1])

    content |> Seq.iter (fun c -> ProcessLine(c.Trim()))

    // ensure stuff that we run is included
    if not(arguments.ContainsKey("sonar.cxx.rats.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.rats.reportPath", GetPathFromHome(".cxxresults/reports-rats/*.xml"))
    if not(arguments.ContainsKey("sonar.cxx.vera.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.vera.reportPath", GetPathFromHome(".cxxresults/reports-vera/*.xml"))
    if not(arguments.ContainsKey("sonar.cxx.cppcheck.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.cppcheck.reportPath", GetPathFromHome(".cxxresults/reports-cppcheck/*.xml"))
    if not(arguments.ContainsKey("sonar.cxx.other.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.other.reportPath", GetPathFromHome(".cxxresults/reports-other/*.xml"))
    if not(arguments.ContainsKey("sonar.cxx.compiler.reportPath")) then
        arguments <- arguments.Add("sonar.cxx.compiler.reportPath", GetPathFromHome(".cxxresults/BuildLog.txt"))
    arguments

let PatchMSbuildSonarRunnerTargetsFiles(targetFile : string) =
    let content = File.ReadAllLines(targetFile)
    use outFile = new StreamWriter(targetFile, false)
    
    for line in content do
        if line.Contains("""<SQAnalysisFileItemTypes Condition=" $(SQAnalysisFileItemTypes) == '' ">""") then
            outFile.WriteLine("""<SQAnalysisFileItemTypes Condition=" $(SQAnalysisFileItemTypes) == '' ">Compile;Content;EmbeddedResource;None;ClCompile;ClInclude;Page;TypeScriptCompile</SQAnalysisFileItemTypes>""")
        else
            outFile.WriteLine(line)

        if line.Contains("""<SonarQubeAnalysisFiles Include="@(%(SonarQubeAnalysisFileItems.Identity))" />""") then
            outFile.WriteLine("""<SonarQubeAnalysisFiles Include="$(MSBuildProjectFullPath)" />""")            


let WriteFile(file : string, content : Array) =
    use outFile = new StreamWriter(file)
    for elem in content do
        outFile.WriteLine(elem)

let WriteUserSettingsFromSonarPropertiesFile(file: string, argsPars : Map<string, string>, sonarProps : Map<string, string>) =
    use outFile = new StreamWriter(file)
    outFile.WriteLine("""<?xml version="1.0" encoding="utf-8" ?>""")
    outFile.WriteLine("""<SonarQubeAnalysisProperties  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">""")
    argsPars |> Map.iter (fun c d -> outFile.WriteLine((sprintf """<Property Name="%s">%s</Property>""" c d)))
    sonarProps |> Map.iter (fun c d -> if not(argsPars.ContainsKey(c)) then outFile.WriteLine((sprintf """<Property Name="%s">%s</Property>""" c d)))
    outFile.WriteLine("""</SonarQubeAnalysisProperties>""")

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
</CxxUserProperties>
""">


let ShowHelp() =
        Console.WriteLine ("Usage: CxxSonarQubeMsbuidRunner [OPTIONS]")
        Console.WriteLine ("Runs MSbuild Runner with Cxx Support")
        Console.WriteLine ()
        Console.WriteLine ("Options:")
        Console.WriteLine ("    /M|/m:<solution file : mandatory>")
        Console.WriteLine ("    /N|/n:<name : name>")
        Console.WriteLine ("    /K|/k:<key : key>")
        Console.WriteLine ("    /V|/v:<version : version>")
        Console.WriteLine ("    /B|/b:<parent_branch  : in multi branch confiuration. Its parent branch>")
        Console.WriteLine ("    /P|/p:<additional settings for msbuild - /p:Configuration=Release>")
        Console.WriteLine ("    /S|/s:<additional settings filekey>")
        Console.WriteLine ("    /R|/r:<msbuild sonarqube runner -> 1.1>")
        Console.WriteLine ("    /Q|/q:<SQ msbuild runner path>")
        Console.WriteLine ("    /D|/d:<property to pass : /d:sonar.host.url=http://localhost:9000>")
        Console.WriteLine ("    /X|/x:<version of msbuild : vs10, vs12, vs13, vs15, default is vs15>")
        Console.WriteLine ("    /A|/a:<amd64, disabled>")
        Console.WriteLine ("    /T|/t:<msbuild target, default is /t:Rebuild>")
        Console.WriteLine ("    /C|/c:<Permission template to apply when using feature branches>")

        printf "\r\n Additional settings file cxx-user-options.xml in user home folder can be used with following format: \r\n"
        printf "\r\n%s\r\n" (CxxSettingsType.GetSample().XElement.ToString())




type OptionsData(args : string []) =
    let arguments = parseArgs(args)
    
    let msbuildRunnerVersion = 
        if arguments.ContainsKey("r") then
            arguments.["r"] |> Seq.head
        else
            "1.1"

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

    let userCxxSettings =
        if File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "cxx-user-options.xml")) then
            let data = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "cxx-user-options.xml"))
            CxxSettingsType.Parse(data)
        else
            CxxSettingsType.Parse("""<CxxUserProperties></CxxUserProperties>""")

    let isChocoOk =
        try
            InstallChocolatey()
            true
        with
        | _ -> false

    let DeployCxxTargets(options : OptionsData) =
        let assembly = Assembly.GetExecutingAssembly()
        let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
        use stream = assembly.GetManifestResourceStream("after.solution.sln.targets")
        use streamreader = new StreamReader(stream)
        let spliters = [|"\r\n"|]
        let content = streamreader.ReadToEnd().Split(spliters, StringSplitOptions.RemoveEmptyEntries)

        use outFile = new StreamWriter(options.SolutionTargetFile, false)
        for line in content do        
            let mutable lineToWrite = line
        
            if lineToWrite.Contains("""$(SolutionDir)""") then
                lineToWrite <- lineToWrite.Replace("$(SolutionDir)", options.HomePath)

            if lineToWrite.Contains("""$(SolutionName)""") then
                lineToWrite <- lineToWrite.Replace("$(SolutionName)", options.SolutionName)

            if lineToWrite.Contains("""<CppCheckPath               Condition="'$(CppCheckPath)' == ''">C:\Program Files (x86)\Cppcheck\cppcheck.exe</CppCheckPath>""") then
                lineToWrite <- lineToWrite.Replace("C:\\Program Files (x86)\\Cppcheck\\cppcheck.exe", options.CppCheckPath)

            if lineToWrite.Contains("""<PythonPath                   Condition="'$(PythonPath)' == ''">c:\tools\Python2\Python.exe</PythonPath>""") then
                lineToWrite <- lineToWrite.Replace("c:\\tools\\Python2\\Python.exe", options.PythonPath)

            if lineToWrite.Contains("""<CppLintPath                   Condition="'$(CppLintPath)' == ''">$(MSBuildThisFileDirectory)\cpplint_mod.py</CppLintPath>""") then
                lineToWrite <- lineToWrite.Replace("$(MSBuildThisFileDirectory)", InstallationModule.InstallationPathHome + "\\")

            if lineToWrite.Contains("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\VERA\bin\vera++.exe""") then
                lineToWrite <- lineToWrite.Replace("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\VERA\bin\vera++.exe""", options.VeraPath)

            if lineToWrite.Contains("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\RATS\rats.exe""") then
                lineToWrite <- lineToWrite.Replace("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\RATS\rats.exe""", options.RatsPath)
            
            if lineToWrite.Contains("""$(MSBuildThisFileDirectory)""") then            
                lineToWrite <- lineToWrite.Replace("$(MSBuildThisFileDirectory)", executingPath + "\\")

            outFile.WriteLine(lineToWrite)


    member val SonarHost : string = "" with get, set
    member val SonarUserName : string = "" with get, set
    member val SonarUserPassword : string = "" with get, set
    member val Branch : string = "" with get, set

    member val ProjectKey : string = "" with get, set
    member val ProjectName : string = "" with get, set
    member val ProjectVersion : string = "" with get, set
    member val PropsForBeginStage : string = "" with get, set
    member val PropsForMsbuild : string = "" with get, set
    member val PropsInSettingsFile : Map<string,string> = Map.empty with get, set
    member val DepracatedSonarPropsContent : string [] = [||] with get, set
    member val DeprecatedPropertiesFile : string = "" with get, set
    member val ConfigFile : string = "" with get, set

    member val Solution : string = "" with get, set
    member val SolutionName : string = "" with get, set
    member val HomePath : string = "" with get, set
    member val SolutionTargetFile : string = "" with get, set
    member val SonarQubeTempPath : string = "" with get, set

    member val VsVersion : string = "" with get, set
    member val UseAmd64 : string = "" with get, set
    member val Target : string = "" with get, set

    member val CppCheckPath : string = "" with get, set
    member val RatsPath : string = "" with get, set
    member val VeraPath : string = "" with get, set
    member val PythonPath : string = "" with get, set
    member val CppLintPath : string = "" with get, set
    member val MSBuildRunnerPath : string = "" with get, set
    member val BuildLog : string = "" with get, set

    member this.ValidateSolutionOptions() = 
        if not(arguments.ContainsKey("m")) then
            ShowHelp()
            raise(new Exception("/m must be specifed. See /h for complete help"))

        if not(arguments.ContainsKey("m")) then
            ShowHelp()
            raise(new Exception("/m must be specifed. See /h for complete help"))

        this.Solution <- 
            let data = arguments.["m"] |> Seq.head
            if Path.IsPathRooted(data) then
                data
            else
                Path.Combine(Environment.CurrentDirectory, data)

        if not(File.Exists(this.Solution)) then
            ShowHelp()
            raise(new Exception("/m used does not point to existent solution: " + this.Solution))

        
        // setup properties paths
        this.SolutionName <- Path.GetFileNameWithoutExtension(this.Solution)                                        
        this.HomePath <- Directory.GetParent(this.Solution).ToString()
        this.ConfigFile <- Path.GetTempFileName()
        this.SolutionTargetFile <- Path.Combine(this.HomePath, "after." + this.SolutionName + ".sln.targets")   
        this.DeprecatedPropertiesFile <- Path.Combine(this.HomePath, "sonar-project.properties")

        if File.Exists(this.DeprecatedPropertiesFile) then
            this.DepracatedSonarPropsContent <- File.ReadAllLines(this.DeprecatedPropertiesFile)

        this.SonarQubeTempPath <- Path.Combine(this.HomePath, ".sonarqube")
        
    // get first from command line, second from user settings file and finally from web
    member this.ConfigureMsbuildRunner() =  
        if sqRunnerPathFromCommandLine <> "" && File.Exists(sqRunnerPathFromCommandLine) then
            this.MSBuildRunnerPath <- sqRunnerPathFromCommandLine
        else 
            try
                this.MSBuildRunnerPath <- userCxxSettings.MsbuildRunnerPath
            with
            | _ -> this.MSBuildRunnerPath <- InstallMsbuildRunner(msbuildRunnerVersion)

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


    member this.CreatOptionsForAnalysis() =        
        this.ProjectKey <-
            if arguments.ContainsKey("k") then
                "/k:" + (arguments.["k"] |> Seq.head)
            else
                "/k:" + (GetPropertyFromFile(this.DepracatedSonarPropsContent, "projectKey"))

        this.ProjectName <- 
            if arguments.ContainsKey("n") then
                "/n:" + (arguments.["n"] |> Seq.head)
            else
                "/n:" + (GetPropertyFromFile(this.DepracatedSonarPropsContent, "projectName"))
        
        this.ProjectVersion <- 
            if arguments.ContainsKey("v") then
                "/v:" + (arguments.["v"] |> Seq.head)
            else
                "/v:" + (GetPropertyFromFile(this.DepracatedSonarPropsContent, "projectVersion"))

        this.VsVersion <- 
            if arguments.ContainsKey("x") then
                arguments.["x"] |> Seq.head
            else
                "vs15"

        this.UseAmd64 <- 
            if arguments.ContainsKey("a") then                
                arguments.["a"] |> Seq.head
            else
                ""
        this.Target <-
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
        with
        | _ -> ()

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

            let GetConnectionToken(service : ISonarRestService, address : string , userName : string, password : string) = 
                let pass =
                    if this.SonarUserPassword = "" then
                        "admin"
                    else
                        this.SonarUserPassword

                let user =
                    if this.SonarUserName = "" then
                        "admin"
                    else
                        this.SonarUserName

                let token = new VSSonarPlugins.Types.ConnectionConfiguration(address, user, pass, 4.5)
                token.SonarVersion <- float (service.GetServerInfo(token))
                token

            let key = this.ProjectKey.Replace("/k:", "")
            let rest = new SonarRestService(new JsonSonarConnector())        
            let token = GetConnectionToken(rest, this.SonarHost, this.SonarUserName, this.SonarUserPassword)
            let masterProject = (rest :> ISonarRestService).GetResourcesData(token, key + ":" + parentBranch).[0]
            let branchProject = (rest :> ISonarRestService).GetResourcesData(token, key + ":" + this.Branch).[0]

            if permissiontemplatename <> "" then
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Apply Permission Template ############") 
                HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
                let errormsg = (rest :> ISonarRestService).ApplyPermissionTemplateToProject(token, branchProject.Key, permissiontemplatename)
                if errormsg <> "" then
                    printf "[CxxSonarQubeMsbuidRunner] Failed to apply permission template %s : %s\r\n" permissiontemplatename errormsg
                else
                    printf "[CxxSonarQubeMsbuidRunner] permission template %s : applied correctly to %s \r\n" permissiontemplatename  branchProject.Key

            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Duplicate False Positives ############") 
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##################################################")

            let filter = "?componentRoots=" + masterProject.Key.Trim() + "&resolutions=FALSE-POSITIVE,WONTFIX"
            let falsePositivesInMaster = (rest :> ISonarRestService).GetIssues(token, filter, masterProject.Key)

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
                let issuesInComponentBranch = (rest :> ISonarRestService).GetIssuesInResource(token, comp.Key.Replace(parentBranch, this.Branch))
                
                printf "[CxxSonarQubeMsbuidRunner] : Try to apply false positives in : %s  -> Issues Found : %i \r\n" (comp.Key.Replace(parentBranch, this.Branch)) issuesInComponentBranch.Count

                for issuetochange in comp.Value do
                    let isMatch = List.ofSeq issuesInComponentBranch |> Seq.tryFind (fun c -> c.Rule.Equals(issuetochange.Rule) && c.Line.Equals(issuetochange.Line) && not(c.Resolution.Equals(Resolution.FALSE_POSITIVE)) && not(c.Resolution.Equals(Resolution.WONTFIX)))
                    match isMatch with
                    | Some c -> let issuelist = new System.Collections.Generic.List<Issue>()
                                issuelist.Add(c)
                                let errormsg = 
                                    if issuetochange.Resolution.Equals(Resolution.WONTFIX) then
                                        (rest :> ISonarRestService).MarkIssuesAsWontFix(token, issuelist, "")
                                    else
                                        (rest :> ISonarRestService).MarkIssuesAsFalsePositive(token, issuelist, "")

                                for msg in errormsg do
                                    if msg.Value <> HttpStatusCode.OK then
                                        printf "[CxxSonarQubeMsbuidRunner] Failed mark issue as %s %s %s\r\n" (issuetochange.Resolution.ToString()) msg.Key (msg.Value.ToString())
                                    else
                                        printf "[CxxSonarQubeMsbuidRunner] Issue %s marked as %s\r\n" msg.Key (issuetochange.Resolution.ToString())
                    | _ -> ()
              

                                    
            ()

    member this.ProvisionProject() =
        
        if parentBranch <> "" && this.Branch <> "" then

            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##########################################")
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "########### Provision Project ############") 
            HelpersMethods.cprintf(ConsoleColor.DarkCyan, "##########################################")

            let GetConnectionToken(service : ISonarRestService, address : string , userName : string, password : string) = 
                let pass =
                    if this.SonarUserPassword = "" then
                        "admin"
                    else
                        this.SonarUserPassword

                let user =
                    if this.SonarUserName = "" then
                        "admin"
                    else
                        this.SonarUserName

                let token = new VSSonarPlugins.Types.ConnectionConfiguration(address, user, pass, 4.5)
                token.SonarVersion <- float (service.GetServerInfo(token))
                token

            let rest = new SonarRestService(new JsonSonarConnector())        
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
                    if returndata.Contains("Could not create Project, key already exists:") || returndata = "" then
                        ()                   
                    else                    
                        raise(new Exception("Cannot provision current branch : " + returndata))

                    printf "[CxxSonarQubeMsbuidRunner] New project was provisioned correctly %s \r\n" key
                    new Resource(Key = key + ":" + this.Branch, BranchName = this.Branch)
            
            // duplicate main branch props to branch             
            let propertiesofMainBranch = (rest :> ISonarRestService).GetProperties(token, projectParent.[0])
            printf "[CxxSonarQubeMsbuidRunner] Duplicating %i properties from master\r\n" propertiesofMainBranch.Count
            for prop in propertiesofMainBranch do
                let errormsg = (rest :> ISonarRestService).UpdateProperty(token, prop.Key, prop.Value, branchProject)
                if errormsg <> "" then
                    printf "[CxxSonarQubeMsbuidRunner] Failed to apply prop %s : %s\r\n" prop.Key errormsg

            
            // clean any prop that is not in main
            let propertiesofBranch = (rest :> ISonarRestService).GetProperties(token, branchProject)            
            for prop in propertiesofBranch do
                if propertiesofMainBranch.ContainsKey(prop.Key) && not(propertiesofMainBranch.[prop.Key].Equals(prop.Value)) then
                    let errormsg = (rest :> ISonarRestService).UpdateProperty(token, prop.Key, propertiesofMainBranch.[prop.Key], branchProject)
                    if errormsg <> "" then
                        printf "[CxxSonarQubeMsbuidRunner] Failed to apply updated value from main branch : prop %s : %s\r\n" prop.Key errormsg                        

                if not(propertiesofMainBranch.ContainsKey(prop.Key)) then
                    let errormsg = (rest :> ISonarRestService).UpdateProperty(token, prop.Key, "", branchProject)
                    if errormsg <> "" then
                        printf "[CxxSonarQubeMsbuidRunner] Failed to clear prop %s : %s\r\n" prop.Key errormsg

            // ensure same quality profiles are in used by both branches
            let profiles = (rest :> ISonarRestService).GetQualityProfilesForProject(token, projectParent.[0])
            let profilesBranch = (rest :> ISonarRestService).GetQualityProfilesForProject(token, branchProject)
            let profilesByApi = (rest :> ISonarRestService).GetProfilesUsingRulesApp(token)

            for profile in profiles do
                let branchProfile = List.ofSeq profilesBranch |> Seq.find (fun c -> c.Language.Equals(profile.Language))

                if branchProfile.Name <> profile.Name then
                    let compProfile = List.ofSeq profilesByApi |> Seq.find (fun c -> c.Name.Equals(profile.Name) && c.Language.Equals(profile.Language))                
                    let errormsg = (rest :> ISonarRestService).AssignProfileToProject(token, compProfile.Key, branchProject.Key)
                    if errormsg <> "" then
                        printf "[CxxSonarQubeMsbuidRunner] Failed to apply profile %s : %s\r\n" profile.Name errormsg
                    else
                        printf "[CxxSonarQubeMsbuidRunner] Profile %s : applied correctly\r\n" profile.Name
                else
                    printf "[CxxSonarQubeMsbuidRunner] Profile %s : already correct\r\n" profile.Name

    member this.Setup() =
        // read sonar project files
        if File.Exists(this.DeprecatedPropertiesFile) then
            File.Delete(this.DeprecatedPropertiesFile)

        let cxxClassArguments = GetArgumentClass(this.PropsForBeginStage, this.DepracatedSonarPropsContent, this.HomePath)        
        WriteUserSettingsFromSonarPropertiesFile(this.ConfigFile, this.PropsInSettingsFile, cxxClassArguments)                
        Directory.CreateDirectory(Path.Combine(this.HomePath, ".cxxresults")) |> ignore
        this.BuildLog <- Path.Combine(this.HomePath, ".cxxresults", "BuildLog.txt")

        DeployCxxTargets(this)

    member this.Clean() =
        if this.DepracatedSonarPropsContent <> Array.empty then
            WriteFile(this.DeprecatedPropertiesFile, this.DepracatedSonarPropsContent)

        if File.Exists(this.ConfigFile) then
            File.Delete(this.ConfigFile)

        if File.Exists(this.SolutionTargetFile) then
            File.Delete(this.SolutionTargetFile)

        try
            if Directory.Exists(Path.Combine(this.SonarQubeTempPath, "bin")) then
                Directory.Delete(Path.Combine(this.SonarQubeTempPath, "bin"), true)
        with
        | _ -> printf "Failed to clean target files, compilation might not be possible. Kill any msbuild processes before compilation"