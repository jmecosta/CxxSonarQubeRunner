module InstallationModule

open System.IO
open System
open System.Net
open System.IO.Compression
open MsbuildTasksCommandExecutor
open System.Diagnostics
open Microsoft.Win32

let ChocoExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "choco.exe")
let InstallationPathHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MSBuidSonarQube")
let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()

let GetPythonPath() = 
    try
        let mutable pathdata = ""
        let PYTHON_KEY = "SOFTWARE\\Python\\PythonCore\\2.7\\InstallPath"

        let localKey32 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry32)
        let localKey64 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)

        use rk = localKey32.OpenSubKey(PYTHON_KEY)
        if rk <> null then
            pathdata <- rk.GetValue("").ToString()
        else                
            use rk = localKey64.OpenSubKey(PYTHON_KEY)
            if rk <> null then
                pathdata <- rk.GetValue("").ToString()

        pathdata
    with
    | ex -> ""

let GetCppCheckPath() = 
    if File.Exists(@"C:\Program Files (x86)\Cppcheck\cppcheck.exe") then
        @"C:\Program Files (x86)\Cppcheck\cppcheck.exe"
    elif File.Exists(@"C:\Program Files\Cppcheck\cppcheck.exe") then
        @"C:\Program Files\Cppcheck\cppcheck.exe"
    else
        @"C:\Program Files (x86)\Cppcheck\cppcheck.exe"


let DownloadFileToPath(url : string, path : string) = 
    let wc = new WebClient()
    wc.DownloadFile(url, path)   

let UnzipFileToFolder(file:string, path:string) = 
    ZipFile.ExtractToDirectory(file, path)

let InstallPackage(file:string) = 
    let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data

    let executor = new CommandExecutor(null, int64(1500000))
    printf  "[Install] : %s /s\r\n" file
    let returncode = (executor :> ICommandExecutor).ExecuteCommand(Path.Combine(executingPath, "Elevate.exe"),"-wait4exit -noui " + file + " /s", Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, Environment.CurrentDirectory)
    returncode

let InstallChocoPackage(package : string) = 
    let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data
    
    let executor = new CommandExecutor(null, int64(1500000))
    printf  "[Install] : %s install %s -y\r\n" ChocoExe package
    (executor :> ICommandExecutor).ExecuteCommand(Path.Combine(executingPath, "Elevate.exe"),"-wait4exit -noui " + ChocoExe + " install " + package + " -y", Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, Environment.CurrentDirectory)
    
let InstallChocolatey() =
    let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data
    
    if not(File.Exists(ChocoExe)) then
        let executor = new CommandExecutor(null, int64(1500000))
        printf  """[Install] : @powershell -NoProfile -ExecutionPolicy unrestricted -Command \"(iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1')))"""
        (executor :> ICommandExecutor).ExecuteCommand("cmd.exe","/c @powershell -NoProfile -ExecutionPolicy unrestricted -Command \"(iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1'))) >$null 2>&1\" && SET PATH=%PATH%;%ALLUSERSPROFILE%\chocolatey\bin\"", Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, Environment.CurrentDirectory) |> ignore

let DownloadAndInstallZipDist(url : string, swName : string) = 
    let installPath = Path.Combine(InstallationPathHome, swName)

    if not(Directory.Exists(installPath)) then
        Directory.CreateDirectory(installPath) |> ignore
        let tmpPath  = Path.GetTempFileName()
        let wc = new WebClient()
        wc.DownloadFile(url, tmpPath)        
        UnzipFileToFolder(tmpPath, installPath) 
        File.Delete(tmpPath)

    installPath

let DownloadAndInstallMSIDist(url : string, installationPath : string) = 
    if not(Directory.Exists(installationPath)) then
        let tmpPath  = Path.GetTempFileName() + ".exe"
        let wc = new WebClient()
        wc.DownloadFile(url, tmpPath)        
        InstallPackage(tmpPath) |> ignore
        File.Delete(tmpPath)

let InstallMsbuildRunner(version : string) =
    let downloadUrl = sprintf """https://github.com/SonarSource/sonar-msbuild-runner/releases/download/%s/MSBuild.SonarQube.Runner-%s.zip""" version version
    Path.Combine(DownloadAndInstallZipDist(downloadUrl, version), "MSBuild.SonarQube.Runner.exe")

let InstallCppLint() =
    let mutable pythonPath = Path.Combine(GetPythonPath(), "python.exe")
    if not(File.Exists(pythonPath)) then
        if InstallChocoPackage("python2") <> 0 then
            printf "[Install] Failed to install python, likely it will fail. please install manually python to c:\tools\python2"
        else
            pythonPath <- "c:\\tools\python2\\python.exe"

    let cpplintMod = Path.Combine(InstallationPathHome, "cpplint_mod.py")
    let wc = new WebClient()
    wc.DownloadFile("""https://raw.githubusercontent.com/SonarOpenCommunity/sonar-cxx-msbuild-tasks/master/Nuget/CppLint/cpplint_mod.py""", cpplintMod)
    cpplintMod, pythonPath

let InstallRats() =    
    Path.Combine(DownloadAndInstallZipDist("""https://github.com/SonarOpenCommunity/sonar-cxx-msbuild-tasks/raw/master/Nuget/rats.zip""", "RATS"), "rats.exe")

let InstallVera() =
    Path.Combine(DownloadAndInstallZipDist("""https://github.com/SonarOpenCommunity/sonar-cxx-msbuild-tasks/raw/master/Nuget/vera.zip""", "VERA"), "bin", "vera++.exe")

let InstallCppCheck() =
    if not(File.Exists(GetCppCheckPath())) then
        if InstallChocoPackage("cppcheck") <> 0 then           
            printf "[Install] Failed to install cppcheck, likely it will fail. please install manually python to C:\Program Files (x86)\Cppcheck"
        
    GetCppCheckPath()


let InstallTools(arguments : Map<string,seq<string>>) =
    let msbuildRunnerVersion = 
        if arguments.ContainsKey("r") then
            arguments.["r"] |> Seq.head
        else
            "1.0.2"
                
    InstallChocolatey()
    InstallMsbuildRunner(msbuildRunnerVersion),
    InstallCppLint(),
    InstallRats(),
    InstallVera(),
    InstallCppCheck()


