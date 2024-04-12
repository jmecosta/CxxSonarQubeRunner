@echo on
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
"C:\Program Files\7-Zip\7z.exe" x Tools\Tools.zip -oBuildDrop\Publish -r
copy /Y Tools\CxxSonarQubeRunnerLinux.sh BuildDrop\Publish
copy /Y Tools\CxxSonarQubeRunnerOsx.sh BuildDrop\Publish
copy /Y Tools\CxxSonarQubeRunnerWin.bat BuildDrop\Publish
robocopy /MIR BuildDrop\Runner\net8.0\linux-x64\publish BuildDrop\Publish\linux-x64
robocopy /MIR BuildDrop\Runner\net8.0\osx-x64\publish BuildDrop\Publish\osx-x64
robocopy /MIR BuildDrop\Runner\net8.0\win-x64\publish BuildDrop\Publish\win-x64
cd BuildDrop\Publish
"C:\Program Files\7-Zip\7z.exe" a -tzip CxxSonarQubeRunner.zip .
move CxxSonarQubeRunner.zip ..\..
cd ..
cd ..
