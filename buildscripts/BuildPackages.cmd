@ECHO OFF

:INITIALIZE_ARGUMENTS
SET %1
SET %2

REM ECHO arg1 = %1
REM ECHO arg2 = %2

GOTO INITIALIZE_VARIABLES


:INITIALIZE_VARIABLES
SET CONFIGURATION="Release"
SET BUILD_VERSION="5.0.0"

GOTO SET_CONFIGURATION


:SET_CONFIGURATION
IF "%config%"=="" GOTO SET_BUILD_VERSION
SET CONFIGURATION=%config%

GOTO SET_BUILD_VERSION


:SET_BUILD_VERSION
IF "%version%"=="" GOTO RESTORE_PACKAGES
SET BUILD_VERSION=%version%

ECHO ---------------------------------------------------
REM ECHO Building "%config%" packages with version "%version%"...
ECHO Building "%CONFIGURATION%" packages with version "%BUILD_VERSION%"...
ECHO ---------------------------------------------------

GOTO RESTORE_PACKAGES


:RESTORE_PACKAGES
dotnet restore .\buildscripts\BuildScripts.csproj
dotnet restore .\src\Lucene.Net.Linq\Lucene.Net.Linq.csproj
dotnet restore .\src\Lucene.Net.Linq.Tests\Lucene.Net.Linq.Tests.csproj

GOTO BUILD


:BUILD
dotnet build Lucene.Net.Linq.sln -c %CONFIGURATION% /p:APPVEYOR_BUILD_VERSION=%BUILD_VERSION% --no-restore

GOTO TEST


:TEST

ECHO ----------------
ECHO Running Tests...
ECHO ----------------

dotnet test .\src\Lucene.Net.Linq.Tests --no-restore || exit /b 1



