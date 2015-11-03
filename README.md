
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

