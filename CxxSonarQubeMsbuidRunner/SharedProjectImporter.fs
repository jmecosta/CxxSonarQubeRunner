module SharedProjectImporter

open MSBuildHelper
open System
open System.IO
open FSharp.Data

type SonarAnalysisType = XmlProvider<"""<?xml version="1.0" encoding="utf-8"?>
<ProjectInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">
  <ProjectName>Examples</ProjectName>
  <ProjectLanguage>C#</ProjectLanguage>
  <ProjectType>Product</ProjectType>
  <ProjectGuid>a8f3632e-874e-4f4a-b54f-c7615d031ebc</ProjectGuid>
  <FullPath>E:\prod\fusion\Examples\Windows\Examples.csproj</FullPath>
  <IsExcluded>false</IsExcluded>
  <AnalysisResults>
    <AnalysisResult Id="FilesToAnalyze" Location="E:\prod\fusion\.sonarqube\out\\Examples_AnyCPU_Debug_2418\FilesToAnalyze.txt" />
  </AnalysisResults>
  <AnalysisSettings>
    <Property Name="sonar.cs.roslyn.reportFilePath">E:\prod\fusion\Examples\Windows\bin\SonarQube.Roslyn.ErrorLog.json</Property>
    <Property Name="sonar.stylecop.projectFilePath">E:\prod\fusion\Examples\Windows\Examples.csproj</Property>
  </AnalysisSettings>
</ProjectInfo>
""">

type ProjType = XmlProvider<"""<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
    <HasSharedItems>true</HasSharedItems>
    <SharedGUID>e065249f-95a2-4aa2-8197-3b6bcadb9a39</SharedGUID>
    <ProjectGuid>{80717E1D-76E2-4905-AD85-55AB8B6BE990}</ProjectGuid>
  </PropertyGroup>
  <PropertyGroup Label="Configuration">
    <Import_RootNamespace>Fusion</Import_RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)file.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)file2.cs" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)file.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)file2.cs" />
  </ItemGroup>
  <Import Project="..\File\Whatever.projitems" Condition="Exists('..\FusionCore\Whatever.projitems')" />
  <Import Project="..\File\Whatever.projitems" Label="Shared" Condition="Exists('..\FusionCore\Whatever.projitems')" />
</Project>
""">

type SharedProj() =
    member val ProjectName : string = "" with get, set
    member val Guid : string = "" with get, set
    member val ProjectItemPath : string = "" with get, set
    member val SharedProjPath : string = "" with get, set
    member val ImportItems : string list = List.Empty with get, set
    member val AnalysisFiles : string list = List.Empty with get, set

