function Write-Log
{
    param([string]$message);
    $timestamp = "[{0}]" -f (Get-Date -Format g);
    return "$timestamp $message";
}

# TODO: Read Unity installed location from the Registry?
$unityExe = "C:\Program Files\Unity\Editor\Unity.exe";
$unityLog = "C:\Users\$env:UserName\AppData\Local\Unity\Editor\Editor.log";
$packagerProjectPath = "$PSScriptRoot\..\ExporterProject";

& $unityExe -batchmode -quit -createProject $packagerProjectPath | Out-Null;

Write-Log "Copying the needed files into the Packager project...";
Write-Log "PSScriptRoot = $PSScriptRoot";
Write-Log "Packager project path = $packagerProjectPath";

# Copy the needed files into the project
Copy-Item "$PSScriptRoot\..\Assets\NuGet\Resources\defaultIcon.png" $packagerProjectPath\Assets\NuGet\Resources;
Copy-Item "$PSScriptRoot\..\CreateDLL\bin\Debug\NuGetForUnity.dll" $packagerProjectPath\Assets\NuGet\Editor;
Copy-Item "$PSScriptRoot\..\CreateDLL\bin\Debug\DotNetZip.dll" $packagerProjectPath\Assets\NuGet\Editor;
Copy-Item "$PSScriptRoot\..\LICENSE" -Destination $packagerProjectPath\Assets\NuGet\LICENSE.txt;

Write-Log "Exporting .unitypackage ...";

# https://docs.unity3d.com/520/Documentation/Manual/CommandLineArguments.html
# | Out-Null forces PowerShell to wait for the application to finish before continuing
# TODO: Get the version number and append it to the file name?
& $unityExe -batchmode -quit -exportPackage Assets/NuGet NuGetForUnity.unitypackage -projectPath $packagerProjectPath | Out-Null;

# upload the Unity editor log as an artifact
# See: https://www.appveyor.com/docs/packaging-artifacts/
Push-AppveyorArtifact $unityLog;

$unityPackagePath = "$packagerProjectPath\NuGetForUnity.unitypackage";

if (Test-Path $unityPackagePath)
{
    Write-Log "Uploading the build artifact...";

    # upload the .unitypackge file as an artifact
    Push-AppveyorArtifact $unityPackagePath;
}
else
{
    Write-Log "The .unitypackage does not exist: $unityPackagePath";

    throw "Failed to create NuGetForUnity.unitypackage file"
}

Write-Log "DONE!";