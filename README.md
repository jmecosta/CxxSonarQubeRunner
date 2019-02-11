[![Build status](https://ci.appveyor.com/api/projects/status/pc2gnjt8tji49y3v/branch/master?svg=true)](https://ci.appveyor.com/project/jorgecosta/sonar-cxx-msbuild-tasks/branch/master)


This repository contains a build wrapper for run SonarQube analysis in a more automated way than the current SonarQube MSBuild Runner. The following features are available.  

1. Runs complete analysis in single command, runs begin, end and build solution.
2. You can use it for feature branch flow, the wrapper will provision project, setup permissions and quality profiles and in the end duplicate false positives into feature branch.
3. Supports C++ community edition by running Vera, Rats, Cpplint and CppCheck before the end stage. This allows issues to be reported without any other configuration
4. Wrapper uses buildlog to retrieve build information the C++ requires 
5. Validates Gate and Compute Engine sucess and brakes the builds if they are not ok. This is the default strategy to SonarQube 5.2 and above
6. Supports Visual Studio Shared projects

# Installation
1. Download wrapper from https://github.com/jmecsoftware/sonar-cxx-msbuild-tasks/releases and unzip to some location in your hard drive. Last snapshot is available in AppVeyour in the artifacts section after followign the build icon above.
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

## Command line options

        Usage: CxxSonarQubeMsbuidRunner [OPTIONS]
        Runs MSbuild Runner with Cxx Support
        
        Options:
            /A|/a:<amd64, disabled>
            /B|/b:<parent_branch  : in multi branch confiuration. Its parent branch>
            /C|/c:<Permission template to apply when using feature branches>
            /D|/d:<property to pass : /d:sonar.host.url=http://localhost:9000>
            /E|/e reuse reports mode, cxx  static tools will not run. Ensure reports are placed in default locations.
            /F|/f disable code analysis in solution.
            /G|/g enable verbose mode.
        
            /I|/i wrapper will install tools only. No analysis is performed
            /J|/j:<number of processor used for msbuild : /m:1 is default. 0 uses all processors /m>
            /K|/k:<key : key>
        
            /M|/m:<solution file : mandatory>
            /N|/n:<name : name>
        
            /P|/p:<additional settings for msbuild - /p:Configuration=Release>
            /Q|/q:<SQ msbuild runner path>
            /R|/r:<msbuild sonarqube runner -> 1.1>       
            /S|/s:<additional settings filekey>
            /T|/t:<msbuild target, default is /t:Rebuild>
        
            /V|/v:<version : version>
            /X|/x:<version of msbuild : vs10, vs12, vs13, vs15, default is vs15>
