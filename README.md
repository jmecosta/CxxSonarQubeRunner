[![Build status](https://ci.appveyor.com/api/projects/status/dicvukk3la7n57gv/branch/master?svg=true)](https://ci.appveyor.com/project/SonarOpenCommunity/sonar-cxx-msbuild-tasks/branch/master)

# SonarQube C++ Community plugin msbuild tasks 

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

Download Latest From: https://ci.appveyor.com/api/buildjobs/avy5n4j6j386ftls/artifacts/CxxSonarQubeMsbuidRunner.zip

To use:
1. Run CxxSonarQubeMsbuidRunner.exe /h to see the usage
2. Example: CxxSonarQubeMsbuidRunner.exe /k:key /n:name /v:work /p:solution.sln
