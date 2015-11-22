namespace MsbuildTaskUtils.Test

open NUnit.Framework
open MsbuildTasksUtils
open System.IO

type VSSolutionUtilsTest() = 

    let executingPath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "")).ToString()

    [<Test>]
    member test.``Should Read the Corrent Number of source files in project`` () = 
        let vsproject = new VSProjectUtils()
        let projects = vsproject.GetCompilationFiles(Path.Combine(executingPath, "testdata\\project1\\project1.vcxproj"), "", "")

        Assert.That(projects.Length, Is.EqualTo(4))
        let mutable expectedPath = Path.Combine(executingPath, "testdata\\project1\\source1.cpp")
        Assert.That(projects.[0], Is.EqualTo(expectedPath))
        expectedPath <- Path.Combine(executingPath, "testdata\\project1\\source2.cpp")
        Assert.That(projects.[1], Is.EqualTo(expectedPath))
        expectedPath <- Path.Combine(executingPath,"testdata\\project1\\header1.hpp")
        Assert.That(projects.[2], Is.EqualTo(expectedPath))
        expectedPath <- Path.Combine(executingPath,"testdata\\project1\\header2.hpp")
        Assert.That(projects.[3], Is.EqualTo(expectedPath))


    [<Test>]
    member test.``Should Read the Corrent Number of source files in project give a include search string`` () = 
        let vsproject = new VSProjectUtils()
        let projects = vsproject.GetCompilationFiles(Path.Combine(executingPath, "testdata\\project1\\project1.vcxproj"), "ce1.cpp;er1.hpp", "")

        Assert.That(projects.Length, Is.EqualTo(2))
        let mutable expectedPath = Path.Combine(executingPath, "testdata\\project1\\source1.cpp")
        Assert.That(projects.[0], Is.EqualTo(expectedPath))
        expectedPath <- Path.Combine(executingPath, "testdata\\project1\\header1.hpp")
        Assert.That(projects.[1], Is.EqualTo(expectedPath))

    [<Test>]
    member test.``Should Read the Corrent Number of source files in project give a include search string without case sensitive`` () = 
        let vsproject = new VSProjectUtils()
        let projects = vsproject.GetCompilationFiles(Path.Combine(executingPath, "testdata\\project1\\project1.vcxproj"), "Ce1.cpp;eR1.hpp", "")

        Assert.That(projects.Length, Is.EqualTo(2))
        let mutable expectedPath = Path.Combine(executingPath, "testdata\\project1\\source1.cpp")
        Assert.That(projects.[0], Is.EqualTo(expectedPath))
        expectedPath <- Path.Combine(executingPath, "testdata\\project1\\header1.hpp")
        Assert.That(projects.[1], Is.EqualTo(expectedPath))

    [<Test>]
    member test.``Should Read the Correct Number of Projects From Solution`` () = 
        let vssolution = new VSSolutionUtils()
        let projects = vssolution.GetProjectFilesFromSolutions(Path.Combine(executingPath, "testdata\\solutionsfile.sln"))
        Assert.That(projects.Length, Is.EqualTo(1))
        Assert.That(projects.[0].name, Is.EqualTo("project1"))
