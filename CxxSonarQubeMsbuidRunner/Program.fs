open System
open System.IO
open System.IO.Compression
open System.Text
open System.Net
open System.Text.RegularExpressions
open System.Diagnostics
open System.Reflection
open MsbuildTasksCommandExecutor
open FSharp.Data

type UserSettingsFileType = XmlProvider<"""
<SonarQubeAnalysisProperties xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">
  <Property Name="sonar.host.url">http://filehost</Property>
  <Property Name="sonar.host.url2">http://filehost2</Property>
</SonarQubeAnalysisProperties>
""">

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

let ShowHelp() =
        Console.WriteLine ("Usage: CxxSonarQubeMsbuidRunner [OPTIONS]")
        Console.WriteLine ("Runs MSbuild Runner with Cxx Support")
        Console.WriteLine ()
        Console.WriteLine ("Options:")
        Console.WriteLine ("    /M|/m:<solution file : mandatory>")
        Console.WriteLine ("    /N|/n:<name : name>")
        Console.WriteLine ("    /K|/k:<key : key>")
        Console.WriteLine ("    /V|/v:<version : version>")
        Console.WriteLine ("    /P|/p:<additional settings for msbuild - /p:Configuration=Release>")
        Console.WriteLine ("    /S|/s:<additional settings filekey>")
        Console.WriteLine ("    /R|/r:<msbuild sonarqueb runner -> 1.0.2>")
        Console.WriteLine ("    /D|/d:<property to pass : /d:sonar.host.url=http://localhost:9000 -> 1.0.2>")
        Console.WriteLine ("    /X|/x:<path for msbuild: default C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe>")
        Console.WriteLine ("    /T|/t:<msbuild target, default is /t:Rebuild>")

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

let DeployCxxTargets(targetFile : string, solutionDir : string, solutionName : string, pythonPath : string, cppCheckPath : string, ratsPath : string, veraPath : string) =
    let assembly = Assembly.GetExecutingAssembly()
    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
    use stream = assembly.GetManifestResourceStream("after.solution.sln.targets")
    use streamreader = new StreamReader(stream)
    let spliters = [|"\r\n"|]
    let content = streamreader.ReadToEnd().Split(spliters, StringSplitOptions.RemoveEmptyEntries)

    use outFile = new StreamWriter(targetFile , false)
    for line in content do        
        let mutable lineToWrite = line
        
        if lineToWrite.Contains("""$(SolutionDir)""") then
            lineToWrite <- lineToWrite.Replace("$(SolutionDir)", solutionDir)

        if lineToWrite.Contains("""$(SolutionName)""") then
            lineToWrite <- lineToWrite.Replace("$(SolutionName)", solutionName)

        if lineToWrite.Contains("""<CppCheckPath               Condition="'$(CppCheckPath)' == ''">C:\Program Files (x86)\Cppcheck\cppcheck.exe</CppCheckPath>""") then
            lineToWrite <- lineToWrite.Replace("C:\\Program Files (x86)\\Cppcheck\\cppcheck.exe", cppCheckPath)

        if lineToWrite.Contains("""<PythonPath                   Condition="'$(PythonPath)' == ''">c:\tools\Python2\Python.exe</PythonPath>""") then
            lineToWrite <- lineToWrite.Replace("c:\\tools\\Python2\\Python.exe", pythonPath)

        if lineToWrite.Contains("""<CppLintPath                   Condition="'$(CppLintPath)' == ''">$(MSBuildThisFileDirectory)\cpplint_mod.py</CppLintPath>""") then
            lineToWrite <- lineToWrite.Replace("$(MSBuildThisFileDirectory)", InstallationModule.InstallationPathHome + "\\")

        if lineToWrite.Contains("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\VERA\bin\vera++.exe""") then
            lineToWrite <- lineToWrite.Replace("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\VERA\bin\vera++.exe""", veraPath)

        if lineToWrite.Contains("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\RATS\rats.exe""") then
            lineToWrite <- lineToWrite.Replace("""$(MSBuildThisFileDirectory)..\MSBuidSonarQube\RATS\rats.exe""", ratsPath)
            
        if lineToWrite.Contains("""$(MSBuildThisFileDirectory)""") then            
            lineToWrite <- lineToWrite.Replace("$(MSBuildThisFileDirectory)", executingPath + "\\")

        outFile.WriteLine(lineToWrite)
    ()

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


