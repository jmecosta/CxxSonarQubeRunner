namespace CppLintTask.Test

open System.IO
open NUnit.Framework
open Microsoft.Build
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Utilities
open MSBuild.Tekla.Tasks.CppLint
open Foq
open MSBuild.Tekla.Tasks.Executor

type CppLintTest() =
    let tempFile = "out.xml"
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
                .Setup(fun x -> <@ x.GetStdError @>).Returns([])
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        let task = CppLintTask(mockExecutor)
        task.CppLintOutputType <- "vs7"

        Assert.That((task.ExecuteCppLint "foo bar"), Is.True)

    [<Test>]
    member test.``Run return 0 when run ok with xml`` () = 
        let data = ["E:\SRC\Project\file.cpp:7:  IllegalIncludeDirectories: Include File is illegal in this Project: main_assert_trap.hpp  [bla/include_files-1] [1]";
                "Done processing E:\SRC\Project\file.cpp";
                "Total errors found: 1"]
        let mockExecutor =
            Mock<ICommandExecutor>()
                .Setup(fun x -> <@ x.ExecuteCommand(any(), any(), any(), any()) @>).Returns(0)
                .Setup(fun x -> <@ x.GetStdError @>).Returns(data)
                .Setup(fun x -> <@ x.GetErrorCode @>).Returns(ReturnCode.Ok)
                .Create()

        let task = CppLintTask(mockExecutor)
        task.CppLintOutputType <- "xml"
        task.SolutionPathToAnalyse <- "E:\\SRC\\Project\\test.sln"

        Assert.That((task.ExecuteCppLint "filepath"), Is.True)
        Assert.That(File.Exists(tempFile), Is.True)
        Assert.That(File.ReadAllLines(tempFile).Length, Is.EqualTo(4))
