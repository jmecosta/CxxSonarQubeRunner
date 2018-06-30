module InstallationModule

open System.IO
open System
open System.Net
open System.IO.Compression
open MsbuildUtilityHelpers
open System.Diagnostics
open Microsoft.Win32

let ChocoExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "choco.exe")
let InstallationPathHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MSBuidSonarQube")
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
    printf  "[Install] : %s %s %s /s\r\n" ("\"" + Path.Combine(executingPath, "Elevate.exe") + "\"") ("-wait4exit -noui") file
    let returncode = (executor :> ICommandExecutor).ExecuteCommand("\"" + Path.Combine(executingPath, "Elevate.exe") + "\"","-wait4exit -noui " + file + " /s", Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, Environment.CurrentDirectory)
    returncode

let InstallChocoPackage(package : string) = 
    let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data
    
    let executor = new CommandExecutor(null, int64(1500000))
    printf  "[Install] : %s %s %s install '%s' -y\r\n" ("\"" + Path.Combine(executingPath, "Elevate.exe") + "\"") ("-wait4exit -noui") ChocoExe package
    (executor :> ICommandExecutor).ExecuteCommand("\"" + Path.Combine(executingPath, "Elevate.exe") + "\"","-wait4exit -noui " + ChocoExe + " install " + package + " -y", Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, Environment.CurrentDirectory)
    
let InstallChocolatey() =
    let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            printf  "%s\r\n" e.Data
    
    if not(File.Exists(ChocoExe)) then
        let executor = new CommandExecutor(null, int64(1500000))
        printf  """[Install] : @powershell -NoProfile -ExecutionPolicy unrestricted -Command \"(iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1')))\r\n"""
        (executor :> ICommandExecutor).ExecuteCommand("cmd.exe","/c @powershell -NoProfile -ExecutionPolicy unrestricted -Command \"(iex ((new-object net.webclient).DownloadString('https://chocolatey.org/install.ps1'))) >$null 2>&1\" && SET PATH=%PATH%;%ALLUSERSPROFILE%\chocolatey\bin\"", Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, Environment.CurrentDirectory) |> ignore

let DownloadAndInstallZipDist(url : string, swName : string) = 
    let installPath = Path.Combine(InstallationPathHome, swName)

    try
        if not(Directory.Exists(installPath)) then
            Directory.CreateDirectory(installPath) |> ignore
        let tmpPath  = Path.GetTempFileName()
        ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12
        let wc = new WebClient()
        wc.DownloadFile(url, tmpPath)
        UnzipFileToFolder(tmpPath, installPath) 
        File.Delete(tmpPath)
    with
    | ex -> HelpersMethods.cprintf(ConsoleColor.Cyan, "Failed to download url: " + ex.Message)

    installPath

let DownloadAndInstallMSIDist(url : string, installationPath : string) = 
    if not(Directory.Exists(installationPath)) then
        let tmpPath  = Path.GetTempFileName() + ".exe"
        let wc = new WebClient()
        wc.DownloadFile(url, tmpPath)        
        InstallPackage(tmpPath) |> ignore
        File.Delete(tmpPath)

