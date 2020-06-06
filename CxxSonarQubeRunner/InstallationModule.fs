module InstallationModule

open System.IO
open System
open System.Net
open System.IO.Compression
open Microsoft.Win32
open System.Runtime.InteropServices
open MsbuildUtilityHelpers

let ChocoExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "choco.exe")
let InstallationPathHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MSBuidSonarQube")
let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
let runnerRootPath = Directory.GetParent(executingPath).FullName
let isWindowSystem = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
let isMacSystem = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
let isLinuxSystem = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
let osNameAndVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription

let UnixDist = 
    if isLinuxSystem then
        let release = Directory.GetFiles(Path.Combine("/etc"), "os-release").[0]
        let lineName = File.ReadAllLines(release) |> Seq.tryFind (fun line -> line.Contains("NAME="))
        lineName.Value.Split('=').[1].Trim('\"')
    else
        ""

HelpersMethods.cprintf(ConsoleColor.DarkMagenta, sprintf "OS Arch: %A\r" System.Runtime.InteropServices.RuntimeInformation.OSArchitecture)
HelpersMethods.cprintf(ConsoleColor.DarkMagenta, sprintf "OS FrameDescription: %A\r" System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)
HelpersMethods.cprintf(ConsoleColor.DarkMagenta, sprintf "OS: %A\r" System.Runtime.InteropServices.RuntimeInformation.OSDescription)
if isLinuxSystem then
    HelpersMethods.cprintf(ConsoleColor.DarkMagenta, sprintf "UnixDist: %A\r" UnixDist)

if isMacSystem then
    HelpersMethods.cprintf(ConsoleColor.DarkMagenta, sprintf "OsDist: %A\r" UnixDist)


let executor = new CommandExecutor(null, int64(1500000))

let GetPythonPath() =
    if isWindowSystem then
        Path.Combine(Path.Combine(runnerRootPath, "Tools", "Python27"), "python.exe")
    else
        try
            let ret = (executor :> ICommandExecutor).ExecuteCommandWait("python", "-V", Map.empty, Environment.CurrentDirectory)

            if ret = 0 then
                HelpersMethods.cprintf(ConsoleColor.Green, sprintf "Python is available: %s\r" "python")
                "python"
            else
                raise (new Exception(sprintf "Python not installed in Machine, install python using linux package manager, yum, apt, brew: return code %i\r" ret))
                ""
        with
        | ex -> raise (new Exception(sprintf "Python not installed in Machine, install python using linux package manager, yum, apt, brew: return code %s\r" ex.Message))
                ""

let GetVeraPath() =
    if isWindowSystem then
        Path.Combine(runnerRootPath, "Tools", "VERA", "bin", "vera++.exe")
    else
        if not(UnixDist.Contains("Ubuntu")) then
            HelpersMethods.cprintf(ConsoleColor.Red, sprintf "Vera++ not aviable in non Ubuntu: %A\r" System.Runtime.InteropServices.RuntimeInformation.OSDescription)
            ""
        else
            try
                let ret = (executor :> ICommandExecutor).ExecuteCommandWait("vera++", "--version", Map.empty, Environment.CurrentDirectory)

                if ret = 0 then
                    HelpersMethods.cprintf(ConsoleColor.Green, sprintf "Vera is available: %s\r" "vera++")
                    "vera++"
                 else
                    raise (new Exception(sprintf "Vera++ not installed in Machine, install Vera with 'brew install vera++' or 'apt install vera++': return code %i\r" ret))
                    ""

            with
            | ex -> raise (new Exception(sprintf "Vera++ not installed in Machine, install Vera with 'brew install vera++' or 'apt install vera++': error message %s\r" ex.Message))
                    ""

let GetRatsPath() =
    if isWindowSystem then
        Path.Combine(runnerRootPath, "Tools", "rats", "rats.exe")
    else
        if isLinuxSystem  then
            HelpersMethods.cprintf(ConsoleColor.Red, sprintf "Rats not aviable in Unix: %A\r" System.Runtime.InteropServices.RuntimeInformation.OSDescription)
            ""
        else
            try
                let ret = (executor :> ICommandExecutor).ExecuteCommandWait("rats", "", Map.empty, Environment.CurrentDirectory)

                if ret = 0 then
                    "rats"
                 else
                    raise (new Exception(sprintf "rats not installed in Machine, install Vera with 'brew install rats': return code %i\r" ret))
                    ""

            with
            | ex -> raise (new Exception(sprintf "rats not installed in Machine, install rats with 'brew install rats': error message %s\r" ex.Message))
                    ""

