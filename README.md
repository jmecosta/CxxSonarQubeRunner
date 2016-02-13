[![Build status](https://ci.appveyor.com/api/projects/status/pc2gnjt8tji49y3v/branch/master?svg=true)](https://ci.appveyor.com/project/jorgecosta/sonar-cxx-msbuild-tasks/branch/master)

This repository contains a build wrapper for run SonarQube analysis in a more automated way than the current SonarQube MSBuild Runner. The following features are available. The wrapper

1. 

# Sonar C++ Community plugin msbuild tasks 

Easy execution of static analysis tools that run outside cxx plugin.

# Quickstart
1. Install nuget packages in you c++ projects from nuget gallery
2. enable analysis with *TaskEnabled=True
 
## Task installation in visual studio
1. CppCheckTask - Install-Package CppCheckTask
2. CppLint - Install-Package CppLintTask
3. GtestRunnerTask - Install-Package GtestRunnerTask
4. IntelInspectorTask - Install-Package IntelInspectorTask
5. RatsTask - Install-Package RatsTask
6. VeraTask - Install-Package VeraTask

## Default reports paths, from solution folder
1. CppCheckTask - sonarcpp\reports-cppcheck
2. CppLint - sonarcpp\cpplint\cpplint_mod.py
3. GtestRunnerTask - sonarcpp\gtest-reports\$(ProjectName).xml
4. IntelInspectorTask - sonarcpp\externalrules-result\intel-result-$(MSBuildProjectName).xml
5. RatsTask - sonarcpp\reports-rats
6. VeraTask - sonarcpp\reports-vera++

## CxxSonarQubeMsbuidRunner
This is a wrapper around SonarQube Msbuild Runner, the input is the same as in the original. with a few more options to support the wrapper.

Download Latest CxxSonarQubeMsbuidRunner.zip From: https://ci.appveyor.com/project/jorgecosta/sonar-cxx-msbuild-tasks/build/artifacts

To use:

1. Run CxxSonarQubeMsbuidRunner.exe /h to see the usage

2. Example: CxxSonarQubeMsbuidRunner.exe /k:key /n:name /v:work /m:solution.sln


### working with feature branches

To use:

1. in command line set /d:sonar.branch=your_branch /b:main_branch

   . /d:sonar.branch is the regular prop for creating branch
   . /b: is the target branch or master branch

This will create a new branch in sonar and copy all settings over to the feature bracnh from main. A small meta-runner exists here https://github.com/SonarOpenCommunity/sonar-cxx-msbuild-tasks/releases for teamcity.

If you want branches to be independent you can skip the /b parameter, after that it will not copy any settings to the new branch
