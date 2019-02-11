SET mypath=%~dp0
set CurrentDir=%mypath:~0,-1%
%CurrentDir%\win-x64\CxxSonarQubeRunner %*