let GetCppCheckPath() = 
    if isWindowSystem then 
        if File.Exists(Path.Combine(runnerRootPath, "Tools", "Cppcheck", "cppcheck.exe")) then
            Path.Combine(runnerRootPath, "Tools", "Cppcheck", "cppcheck.exe")
        elif File.Exists(@"C:\Program Files (x86)\Cppcheck\cppcheck.exe") then
            @"C:\Program Files (x86)\Cppcheck\cppcheck.exe"
        elif File.Exists(@"C:\Program Files\Cppcheck\cppcheck.exe") then
            @"C:\Program Files\Cppcheck\cppcheck.exe"
        else
            Path.Combine(runnerRootPath, "Tools", "Cppcheck", "cppcheck.exe")
    else
        try
            let ret = (executor :> ICommandExecutor).ExecuteCommandWait("cppcheck", "--version", Map.empty, Environment.CurrentDirectory)

            if ret = 0 then
                HelpersMethods.cprintf(ConsoleColor.Green, sprintf "CppCheck is available: %s\r" "cppcheck")
                "cppcheck"
             else
                raise (new Exception(sprintf "cppcheck not installed in Machine http://cppcheck.sourceforge.net/, install python using linux package manager, yum, apt, brew: return code %i\r" ret))
                ""
        with
        | ex -> raise (new Exception(sprintf "cppcheck not installed in Machine http://cppcheck.sourceforge.net/, install python using linux package manager, yum, apt, brew: error Message %s\r" ex.Message))
                ""

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

let InstallMsbuildRunnerWindows(version : string) =
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

let InstallSonarScannerForLinux(version : string) =
    let versionPath =
        if isLinuxSystem then
            sprintf "sonar-scanner-%s-linux" version
        else
            sprintf "sonar-scanner-%s-macosx" version

    let exePath = Path.Combine(InstallationPathHome, version, versionPath, "bin", "sonar-scanner")
    let javaPath = Path.Combine(InstallationPathHome, version, versionPath, "jre", "bin", "java")
    
    if File.Exists(exePath) then
        exePath
    else
        if Directory.Exists(Path.Combine(InstallationPathHome, version)) then
            Directory.Delete(Path.Combine(InstallationPathHome, version), true)

        
        let downloadUrl = 
            if isLinuxSystem then
                printf "Download and install cli scanner for linux in binaries, make sure you have access to Internet or use settings in home folder to specify a external location\r\n";
                sprintf """https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/sonar-scanner-cli-%s-linux.zip""" version
            else
                printf "Download and install cli scanner for osx in binaries, make sure you have access to Internet or use settings in home folder to specify a external location\r\n";
                sprintf """https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/sonar-scanner-cli-%s-macosx.zip""" version

        printf "Download %s\r\n" downloadUrl
        let mutable exe = Path.Combine(DownloadAndInstallZipDist(downloadUrl, version), versionPath, "bin", "sonar-scanner")

        if not(File.Exists(exe)) then
            raise (new Exception("Unable to download and unzip file from sonarsource : " + downloadUrl))

        try
            (executor :> ICommandExecutor).ExecuteCommandWait("chmod", "+x " + exePath, Map.empty, Environment.CurrentDirectory) |> ignore
        with
        | ex -> raise (new Exception(sprintf "cannot change run permssions for %s %s\r" exePath ex.Message))

        try
            (executor :> ICommandExecutor).ExecuteCommandWait("chmod", "+x " + javaPath, Map.empty, Environment.CurrentDirectory) |> ignore
        with
        | ex -> raise (new Exception(sprintf "cannot change run permssions for %s %s\r" javaPath ex.Message))


        exe

let InstallPython() = GetPythonPath()
let InstallCppLint() = Path.Combine(runnerRootPath, "Tools", "CppLint",  "cpplint_mod.py")
let InstallRats() =  GetRatsPath()
let InstallVera() = GetVeraPath()
let InstallCppCheck() = GetCppCheckPath()
let InstallScannerRunner(msbuildversion:string,cliscanner:string) = 
    if isWindowSystem then
        InstallMsbuildRunnerWindows(msbuildversion)
    else
        InstallSonarScannerForLinux(cliscanner)


