module InstallationModule

open System.IO
open System
open System.Net
open System.IO.Compression
open Microsoft.Win32

let ChocoExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "choco.exe")
let InstallationPathHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MSBuidSonarQube")
let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
let runnerRootPath = Directory.GetParent(executingPath).FullName

let GetPythonPath() = Path.Combine(runnerRootPath, "Python27")

let GetCppCheckPath() = 
    if File.Exists(@"C:\Program Files (x86)\Cppcheck\cppcheck.exe") then
        @"C:\Program Files (x86)\Cppcheck\cppcheck.exe"
    elif File.Exists(@"C:\Program Files\Cppcheck\cppcheck.exe") then
        @"C:\Program Files\Cppcheck\cppcheck.exe"
    elif File.Exists(@"C:\Program Files (x86)\Cppcheck\cppcheck.exe") then
        @"C:\Program Files (x86)\Cppcheck\cppcheck.exe"
    else
        Path.Combine(runnerRootPath, "Cppcheck", "cppcheck.exe")

let UnzipFileToFolder(file:string, path:string) = 
    ZipFile.ExtractToDirectory(file, path)

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

let InstallPython() = Path.Combine(GetPythonPath(), "python.exe")
let InstallCppLint() = Path.Combine(runnerRootPath, "CppLint",  "cpplint_mod.py")
let InstallRats() =  Path.Combine(runnerRootPath, "rats", "rats.exe")
let InstallVera() = Path.Combine(runnerRootPath, "VERA", "bin", "vera++.exe")
let InstallCppCheck() = GetCppCheckPath()