let ImportSharedProjects(sonarTempPath : string, projectKey : string, solution : ProjectTypes.Solution) =
    let workDir = Path.Combine(sonarTempPath, ".sonarqube", "out")
    let mutable sharedProjects = Map.empty

    for project in solution.Projects do
        printf "%s" project.Value.Path
        if project.Value.Path.EndsWith(".shproj") then
            // load project items
            let projectPath = Directory.GetParent(project.Value.Path).ToString()
            let projectItemsPath = Path.Combine(projectPath, Path.GetFileNameWithoutExtension(project.Value.Path) + ".projitems")

            if File.Exists(projectItemsPath) then
                let sharedProject = ProjType.Parse(File.ReadAllText(projectItemsPath))
                let guid = (sharedProject.PropertyGroups |> Seq.find (fun c -> c.SharedGuid.IsSome)).SharedGuid.Value
                let mutable sharedFiles = List.empty

                for itemGroup in sharedProject.ItemGroups do
                    for compile in itemGroup.Compiles do
                        sharedFiles <- sharedFiles @ [compile.Include.Replace("$(MSBuildThisFileDirectory)", projectPath + "\\")]

                let newit = new SharedProj()
                newit.ImportItems <- sharedFiles
                newit.Guid <- guid.ToString().ToUpper()
                newit.ProjectItemPath <- projectItemsPath
                newit.SharedProjPath <- project.Value.Path
                newit.ProjectName <- project.Value.Name
                sharedProjects <- sharedProjects.Add(projectItemsPath.ToLower().Replace("\\", "/"), newit)

    if sharedProjects.Count > 0 then
        // find projects that have shared refs and add analysis file the shared folder
        let mutable configuration = ""
        let mutable platform = ""
        for project in solution.Projects do
            if project.Value.Path.EndsWith(".csproj") then
                let projectData = ProjType.Parse(File.ReadAllText(project.Value.Path))
                for import in projectData.Imports do
                    if import.Label.IsSome then
                        let pathofsharedproj =
                            if Path.IsPathRooted(import.Project) then
                                import.Project
                            else
                                Path.GetFullPath(Path.Combine(Directory.GetParent(project.Value.Path).ToString(), import.Project))

                        let sharedProj = sharedProjects.[pathofsharedproj.ToLower().Replace("\\", "/")]
                        let guidofprojet = (projectData.PropertyGroups |> Seq.tryFind(fun c -> c.ProjectGuid.IsSome)).Value.ProjectGuid.Value
                        let roslynReport =
                            try
                                
                                let folders = Directory.GetDirectories(workDir)
                                let mutable report = ""
                                for folder in folders do
                                    if folder.Contains(project.Value.Name + "_") then
                                        let splitters = [|project.Value.Name + "_"|]
                                        let namesplit = folder.Split(splitters, StringSplitOptions.RemoveEmptyEntries).[1].Split('_')
                                        platform <- namesplit.[0]
                                        configuration <- namesplit.[1]
                                        let files = Directory.GetFiles(folder)
                                        for file in files do
                                            if file.EndsWith("ProjectInfo.xml") then
                                                let data = SonarAnalysisType.Parse(File.ReadAllText(file))
                                                report <- (data.AnalysisSettings.Properties |> Seq.find (fun c -> c.Name.Equals("sonar.cs.roslyn.reportFilePath"))).Value
                                    
                                report
                            with
                            | _ -> ""

                        if roslynReport <> "" then
                            sharedProj.AnalysisFiles <- sharedProj.AnalysisFiles @ [roslynReport]
                        ()

        // augment properties file with shared projects


        
        for sharedProj in sharedProjects do
            let folderData = Path.Combine(workDir, sharedProj.Value.ProjectName + "_" + platform + "_" + configuration + "_0010")
            let filestoanalystxt = Path.Combine(folderData, "FilesToAnalyze.txt")
            let projConfig = Path.Combine(folderData, "ProjectInfo.xml")
            if Directory.Exists(folderData) then
                Directory.Delete(folderData)

            Directory.CreateDirectory(folderData) |> ignore

            let mutable newContent = List.Empty
            for source in sharedProj.Value.ImportItems do
                newContent <- newContent @ [source]

            File.WriteAllLines(filestoanalystxt, newContent)

            File.WriteAllText(projConfig, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n")
            File.AppendAllText(projConfig, "<ProjectInfo xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"http://www.sonarsource.com/msbuild/integration/2015/1\">\r\n")
            File.AppendAllText(projConfig, (sprintf "  <ProjectName>%s</ProjectName>\r\n" sharedProj.Value.ProjectName))
            File.AppendAllText(projConfig, (sprintf "  <ProjectLanguage>C#</ProjectLanguage>\r\n"))
            File.AppendAllText(projConfig, (sprintf "  <ProjectType>Product</ProjectType>\r\n"))
            File.AppendAllText(projConfig, (sprintf "  <ProjectGuid>%s</ProjectGuid>\r\n" (sharedProj.Value.Guid.ToLower())))
            File.AppendAllText(projConfig, (sprintf "  <FullPath>%s</FullPath>\r\n" sharedProj.Value.SharedProjPath))
            File.AppendAllText(projConfig, (sprintf "  <IsExcluded>false</IsExcluded>\r\n"))
            File.AppendAllText(projConfig, (sprintf "  <AnalysisResults>\r\n"))
            File.AppendAllText(projConfig, (sprintf "    <AnalysisResult Id=\"FilesToAnalyze\" Location=\"%s\" />\r\n" filestoanalystxt))
            File.AppendAllText(projConfig, (sprintf "  </AnalysisResults>\r\n"))
            File.AppendAllText(projConfig, (sprintf "  <AnalysisSettings>\r\n"))

            let reports = sharedProj.Value.AnalysisFiles |> List.fold (fun a b -> a + "," + b) ""
            File.AppendAllText(projConfig, (sprintf "    <Property Name=\"sonar.cs.roslyn.reportFilePath\">%s</Property>\r\n" reports))
            File.AppendAllText(projConfig, (sprintf "    <Property Name=\"sonar.stylecop.projectFilePath\">%s</Property>\r\n" sharedProj.Value.SharedProjPath))
            File.AppendAllText(projConfig, (sprintf "  </AnalysisSettings>\r\n"))
            File.AppendAllText(projConfig, (sprintf "</ProjectInfo>\r\n"))

        ()
                                                


