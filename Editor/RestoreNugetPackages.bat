::@echo OFF

:: Get the Unity executable location from the registry
:: Info on getting registry key: http://stackoverflow.com/questions/445167/how-can-i-get-the-value-of-a-registry-key-from-within-a-batch-script
FOR /F "usebackq tokens=3*" %%A IN (`REG QUERY "HKCU\Software\Unity Technologies\Installer\Unity" /v "Location x64"`) DO (
    set unitydir=%%B
    )
::ECHO %unitydir%

:: Call the NuGet package restore method
"%unitydir%\Editor\Unity.exe" -quit -batchmode -projectPath %~dp0 -executeMethod NugetForUnity.NugetHelper.Restore

::pause