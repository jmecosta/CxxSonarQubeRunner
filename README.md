[![Build status](https://ci.appveyor.com/api/projects/status/pc2gnjt8tji49y3v/branch/master?svg=true)](https://ci.appveyor.com/project/jorgecosta/sonar-cxx-msbuild-tasks/branch/master)

This repository contains a build wrapper for run SonarQube analysis in a more automated way than the current SonarQube MSBuild Runner. The following features are available.  

1. Runs complete analysis in single command, runs begin, end and build solution.
2. You can use it for feature branch flow, the wrapper will provision project, setup permissions and quality profiles and in the end duplicate false positives into feature branch.
3. Supports C++ community edition by running Vera, Rats, Cpplint and CppCheck before the end stage. This allows issues to be reported without any other configuration
4. Wrapper uses buildlog to retrieve build information the C++ requires 
5. Validates Gate and Compute Engine sucess and brakes the builds if they are not ok. This is the default strategy to SonarQube 5.2 and above
6. Supports Visual Studio Shared projects

# Installation
1. Download wrapper from https://github.com/jmecsoftware/sonar-cxx-msbuild-tasks/releases and unzip to some location in your hard drive.
2. If you use Teamcity, you can use this metarunner https://gist.github.com/jmecosta/32bfc907668256bd7763 that will automate the download of the wrapper.

# Basic Usage
1. Run CxxSonarQubeMsbuidRunner.exe (/h to see the usage)

   Example: CxxSonarQubeMsbuidRunner.exe /k:key /n:name /v:work /m:solution.sln > these are the mandatory arguments

## Feature branch flow 
1. in command line set /d:sonar.branch=your_branch /b:main_branch

   . /d:sonar.branch is the regular prop for creating branch

   . /b: is the target branch or master branch

This will create a new branch in sonar and copy all settings over to the feature bracnh from main. If you want branches to be independent you can skip the /b parameter, after that it will not copy any settings to the new branch

## Using the wrapper behind proxy or were admin rights are not available
The wrapper will install the needed tools to run analysis, however in cases were internet is not available or user has no admin rights to install some softwares it is possible to use a settings file called cxx-user-options.xml in your home folder. The format is as follow.

```
<CxxUserProperties>
  <CppCheck>c:\path</CppCheck>
  <Rats>c:\path</Rats>
  <Vera>c:\path</Vera>
  <Python>c:\path</Python>
  <Cpplint>c:\path</Cpplint>
  <MsbuildRunnerPath>c:\path</MsbuildRunnerPath>
</CxxUserProperties>
```

## Custom msbuild tasks
This wrapper uses several msbuild tasks that can be used outside the wrapper. Nuget packages are available in Nuget.org and can be installed by follwing the next instructions

1. CppCheckTask - Install-Package CppCheckTask
2. CppLint - Install-Package CppLintTask
3. GtestRunnerTask - Install-Package GtestRunnerTask 
4. IntelInspectorTask - Install-Package IntelInspectorTask
5. RatsTask - Install-Package RatsTask
6. VeraTask - Install-Package VeraTask

Gtest Task is a usefull task to gather test information before the analysis build. Same for intel inspector task. The configuration parameters for each task are available in each target file that is in the package or in GitHub repository. For example the following section in  https://github.com/jmecsoftware/sonar-cxx-msbuild-tasks/blob/master/Nuget/CppCheckTask.targets

```    <PropertyGroup>        
        <CppCheckPathX86            Condition="Exists('C:\Program Files (x86)\Cppcheck\cppcheck.exe')">C:\Program Files (x86)\Cppcheck\cppcheck.exe</CppCheckPathX86>
        <CppCheckPathX64            Condition="Exists('C:\Program Files\Cppcheck\cppcheck.exe')">C:\Program Files\Cppcheck\cppcheck.exe</CppCheckPathX64>        
        <CppCheckPath               Condition="'$(CppCheckPath)' == '' And '$(CppCheckPathX86)' != ''">$(CppCheckPathX86)</CppCheckPath>
        <CppCheckPath               Condition="'$(CppCheckPath)' == '' And '$(CppCheckPathX64)' != ''">$(CppCheckPathX64)</CppCheckPath>
        <CppCheckOptions            Condition="'$(CppCheckOptions)' == ''">--inline-suppr;--enable=all;-j 8</CppCheckOptions>
		<CppCheckDefines            Condition="'$(CppCheckDefines)' == ''">__cplusplus</CppCheckDefines>
        <CppCheckIgnores            Condition="'$(CppCheckIgnores)' == ''"></CppCheckIgnores>
        <CppCheckOutputType         Condition="'$(CppCheckOutputType)' == ''">xml-version-1</CppCheckOutputType>
        <CppCheckOutputPath         Condition="'$(CppCheckOutputPath)' == ''">$(SolutionDir)\sonarcpp\reports-cppcheck</CppCheckOutputPath>
        <CppCheckTaskEnabled        Condition="'$(CppCheckTaskEnabled)' == ''">false</CppCheckTaskEnabled>
    </PropertyGroup>
```

contains the proerties the task use, by default it will look for the analysers in their default locations. But those can be overriten in your project file.