let InstallMsbuildRunner(version : string) =
    let exePath = Path.Combine(InstallationPathHome, version, "MSBuild.SonarQube.Runner.exe")
    let exePathNew = Path.Combine(InstallationPathHome, version, "SonarQube.Scanner.MSBuild.exe")
    let exePathNewNew = Path.Combine(InstallationPathHome, version, "SonarScanner.MSBuild.exe")

    if File.Exists(exePathNewNew) then
        exePathNewNew
    elif File.Exists(exePathNew) then
        exePathNew
    elif File.Exists(exePath) then
        exePath
    else
        if Directory.Exists(Path.Combine(InstallationPathHome, version)) then
            Directory.Delete(Path.Combine(InstallationPathHome, version), true)

        printf "Download and install msbuild runner from github, make sure you have access to Internet or use settings in home folder to specify a external location\r\n";

        let downloadUrl = sprintf """https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/%s/sonar-scanner-msbuild-%s-net46.zip""" version version
        printf "Download %s\r\n" downloadUrl
        let mutable exe = Path.Combine(DownloadAndInstallZipDist(downloadUrl, version), "SonarScanner.MSBuild.exe")

        if not(File.Exists(exe)) then
            let downloadUrl = sprintf """https://github.com/SonarSource/sonar-msbuild-runner/releases/download/%s/MSBuild.SonarQube.Runner-%s.zip""" version version
            printf "Download %s\r\n" downloadUrl
            exe <- Path.Combine(DownloadAndInstallZipDist(downloadUrl, version), "MSBuild.SonarQube.Runner.exe")

        if not(File.Exists(exe)) then
            let downloadUrl = sprintf """https://github.com/SonarSource/sonar-msbuild-runner/releases/download/%s/sonar-scanner-msbuild-%s.zip""" version version
            printf "Download %s\r\n" downloadUrl
            exe <- Path.Combine(DownloadAndInstallZipDist(downloadUrl, version), "MSBuild.SonarQube.Runner.exe")

        if not(File.Exists(exe)) then
            let downloadUrl = sprintf """https://github.com/SonarSource/sonar-msbuild-runner/releases/download/%s/MSBuild.SonarQube.Runner.%s.zip""" version version
            printf "Download %s\r\n" downloadUrl
            exe <- Path.Combine(DownloadAndInstallZipDist(downloadUrl, version), "MSBuild.SonarQube.Runner.exe")

        if not(File.Exists(exe)) then
            raise (new Exception("Unable to download and unzip file from github : " + downloadUrl))
        exe

let InstallPython() =
    let mutable pythonPath = Path.Combine(GetPythonPath(), "python.exe")
    if not(File.Exists(pythonPath)) then
        if InstallChocoPackage("python2") <> 0 then
            printf "[Install] Failed to install python, likely it will fail. please install manually python to 'c:/tools/python2' \r\n"
            pythonPath
        else
            printf "[Install] Python available in %s\r\n" pythonPath
            "c:\\tools\python2\\python.exe"
    else
        printf "[Install] python installed already in %s\r\n" pythonPath
        pythonPath

let InstallCppLint() =
    let cpplintMod = Path.Combine(InstallationPathHome, "cpplint_mod.py")
    if not(File.Exists(cpplintMod)) then
        printf "[Install] Download patched version of cpplint\r\n"
        let wc = new WebClient()
        wc.DownloadFile("""https://raw.githubusercontent.com/SonarOpenCommunity/sonar-cxx-msbuild-tasks/master/Nuget/CppLint/cpplint_mod.py""", cpplintMod)
    else
        printf "[Install] cpplint installed already in %s\r\n" cpplintMod
    cpplintMod

let InstallRats() = 
    let ratsPath = Path.Combine(InstallationPathHome, "RATS", "rats.exe")
    if not(File.Exists(ratsPath)) then
        printf "[Install] Download version of rats\r\n"
        Path.Combine(DownloadAndInstallZipDist("""https://github.com/SonarOpenCommunity/sonar-cxx-msbuild-tasks/raw/master/Nuget/rats.zip""", "RATS"), "rats.exe")
    else
        printf "[Install] rats installed already in %s\r\n" ratsPath
        ratsPath

let InstallVera() =
    let veraPath = Path.Combine(InstallationPathHome, "VERA", "bin", "vera++.exe")
    if not(File.Exists(veraPath)) then
        printf "[Install] Download version of vera\r\n"
        Path.Combine(DownloadAndInstallZipDist("""https://github.com/SonarOpenCommunity/sonar-cxx-msbuild-tasks/raw/master/Nuget/vera.zip""", "VERA"), "bin", "vera++.exe")
    else
        printf "[Install] vera installed already in %s\r\n" veraPath
        veraPath

let InstallCppCheck() =
    let cppCheckPath = GetCppCheckPath()
    if not(File.Exists(cppCheckPath)) then
        if InstallChocoPackage("cppcheck") <> 0 then
            printf "[Install] Failed to install cppcheck, likely it will fail. please install manually python to 'C:/Program Files (x86)/Cppcheck'\r\n"
            cppCheckPath
        else
            printf "[Install] cppcheck installed correctly in %s\r\n" (GetCppCheckPath())
            GetCppCheckPath()
    else
        printf "[Install] cppcheck installed already in %s\r\n" cppCheckPath
        cppCheckPath



