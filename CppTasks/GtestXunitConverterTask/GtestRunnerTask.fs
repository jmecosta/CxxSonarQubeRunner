// Learn more about F# at http://fsharp.net

namespace GtestRunnerMSBuildTask
#if INTERACTIVE
#r "Microsoft.Build.Framework.dll";;
#r "Microsoft.Build.Utilities.v4.0.dll";;
#endif

open FSharp.Data
open System
open System.IO
open System.Xml
open System.Xml.Linq
open System.Diagnostics
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open Microsoft.Win32
open MsbuildUtilityHelpers

type GtestXmlReport = XmlProvider<"""<?xml version="1.0" encoding="UTF-8"?>
<testsuites tests="43" failures="0" disabled="2" errors="0" timestamp="2013-06-29T09:23:30" time="0.348" name="AllTests">
  <testsuite name="MD_GetMac" tests="1" failures="0" disabled="0" errors="0" time="0">
    <testcase name="ReturnsMacAddress" status="notrun" time="0" classname="MD_GetMac" /> 
  </testsuite>
  <testsuite name="MD_coPolygonCutTest" tests="4" failures="1" disabled="0" errors="0" time="0">
    <testcase name="GetObjectType" status="run" time="0" classname="MD_coPolygonCutTest" />
    <testcase name="GetObjectClass" status="run" time="0" classname="MD_coPolygonCutTest" />
    <testcase name="GetPartForm" status="run" time="0" classname="MD_coPolygonCutTest" />
    <testcase name="SetUIInputForPolygonCut" status="run" time="0" classname="MD_coPolygonCutTest">
      <failure message="e:\prod\structures\src\work\core\model\libco_classes\test\co_polygon_cut_test.cpp:73&#x0A;Value of: RefCoordsys[3]&#x0A;Expected: Vector Check Failed&#x0A;  Actual: 24-byte object &lt;CC-CC CC-CC CC-CC CC-CC 01-CC CC-CC 00-00 00-00 CC-CC CC-CC 00-00 00-00&gt; (of type class Vector_c)&#x0A;Incorrect coordsys after SetUIInputItem" type=""><![CDATA[e:\prod\structures\src\work\core\model\libco_classes\test\co_polygon_cut_test.cpp:73
Value of: RefCoordsys[3]
Expected: Vector Check Failed
  Actual: 24-byte object <CC-CC CC-CC CC-CC CC-CC 01-CC CC-CC 00-00 00-00 CC-CC CC-CC 00-00 00-00> (of type class Vector_c)
Incorrect coordsys after SetUIInputItem]]></failure>
    </testcase>
  </testsuite>
  <testsuite name="CM_toolFileSystemTest" tests="34" failures="0" disabled="0" errors="0" time="0.128">
    <testcase name="DISABLED_TT80570_TestgeoSolveExtremaSpatialRelation_5" status="notrun" time="0" classname="CM_geoExtremaTest" />
    <testcase name="DISABLED_TT80570_TestgeoSolveExtremaSpatialRelation_6" status="notrun" time="0" classname="CM_geoExtremaTest" />
    <testcase name="testWLowerCase" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testLowerCase" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGets_FilesWithSuffixFromSubFolder" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFilesFromSubFolderWhenMainDirIsCached" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemDoesNotReturnDuplicateFileNames" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemDoesNotCareAboutExcludeList" status="run" time="0.004" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGets_FilesWithSuffixFromSubFolderAfterAddingFiles" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsThreeFilesAfterClearingCache" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="DoesNotReturnFiles_FromNonExistentFolder" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="GetsFilesFromMainFolder_WithJustMainDirectory" status="run" time="0.006" classname="CM_toolFileSystemTest" />
    <testcase name="GetsFilesFromMainFolder_WhenSubFolderDoesNotExist" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="GetsFileFullPath_WhenSubFolderDoesNotExist" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFilesFromMainFolderIfSubFolderDoesNotExist_WithDifferentCase" status="run" time="0.004" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFilesByPrefixAndSuffix_DoesNotLoseCaseOfFilenameCharacters" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFilesByPrefixAndSuffix" status="run" time="0.009" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFilesByPrefix" status="run" time="0.012" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFilesByPrefixAndSuffix_WithDifferentCase" status="run" time="0.004" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFilesByPrefixInDifferentCase" status="run" time="0.006" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFileFullPath" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFileFullPath_WithExactNameOnly" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFileFullPathFromSubFolder" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFileFullPath_NonExistingSubFolder" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFileFullPath_FileNotInCache" status="run" time="0.004" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFileFullPath_FileInCacheWasRemoved" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testFileSystemGetsFileFullPath_FileWithSameNameWasCreatedInHigherPriorityFolder" status="run" time="0.004" classname="CM_toolFileSystemTest" />
    <testcase name="testClearDirectoryCache_AllowsNewlyCreatedFilesToBeFetched" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testClearDirectoryCache_CanEmptyDirectory" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testFileExists_FileExists" status="run" time="0.003" classname="CM_toolFileSystemTest" />
    <testcase name="testFileExists_FileDoesNotExist" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testInsertInCache_FilesExistInFolder" status="run" time="0.009" classname="CM_toolFileSystemTest" />
    <testcase name="testInsertInCache_EmptyFolder" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="testInsertInCache_ExistingFile" status="run" time="0.008" classname="CM_toolFileSystemTest" />
    <testcase name="testInsertInCache_FileDoesNotExist" status="run" time="0.002" classname="CM_toolFileSystemTest" />
    <testcase name="GetFileFullPath_ShouldNotReturnNullDString_c" status="run" time="0.003" classname="CM_toolFileSystemTest" />
  </testsuite>
  <testsuite name="CM_toolFileSystemJapaneseTest" tests="6" failures="4" disabled="0" errors="0" time="0.021">
    <testcase name="testFileSystemGets_FilesWithSuffixFromSubFolder" status="run" time="0.004" classname="CM_toolFileSystemJapaneseTest" />
    <testcase name="testFileSystem_GetFullPathTo" status="run" time="0.004" classname="CM_toolFileSystemJapaneseTest" />
    <testcase name="testTransform" status="run" time="0.003" classname="CM_toolFileSystemJapaneseTest" />
    <testcase name="testFileSystemGetsAllFilesFromAllFolders" status="run" time="0.004" classname="CM_toolFileSystemJapaneseTest" />
    <testcase name="testFileSystemGetsAllFilesFromSubFolder" status="run" time="0.003" classname="CM_toolFileSystemJapaneseTest" />
    <testcase name="testFileSystemGetsAllFilesFromSubFolder_FailsWithNonJapaneseLocale" status="run" time="0.003" classname="CM_toolFileSystemJapaneseTest" />
  </testsuite>
  <testsuite name="CM_GetMac" tests="1" failures="0" disabled="0" errors="0" time="0.001">
    <testcase name="ReturnsMacAddress" status="run" time="0.001" classname="CM_GetMac" />
  </testsuite>
  </testsuites>""">

