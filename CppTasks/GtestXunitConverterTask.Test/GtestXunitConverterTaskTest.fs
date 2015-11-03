namespace GtestXunitConverterTask.Test

open System.IO
open NUnit.Framework
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open MSBuild.Tekla.Tasks.GtestXunitConverter
open Foq

type GtestXuniConverterPathTest() =

    [<TearDown>]
    member test.TearDown() =
        for filename in Directory.GetFiles(".", @"xunit-result-*.xml", SearchOption.AllDirectories) do
            File.Delete(filename)

    //[<Test>]
    member test.``Should Parse Correctly Xunit Report`` () =
        let mockLogger =
            Mock<Microsoft.Build.Utilities.TaskLoggingHelper>().Create()
                 
        let task = GtestRunnerTask(mockLogger)
        task.SolutionPathToAnalyse <- "./testdata/solutionsfile.sln"
        task.TestSuffix <- "_test.cpp;_tests.cpp"
        task.GtestXMLReportFile <- "./testdata/xunit-report.xml"
        task.GtestXunitConverterOutputPath <- "./testdata/"
        Assert.That(task.Execute(), Is.True)
       
        let files = Directory.GetFiles("./testdata/", "xunit-result-*.xml")
        Assert.That(files.Length, Is.EqualTo(2))
        Assert.That(File.ReadAllText(files.[1]).Contains("file2_tests.cpp"), Is.True)
        Assert.That(File.ReadAllText(files.[0]).Contains("file1_test.cpp"), Is.True)

    //[<Test>]
    member test.``Should Parse Correctly Xunit Report with *.xml`` () = 
        let mockLogger =
            Mock<Microsoft.Build.Utilities.TaskLoggingHelper>().Create()

        let task = GtestRunnerTask(mockLogger)
        task.SolutionPathToAnalyse <- "./testdata/solutionsfile.sln"
        task.TestSuffix <- "_test.cpp;_tests.cpp"
        task.GtestXMLReportFile <- "./testdata/*.xml"
        task.GtestXunitConverterOutputPath <- "./testdata/"

        Assert.That(task.Execute(), Is.True)
       
        let files = Directory.GetFiles("./testdata/", "xunit-result-*.xml")
        Assert.That(files.Length, Is.EqualTo(2))
        Assert.That(File.ReadAllText(files.[1]).Contains("file2_tests.cpp"), Is.True)
        Assert.That(File.ReadAllText(files.[0]).Contains("file1_test.cpp"), Is.True)
