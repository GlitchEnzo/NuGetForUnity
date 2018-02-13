function Write-Log
{
    param([string]$message);
    $timestamp = "[{0}]" -f (Get-Date -Format g);
    return "$timestamp $message";
}

# TODO: Read Unity installed location from the Registry?
$unityExe = "C:\Program Files\Unity\Editor\Unity.exe";
$packagerProjectPath = "$PSScriptRoot\..\Packager";

Write-Log "Copying the needed files into the Packager project...";
Write-Log "PSScriptRoot = $PSScriptRoot";
Write-Log $packagerProjectPath;

# Copy the needed files into the project
Copy-Item "$PSScriptRoot\..\Assets\NuGet\Resources\defaultIcon.png" $packagerProjectPath\Assets\NuGet\Resources;
Copy-Item "$PSScriptRoot\..\CreateDLL\bin\Debug\NuGetForUnity.dll" $packagerProjectPath\Assets\NuGet\Editor;
Copy-Item "$PSScriptRoot\..\CreateDLL\bin\Debug\DotNetZip.dll" $packagerProjectPath\Assets\NuGet\Editor;
Copy-Item "$PSScriptRoot\..\LICENSE" -Destination $packagerProjectPath\Assets\NuGet\LICENSE.txt;

Write-Log "Exporting .unitypackage ...";

# TODO: Get the version number and append it to the file name?
& $unityExe -force-free -batchmode -quit -exportPackage Assets/NuGet NuGetForUnity.unitypackage -projectPath $packagerProjectPath | Out-Null;

$unityPackagePath = "$packagerProjectPath\NuGetForUnity.unitypackage";

if (Test-Path $unityPackagePath)
{
    Write-Log "Uploading the build artifact...";

    # Push-AppveyorArtifact uploads a file as a build artifact that is visible in the build webpage
    # See: https://www.appveyor.com/docs/packaging-artifacts/
    Push-AppveyorArtifact $unityPackagePath;
}
else
{
    Write-Log "The .unitypackage does not exist: $unityPackagePath";

    Get-ChildItem -Path C:\Users\$env:UserName

    # since there was a failure somewhere, push the Unity editor log as an artifact
    Push-AppveyorArtifact "C:\Users\$env:UserName\AppData\Local\Unity\Editor\Editor.log";
}

Write-Log "DONE!";