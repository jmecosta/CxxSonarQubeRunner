namespace VeraTask.Test

open System.IO
open NUnit.Framework
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open MSBuild.Tekla.Tasks.Vera
open Foq
open MSBuild.Tekla.Tasks.Executor
open System.IO

type VeraTest() =
    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
    let tempFile = Path.Combine(executingPath, "out.xml")
    let mockLogger = Mock<TaskLoggingHelper>().Create()

    [<TearDown>]
    member test.TearDown() =
        if File.Exists(tempFile) then
            File.Delete(tempFile)

    [<Test>]
    member test.``Run return 0 when run ok with vs with empty lines`` () = 
        let task = VeraTask()
        task.VeraOutputType <- "vs7"
        let mockExecutor =
            Mock<ICommandExecutor>()
                .Setup(fun x -> <@ x.ExecuteCommand(any(), any(), any(), any()) @>).Returns(0)
                .Setup(fun x -> <@ x.GetStdError @>).Returns([])
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        Assert.That((task.ExecuteVera mockExecutor "foo bar" "out.xml"), Is.True)

    [<Test>]
    member test.``Run return 0 when run ok with xml`` () = 
        let data = ["E:\SRC\Project\file.cpp:18: (T002) reserved name used for macro (incorrect use of underscore)";
                "E:\SRC\Project\file.cpp:83: (T008) keyword 'if' not followed by a single space";
                "E:\SRC\Project\file.cpp:88: (T008) keyword 'if' not followed by a single space";
                "E:\SRC\Project\file.cpp:93: (T008) keyword 'if' not followed by a single space";
                "E:\SRC\Project\file.cpp:202: (L003) trailing empty line(s)";]
        let task = VeraTask()
        task.VeraOutputType <- "xml"
        task.SolutionPathToAnalyse <- "E:\\SRC\\Project\\test.sln"
        let mockExecutor =
            Mock<ICommandExecutor>()
                .Setup(fun x -> <@ x.ExecuteCommand(any(), any(), any(), any()) @>).Returns(0)
                .Setup(fun x -> <@ x.GetStdError @>).Returns(data)
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        Assert.That((task.ExecuteVera mockExecutor "filepath" tempFile), Is.True)
        Assert.That(File.Exists(tempFile), Is.True)
        Assert.That(File.ReadAllLines(tempFile).Length, Is.EqualTo(10))
