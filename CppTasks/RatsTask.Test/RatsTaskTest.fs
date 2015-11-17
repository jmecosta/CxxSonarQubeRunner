namespace RatsTask.Test

open System.IO
open NUnit.Framework
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open MSBuild.Tekla.Tasks.Rats
open Foq
open MSBuild.Tekla.Tasks.Executor

type RatsTest() =
    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
    let tempFile = Path.Combine(executingPath, "out.xml")
    let lines = List.ofArray(File.ReadAllLines(Path.Combine(executingPath, "rats-report.txt")))
    let mockLogger = Mock<TaskLoggingHelper>().Create()

    [<TearDown>]
    member test.TearDown() =
        if File.Exists(tempFile) then
            File.Delete(tempFile)

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

        Assert.That(task.ExecuteRats "foo bar", Is.True)
        Assert.That((File.Exists(tempFile)), Is.True)
        Assert.That(File.ReadAllLines(tempFile).Length, Is.EqualTo(41))
