@ECHO OFF

SET BuildNumber=1000
SET VersionControlInfo="master-Signed"

%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe /p:BuildNumber=%BuildNumber%,VersionControlInfo=%VersionControlInfo% Ciao.proj
XCOPY "build\artifacts\*.nupkg" "..\..\Libraries\packages"