type GtestRunnerMSBuildTask(logger : TaskLoggingHelper) as this =
    inherit Task()
    let logger : TaskLoggingHelper = if logger = null then new TaskLoggingHelper(this) else logger
    let executor : CommandExecutor = new CommandExecutor(logger, int64(3500000))
    new() = GtestRunnerMSBuildTask(null)
    member val CurrentSeed : string = "" with get, set
    member val PreviousMessage : string = "" with get, set
    member val Actual : string = "" with get, set    
    member val Expected : string = "" with get, set
    member val buildok : bool = true with get, set
    member val counterFile : int = 0 with get, set
    member val ToOutputData : string list = [] with get, set
    member val testFiles : string list = [] with get, set

    member val UseRelativePaths = false with get, set

    /// Solution Path, Required
    member val SolutionPathToAnalyse = "" with get, set

    member val ProjectNameToAnalyse = "" with get, set

    /// Output Path, Required
    [<Required>]
    member val GtestXunitConverterOutputPath = "" with get, set

    /// Input report file Path, Required
    member val GtestXMLReportFile = "" with get, set

    /// path for GtestXunitConverter executable, default expects GtestXunitConverter in path
    member val TestSuffix = "_test.cpp" with get, set

    /// path replacement strings
    member val PathReplacementStrings = "" with get, set

    /// path replacement strings
    member val SkipSearchForFileLocation = false with get, set

    /// Exe with test files
    [<Required>]
    member val GtestExeFile = "" with get, set

    member val ExtraArgumentsToGtestExe = "" with get, set

    member val Shuffle = false with get, set
    member val SeedStart = 1 with get, set
    member val SeedEnd = 1 with get, set

    member val BrakeBuild = true with get, set

    /// If True GtestExe Needs to be given
    member val RunTests = false with get, set

    member x.ParseXunitReport(filePath :string, logger : TaskLoggingHelper) = 
        let xunitReport = GtestXmlReport.Parse(File.ReadAllText(filePath))

        let mutable xmloutputcontent = ""

        let addLine (line:string, ouputFilePath) =
            use wr = new StreamWriter(ouputFilePath, true)
            wr.WriteLine(line)

        let getFileNameFromListOfFile className =
            let checkPresenceOfTest(filePath : string) = 
                if this.BuildEngine <> null then
                    logger.LogMessage(sprintf "Parse Source File: %s" filePath)
                let lineswithoutspaces = File.ReadAllText(filePath).Replace(" ", "")
                lineswithoutspaces.Contains("TEST_F(" + className) || lineswithoutspaces.Contains("TEST(" + className)

            try
                (List.toArray this.testFiles) |> Array.find (fun elem -> checkPresenceOfTest(elem))
            with
             | ex -> 
                    if this.BuildEngine = null then
                        System.Console.WriteLine(sprintf "Cannot Find Test Class: %s" className)
                    else                        
                        logger.LogWarning(sprintf "Cannot Find Test Class: %s" className)
                    ""

        let getTestCaseNameFromListOfFiles(testCaseName:string, filename:string) =
            let checkPresenceOfTestCase(filePath : string) = 
                if this.BuildEngine <> null then
                    logger.LogMessage(sprintf "Parse Source File: %s" filePath)
                let lineswithoutspaces = File.ReadAllText(filePath).Replace(" ", "")
                (lineswithoutspaces.Contains("TEST_F(") && lineswithoutspaces.Contains(testCaseName)) ||
                    (lineswithoutspaces.Contains("TEST(") && lineswithoutspaces.Contains(testCaseName))

            if checkPresenceOfTestCase(filename) then
                filename
            else
                try
                    (List.toArray this.testFiles) |> Array.find (fun elem -> checkPresenceOfTestCase(elem))
                with
                 | ex -> 
                        if this.BuildEngine = null then
                            System.Console.WriteLine(sprintf "Cannot Find Test Class: %s" testCaseName)
                        else                        
                            logger.LogWarning(sprintf "Cannot Find Test Class: %s" testCaseName)
                        ""



        let XmlEscape(unescaped : string) =
            let doc = new XmlDocument()
            let node = doc.CreateElement("root")
            node.InnerText <- unescaped
            node.InnerXml

        let getReportName = 
            if not(x.ProjectNameToAnalyse = "") then 
                x.ProjectNameToAnalyse
            else
                ""

        for testSuite in xunitReport.Testsuites do
                             
            if (testSuite.Testcases |> Array.exists (fun case -> case.Status.Equals("run"))) then
                let reportName = sprintf "xunit-result-%s-%i.xml" getReportName this.counterFile
                let xml_file = Path.Combine(x.GtestXunitConverterOutputPath, reportName)
                if File.Exists(xml_file) then
                    File.Delete(xml_file)

                this.counterFile <- this.counterFile + 1
                addLine("""<?xml version="1.0" encoding="UTF-8"?>""", xml_file)
                let mutable fileName = ""
                if not(x.SkipSearchForFileLocation) then
                    fileName <- getFileNameFromListOfFile(testSuite.Name)
                    if x.UseRelativePaths then
                        let pathAbs = Directory.GetParent(x.SolutionPathToAnalyse).ToString()
                        let pathFile = "./" + fileName.Replace(pathAbs, "").Substring(1).Replace("\\", "/")
                        let suitestr = sprintf """<testsuite name="%s" tests="%i" failures="%i" disabled="%i" errors="%i" time="%f" filename="%s">""" testSuite.Name testSuite.Tests testSuite.Failures testSuite.Disabled testSuite.Errors testSuite.Time pathFile
                        addLine(suitestr, xml_file)
                    else
                        let suitestr = sprintf """<testsuite name="%s" tests="%i" failures="%i" disabled="%i" errors="%i" time="%f" filename="%s">""" testSuite.Name testSuite.Tests testSuite.Failures testSuite.Disabled testSuite.Errors testSuite.Time fileName
                        addLine(suitestr, xml_file)
                else
                    let suitestr = sprintf """<testsuite name="%s" tests="%i" failures="%i" disabled="%i" errors="%i" time="%f">""" testSuite.Name testSuite.Tests testSuite.Failures testSuite.Disabled testSuite.Errors testSuite.Time
                    addLine(suitestr, xml_file)

                for testcase in testSuite.Testcases do 
                    let mutable casestr = ""
                    let mutable testCasefileName = ""
                    try
                        if not(x.SkipSearchForFileLocation) then
                            testCasefileName <- getTestCaseNameFromListOfFiles(testcase.Name, fileName)
                            if x.UseRelativePaths then
                                let pathAbs = Directory.GetParent(x.SolutionPathToAnalyse).ToString()
                                testCasefileName <- "./" + testCasefileName.Replace(pathAbs, "").Substring(1).Replace("\\", "/")

                            casestr <- sprintf """   <testcase name="%s" status="%s" time="%f" classname="%s" filename="%s">""" testcase.Name testcase.Status testcase.Time testcase.Classname testCasefileName
                        else
                            casestr <- sprintf """   <testcase name="%s" status="%s" time="%f" classname="%s" >""" testcase.Name testcase.Status testcase.Time testcase.Classname

                        let message = XmlEscape testcase.Failure.Value.Message
                        let unscape = message.Replace("\"", "&quot;")
                        let failurestr = sprintf """<failure message="%s" type="%s"><![CDATA[%s]]></failure>""" unscape testcase.Failure.Value.Type (XmlEscape testcase.Failure.Value.Type)
                        addLine(casestr, xml_file)
                        addLine(failurestr, xml_file)
                        addLine("""   </testcase>""", xml_file)
                    with
                    | ex ->
                        if not(x.SkipSearchForFileLocation) then
                            let casestr = sprintf """   <testcase name="%s" status="%s" time="%f" classname="%s"  filename="%s"/>""" testcase.Name testcase.Status testcase.Time testcase.Classname testCasefileName
                            addLine(casestr, xml_file)
                        else
                            let casestr = sprintf """   <testcase name="%s" status="%s" time="%f" classname="%s" />""" testcase.Name testcase.Status testcase.Time testcase.Classname
                            addLine(casestr, xml_file)

                        ()

                addLine("""</testsuite>""", xml_file)                 
        ()

    member x.generateCommandLineArgs seed =
        let builder = new CommandLineBuilder()
        if String.IsNullOrEmpty(seed) then
            x.GtestXMLReportFile <- x.GtestExeFile + ".xml"
        else
            x.GtestXMLReportFile <- x.GtestExeFile + "." + seed + ".xml"
            builder.AppendSwitch("--gtest_shuffle")     
            builder.AppendSwitch("--gtest_random_seed=" + seed)

        if not(String.IsNullOrEmpty(x.ExtraArgumentsToGtestExe)) then
            builder.AppendSwitch(x.ExtraArgumentsToGtestExe)

        builder.AppendSwitch("--gtest_output=xml:" + x.GtestXMLReportFile)        
        builder.ToString()

    member x.ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
        if not(String.IsNullOrWhiteSpace(e.Data))  then
            if Environment.GetEnvironmentVariable("TEAMCITY_PROJECT_NAME") = null || Environment.GetEnvironmentVariable("TEAMCITY_PROJECT_NAME").Equals("NOT FOUND") then
                if e.Data.Contains("[ FAILED        ]") || e.Data.Contains("[  FAILED  ] ") || e.Data.Contains("SEH exception with code") then
                    try
                        if e.Data.Contains("SEH exception with code") then
                            logger.LogError(e.Data)
                        // c:\prod\structures\src\work\core\common\libtransform\test\transform_vector_test.cpp(40): error : Value of: Vector_c(-0.0, -1.0, -1.0)
                        // transform_vector_test.cpp(40): error : Value of: Vector_c(-0.0, -1.0, -1.0)
                        else if not(String.IsNullOrEmpty(x.PreviousMessage)) then
                            let elemsName = x.PreviousMessage.Split("(".ToCharArray())
                            let line = System.Int32.Parse(elemsName.[1].Split(")".ToCharArray()).[0])
                            let msg = x.PreviousMessage.Replace(elemsName.[0] + "(" + line.ToString() + "): error :", "")
              
                            if x.Shuffle then
                                logger.LogError("", "", "", elemsName.[0], line, 0, line, 0, "SEED Error: " + x.CurrentSeed + " : " + msg + "\n" + x.Expected + "\n" + x.Actual)
                            else
                                logger.LogError("", "", "", elemsName.[0], line, 0, line, 0, msg + "\n" + x.Expected + "\n" + x.Actual)

                            x.Expected <- ""
                            x.Actual <- ""
                            x.PreviousMessage <- ""
                    with :? System.Exception as e ->
                        logger.LogWarning(e.Message + " " + e.StackTrace + "\n" + e.Source + "\n" +  e.TargetSite.ToString() + "\n" +  e.HelpLink )                                                                                      
                else
                    if e.Data.Contains("Expected: ") then
                        x.Expected <- e.Data
                    elif e.Data.Contains("  Actual: ") then
                        x.Actual <- e.Data
                    else
                        let failedFile = e.Data.Split("(".ToCharArray()).[0]
                        if not(String.IsNullOrEmpty(failedFile)) && File.Exists(failedFile) then
                            x.PreviousMessage <- e.Data
                                        
            if this.BuildEngine <> null then    
                logger.LogMessage(MessageImportance.High, e.Data)

    member x.ExecuteTests executor =
        let env = Map.ofList [("CPPCHECK_INPUT", x.GtestExeFile)]

        let mutable returncode = 0
        if not(x.Shuffle) then
            if not(this.BuildEngine = null) then
                logger.LogMessage(sprintf "gtest: %s %s" x.GtestExeFile (x.generateCommandLineArgs ""))
            returncode <- (executor :> ICommandExecutor).ExecuteCommand(x.GtestExeFile, (x.generateCommandLineArgs ""), env, x.ProcessOutputDataReceived, x.ProcessOutputDataReceived, Directory.GetParent(x.GtestExeFile).ToString())
            try
                if File.Exists(x.GtestXMLReportFile) then
                    this.ParseXunitReport(x.GtestXMLReportFile,logger)
            with
            | ex -> ()
        else
            for i in x.SeedStart .. x.SeedEnd do
                x.CurrentSeed <- i.ToString()
                if not(this.BuildEngine = null) then
                    logger.LogMessage(sprintf "gtest: %s %s" x.GtestExeFile (x.generateCommandLineArgs(i.ToString())))
                returncode <- (executor :> ICommandExecutor).ExecuteCommand(x.GtestExeFile, x.generateCommandLineArgs(i.ToString()), env, x.ProcessOutputDataReceived, x.ProcessOutputDataReceived, Directory.GetParent(x.GtestExeFile).ToString())
                if File.Exists(x.GtestXMLReportFile) then
                    this.ParseXunitReport(x.GtestXMLReportFile, logger)

        if returncode <> 0 then
            if this.BrakeBuild then
                logger.LogError(sprintf "%s Exit with Return Code = %d" x.GtestExeFile returncode)
            else
                logger.LogWarning(sprintf "%s Exit with Return Code = %d => Turn to Warning BrakeBuild=false" x.GtestExeFile returncode)

        returncode = 0          
        
    member x.TestExecutableIsFound =
        lazy(

            if not(System.IO.File.Exists(x.GtestExeFile)) then logger.LogError(sprintf "Test Executable Not Found, Be Sure It Has Been Build: %s Cannot Be Found on System, Set Path Correctly" x.GtestExeFile)
            )

    override x.Execute() =

        if this.BuildEngine <> null then
            this.TestExecutableIsFound.Force()

        let mutable result = not(logger.HasLoggedErrors)
        if result then
            let stopWatchTotal = Stopwatch.StartNew()

            if not(Directory.Exists(x.GtestXunitConverterOutputPath)) then
                if this.BuildEngine <> null then
                    logger.LogMessage(sprintf "Create New Folder: %s " x.GtestXunitConverterOutputPath)

                Directory.CreateDirectory(x.GtestXunitConverterOutputPath) |> ignore

            if not(x.SkipSearchForFileLocation) && not(String.IsNullOrEmpty(x.SolutionPathToAnalyse)) then
                let solutionHelper = new VSSolutionUtils()
                let projectHelper = new VSProjectUtils()
                let iterateOverProjectFiles(projectFile : ProjectFiles) =
                    if this.BuildEngine <> null then
                        logger.LogMessage(sprintf "Get Test Files in: %s Using TextSuffix: %s And ReplacementStrings: %s" projectFile.path x.TestSuffix x.PathReplacementStrings)
                    this.testFiles <- this.testFiles @ projectHelper.GetCompilationFiles(projectFile.path, x.TestSuffix, x.PathReplacementStrings)
                solutionHelper.GetProjectFilesFromSolutions(x.SolutionPathToAnalyse) |> Seq.iter (fun x -> iterateOverProjectFiles x)
                
            if x.RunTests then
                result <- x.ExecuteTests executor
            else 
                for repfile in Directory.GetFiles(Directory.GetParent(this.GtestXMLReportFile).ToString(), Path.GetFileName(this.GtestXMLReportFile)) do
                    this.ParseXunitReport(repfile, logger)

            if this.BuildEngine = null then
                System.Console.WriteLine(sprintf "GtestXunitConverter End: %u ms" stopWatchTotal.ElapsedMilliseconds)
            else
                logger.LogMessage(sprintf "GtestXunitConverter End: %u ms" stopWatchTotal.ElapsedMilliseconds)

        if x.BrakeBuild then
            if result && this.buildok then
                true
            else
                false
        else
            true

    interface ICancelableTask with
        member this.Cancel() =
            Environment.Exit(0)
            ()
