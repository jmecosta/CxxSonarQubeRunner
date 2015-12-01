namespace VeraTask.Test

open System.IO
open NUnit.Framework
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open MSBuild.Tekla.Tasks.Vera
open Foq
open MsbuildTasksCommandExecutor
open System.IO

type VeraTest() =
    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()
    let mockLogger = Mock<TaskLoggingHelper>().Create()

    [<TearDown>]
    member test.TearDown() =
        if File.Exists(Path.Combine(executingPath, "vera-result-test-0.xml")) then
            File.Delete(Path.Combine(executingPath, "vera-result-test-0.xml"))

    [<Test>]
    member test.``Run return 0 when run ok with vs with empty lines`` () = 

        let mockExecutor =
            Mock<ICommandExecutor>()
                .Setup(fun x -> <@ x.ExecuteCommand(any(), any(), any(), any()) @>).Returns(0)
                .Setup(fun x -> <@ x.GetStdError @>).Returns([])
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        let task = VeraTask(mockExecutor)
        task.VeraOutputType <- "vs7"
        Assert.That((task.ExecuteVera "foo bar"), Is.True)

    [<Test>]
    member test.``Run return 0 when run ok with xml`` () = 
        let data = ["E:\SRC\Project\file.cpp:18: (T002) reserved name used for macro (incorrect use of underscore)";
                "E:\SRC\Project\file.cpp:83: (T008) keyword 'if' not followed by a single space";
                "E:\SRC\Project\file.cpp:88: (T008) keyword 'if' not followed by a single space";
                "E:\SRC\Project\file.cpp:93: (T008) keyword 'if' not followed by a single space";
                "E:\SRC\Project\file.cpp:202: (L003) trailing empty line(s)";]
        let mockExecutor =
            Mock<ICommandExecutor>()
                .Setup(fun x -> <@ x.ExecuteCommand(any(), any(), any(), any()) @>).Returns(0)
                .Setup(fun x -> <@ x.GetStdError @>).Returns(data)
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        let task = VeraTask(mockExecutor)
        task.VeraOutputType <- "xml"
        task.VeraOutputPath <- executingPath
        task.SolutionPathToAnalyse <- "E:\\SRC\\Project\\test.sln"

        Assert.That((task.ExecuteVera "filepath"), Is.True)
        Assert.That(File.Exists(Path.Combine(executingPath, "vera-result-test-0.xml")), Is.True)
        Assert.That(File.ReadAllLines(Path.Combine(executingPath, "vera-result-test-0.xml")).Length, Is.EqualTo(10))
