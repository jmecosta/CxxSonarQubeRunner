namespace MsbuildUtilityHelpers

open System
open System.IO

type ProjectFiles(projectName : string, absolutepath : string) = 
    member val name = projectName with get
    member val path = absolutepath with get

type VSSolutionUtils() =
    let Readlines solutionPath = File.ReadLines(solutionPath)

    member this.GetProjectFilesFromSolutions(solutionPath : string) =
        let mutable projectFiles : ProjectFiles list = []
        for line in Readlines(solutionPath) do
            if line <> null then
                if line.StartsWith("Project(\"{") then
                    let projectName = line.Split(',').[0].Split('=').[1].Replace("\"","").Trim()
                    let projectRelativePath = line.Split(',').[1].Replace("\"","").Trim()
                    let path = Path.Combine(Directory.GetParent(solutionPath).ToString(), projectRelativePath)
                    if File.Exists(path) then
                        projectFiles <- projectFiles @ [new ProjectFiles(projectName, path)]
        projectFiles

type VSProjectUtils() = 
    let Readlines solutionPath = File.ReadLines(solutionPath)

    member this.GetCompilationFiles(projectFile : string, hasString : string, pathReplaceStrings : string) =
        let mutable files : string list = []
        let str = String.Concat(Readlines(projectFile))
        let data = CppProjectFile.Parse(str)

        for item in data.ItemGroups do

            let filterAndChangeStringsInFile(file : string) = 
                let mutable fileFinal = ""

                if String.IsNullOrEmpty(hasString) then
                    fileFinal <- file
                else
                    for elem in hasString.Split(';') do
                        if file.ToLowerInvariant().Contains(elem.ToLowerInvariant()) then
                            fileFinal <- file

                if not(String.IsNullOrEmpty(pathReplaceStrings)) then
                    fileFinal <- Utils().ProcessFileUsingReplacementStrings(fileFinal, pathReplaceStrings)

                fileFinal

            for source in item.ClCompiles do
                let validFile = filterAndChangeStringsInFile source.Include
                if not(String.IsNullOrEmpty(validFile)) then
                    if Path.IsPathRooted(validFile) then
                        files <- files @ [validFile]
                    else
                        files <- files @ [Path.Combine(Directory.GetParent(projectFile).ToString(), validFile)]
                          

            for headers in item.ClIncludes do
                let validFile = filterAndChangeStringsInFile headers.Include
                if not(String.IsNullOrEmpty(validFile)) then
                    if Path.IsPathRooted(validFile) then
                        files <- files @ [validFile]                        
                    else
                        files <- files @ [Path.Combine(Directory.GetParent(projectFile).ToString(), validFile)]

            for cssource in item.Compiles do
                let validFile = filterAndChangeStringsInFile cssource.Include
                if not(String.IsNullOrEmpty(validFile)) then
                    if Path.IsPathRooted(validFile) then
                        files <- files @ [validFile]
                    else
                        files <- files @ [Path.Combine(Directory.GetParent(projectFile).ToString(), validFile)]

        files
