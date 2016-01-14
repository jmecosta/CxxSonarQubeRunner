namespace GtestXunitConverterTask.Test

open System.IO
open NUnit.Framework
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open GtestRunnerMSBuildTask
open Foq

type GtestXuniConverterPathTest() =

    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()

    [<Test>]
    member test.``Should Parse Correctly Xunit Report with *.xml`` () = 
        let task = GtestRunnerMSBuildTask(null)
        task.SolutionPathToAnalyse <- Path.Combine(executingPath, "testdata", "solutionsfile.sln")
        task.TestSuffix <- "_test.cpp;_tests.cpp"
        task.GtestXMLReportFile <- Path.Combine(executingPath,  "testdata", "*.xml")
        task.GtestXunitConverterOutputPath <- Path.Combine(executingPath,  "testdata")
        Assert.That(task.Execute(), Is.True)
       
        let files = Directory.GetFiles(Path.Combine(executingPath, "testdata"), "xunit-result-*.xml")
        Assert.That(files.Length, Is.EqualTo(2))
        Assert.That(File.ReadAllText(files.[1]).Contains("file2_tests.cpp"), Is.True)
        Assert.That(File.ReadAllText(files.[0]).Contains("file1_test.cpp"), Is.True)
