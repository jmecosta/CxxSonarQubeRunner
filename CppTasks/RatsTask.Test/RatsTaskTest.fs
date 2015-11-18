namespace RatsTask.Test

open System.IO
open NUnit.Framework
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open MSBuild.Tekla.Tasks.Rats
open Foq
open MsbuildTasks

type RatsTest() =
    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
    let lines = List.ofArray(File.ReadAllLines(Path.Combine(executingPath, "rats-report.txt")))
    let mockLogger = Mock<TaskLoggingHelper>().Create()
    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()

    [<TearDown>]
    member test.TearDown() =
        if File.Exists(Path.Combine(executingPath, "rats-result--0.xml")) then
            File.Delete(Path.Combine(executingPath, "rats-result--0.xml"))

    [<Test>]
    member test.``Run return 0 when run ok with vs with empty lines`` () = 
        let mockExecutor =
            Mock<ICommandExecutor>()
                .Setup(fun x -> <@ x.ExecuteCommand(any(), any(), any(), any()) @>).Returns(0)
                .Setup(fun x -> <@ x.GetStdOut @>).Returns([])
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        let task = RatsTask(mockExecutor)
        task.RatsOutputType <- "vs7"

        Assert.That((task.ExecuteRats "foo bar"), Is.True)

    [<Test>]
    member test.``Run return 0 when run ok with vs with lines`` () = 
        let mockExecutor =
            Mock<ICommandExecutor>()
                .Setup(fun x -> <@ x.ExecuteCommand(any(), any(), any(), any()) @>).Returns(0)
                .Setup(fun x -> <@ x.GetStdOut @>).Returns(lines)
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        let task = RatsTask(mockExecutor)
        task.RatsOutputType <- "xml"
        task.RatsOutputPath <- executingPath

        Assert.That(task.ExecuteRats "foo bar", Is.True)
        Assert.That((File.Exists(Path.Combine(executingPath, "rats-result--0.xml"))), Is.True)
        Assert.That(File.ReadAllLines(Path.Combine(executingPath, "rats-result--0.xml")).Length, Is.EqualTo(41))