[<EntryPoint>]
let main argv = 

    printfn "%A" argv
    let arguments = parseArgs(argv)
    let mutable ret = 0

    try
        if arguments.ContainsKey("h") then
            ShowHelp()
        else
            if not(arguments.ContainsKey("m")) then
                ShowHelp()
                raise(new Exception("/m must be specifed. See /h for complete help"))

            let solution = 
                let data = arguments.["m"] |> Seq.head
                if Path.IsPathRooted(data) then
                    data
                else
                    Path.Combine(Environment.CurrentDirectory, data)

            if not(File.Exists(solution)) then
                ShowHelp()
                raise(new Exception("/m used does not point to existent solution: " + solution))

            // setup properties paths
            let solutionName = Path.GetFileNameWithoutExtension(solution)                                        
            let homePath = Directory.GetParent(solution).ToString()
            let configFile = Path.GetTempFileName()
            let solutionTargetFile = Path.Combine(homePath, "after." + solutionName + ".sln.targets")   
            let deprecatedPropertiesFile = Path.Combine(homePath, "sonar-project.properties")
            let mutable deprecatedPropertiesFileContent = Array.empty

            try         
                let msbuildRunnerExec, (cpplintMod, pythonPath), rats, vera, cppcheck
                        = InstallationModule.InstallTools(arguments)

                // read sonar project files
                if File.Exists(deprecatedPropertiesFile) then
                    deprecatedPropertiesFileContent <- File.ReadAllLines(deprecatedPropertiesFile)
                    File.Delete(deprecatedPropertiesFile)

                let url = 
                    if arguments.ContainsKey("d") then
                        try
                            "/d:" + (arguments.["d"] |> Seq.find(fun c -> c.StartsWith("sonar.host.url=")))
                        with
                        | _ -> "/d:sonar.host.url=" + (GetPropertyFromFile(deprecatedPropertiesFileContent, "host.url"))
                    else
                        let url = (GetPropertyFromFile(deprecatedPropertiesFileContent, "host.url"))
                        if url = "" then
                            "/d:sonar.host.url=http://localhost:9000"
                        else
                            "/d:sonar.host.url=" + url

                let key = 
                    if arguments.ContainsKey("k") then
                        "/k:" + (arguments.["k"] |> Seq.head)
                    else
                        "/k:" + (GetPropertyFromFile(deprecatedPropertiesFileContent, "projectKey"))

                let name = 
                    if arguments.ContainsKey("n") then
                        "/n:" + (arguments.["n"] |> Seq.head)
                    else
                        "/n:" + (GetPropertyFromFile(deprecatedPropertiesFileContent, "projectName"))
        
                let version = 
                    if arguments.ContainsKey("v") then
                        "/v:" + (arguments.["v"] |> Seq.head)
                    else
                        "/v:" + (GetPropertyFromFile(deprecatedPropertiesFileContent, "projectVersion"))

                let msbuildPath = 
                    if arguments.ContainsKey("x") then
                        arguments.["x"] |> Seq.head
                    else
                        @"C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe"

                let msbuildTarget = 
                    if arguments.ContainsKey("t") then
                        "/t:" + (arguments.["t"] |> Seq.head)
                    else
                        "/t:Rebuild"

                let mutable usermame = ""
                let mutable password = ""
                let additionalArgumentsforBeginPhase = 
                    let mutable args = ""
                    if arguments.ContainsKey("d") then
                        for arg in arguments.["d"] do
                            if arg <> "" then                                
                                if arg.StartsWith("sonar.login") then
                                    usermame <- arg.Replace("sonar.login=", "")
                                if arg.StartsWith("sonar.password") then
                                    password <- arg.Replace("sonar.password=", "")

                                if not(arg.StartsWith("sonar.host.url=")) then
                                    args <- args + " /d:" + arg

                    args.Trim() + " " + url 

                let additionalArgumentsForMsbuild = 
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

                // update sync file
                let configurationInProps = 
                    if arguments.ContainsKey("s") then
                
                        let configFile = arguments.["s"] |> Seq.head
                        let elements = UserSettingsFileType.Parse(configFile)
                        let elems = (List.ofSeq elements.Properties)
                        elements.Properties |> Seq.map (fun f -> f.Name, f.Value) |> Map.ofSeq
                    else
                        Map.empty

                let cxxClassArguments = GetArgumentClass(additionalArgumentsforBeginPhase, deprecatedPropertiesFileContent, homePath)
        
                WriteUserSettingsFromSonarPropertiesFile(configFile, configurationInProps, cxxClassArguments)
                
                Directory.CreateDirectory(Path.Combine(homePath, ".cxxresults")) |> ignore

                let allArguments = key + " " + name + " " + version + " " + additionalArgumentsforBeginPhase + " " + "/s:" + configFile
                if SonarRunnerPhases.BeginPhase(msbuildRunnerExec, allArguments, homePath, usermame, password) <> 0 then
                    ret <- 1
                    printf "Failed to execute Begin Phase, check log"
                else
                    let targetFile = Path.Combine(homePath, ".sonarqube", "bin", "Targets", "SonarQube.Integration.targets")
                    PatchMSbuildSonarRunnerTargetsFiles(targetFile)
                
                    DeployCxxTargets(solutionTargetFile, homePath, solutionName, pythonPath, cppcheck, rats, vera)

                    if SonarRunnerPhases.RunBuild(msbuildPath, solution, additionalArgumentsForMsbuild + " " + msbuildTarget, Path.Combine(homePath, ".cxxresults", "BuildLog.txt"), Path.Combine(homePath, ".sonarqube"), homePath) <> 0 then
                        ret <- 1
                        printf "Failed to build project, check log"
                    else
                        if SonarRunnerPhases.EndPhase(msbuildRunnerExec, usermame, password, homePath) <> 0 then
                            ret <- 1
                            printf "Failed analyse project, check log"            
            with
            | ex ->
                printf "Exception During Run: %s \r\n %s" ex.Message ex.StackTrace            
                ret <- 1

            if File.Exists(configFile) then
                File.Delete(configFile)

            if File.Exists(solutionTargetFile) then
                File.Delete(solutionTargetFile)

            if deprecatedPropertiesFileContent <> Array.empty then
                WriteFile(deprecatedPropertiesFile, deprecatedPropertiesFileContent)

        with
        | ex ->
            printf "Exception During Run: %s" ex.Message
            ret <- 1
        
    ret
