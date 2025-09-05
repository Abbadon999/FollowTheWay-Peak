@echo off
setlocal enabledelayedexpansion

echo ========================================
echo FollowTheWay Build Script
echo ========================================

:: Check if API key is provided
if "%FOLLOWTHEWAY_API_KEY%"=="" (
    echo ERROR: FOLLOWTHEWAY_API_KEY environment variable is not set!
    echo Please set your API key: set FOLLOWTHEWAY_API_KEY=your_api_key_here
    pause
    exit /b 1
)

:: Set default server URL if not provided
if "%FOLLOWTHEWAY_SERVER_URL%"=="" (
    set FOLLOWTHEWAY_SERVER_URL=https://followtheway.ru
    echo Using default server URL: !FOLLOWTHEWAY_SERVER_URL!
) else (
    echo Using custom server URL: %FOLLOWTHEWAY_SERVER_URL%
)

:: Create ApiKeys.cs from template
echo Creating ApiKeys.cs from template...
if not exist "src\Config\ApiKeys.cs.template" (
    echo ERROR: ApiKeys.cs.template not found!
    pause
    exit /b 1
)

:: Replace placeholders in template
powershell -Command "(Get-Content 'src\Config\ApiKeys.cs.template') -replace '{{FOLLOWTHEWAY_API_KEY_PLACEHOLDER}}', '%FOLLOWTHEWAY_API_KEY%' -replace '{{FOLLOWTHEWAY_SERVER_URL_PLACEHOLDER}}', '%FOLLOWTHEWAY_SERVER_URL%' | Set-Content 'src\Config\ApiKeys.cs'"

if not exist "src\Config\ApiKeys.cs" (
    echo ERROR: Failed to create ApiKeys.cs!
    pause
    exit /b 1
)

echo ApiKeys.cs created successfully!

:: Build the project
echo Building FollowTheWay...
dotnet build src\FollowTheWayPeak.csproj -c Release

if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

:: Clean up - remove ApiKeys.cs for security
echo Cleaning up ApiKeys.cs for security...
del "src\Config\ApiKeys.cs"

echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo IMPORTANT SECURITY NOTES:
echo - ApiKeys.cs was automatically deleted after build
echo - Your API key is embedded in the compiled DLL
echo - Never share your API key or commit ApiKeys.cs to version control
echo - The template file is safe to commit (contains only placeholders)
echo.
pause