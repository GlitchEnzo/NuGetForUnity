function Write-Log
{
    param([string]$message);
    $timestamp = "[{0}]" -f (Get-Date -Format g);
    return "$timestamp $message";
}

# TODO: Read install location from Registry?
$unityExe = "C:\Program Files\Unity\Editor\Unity.exe";
$exporterProjectPath = "$PSScriptRoot\..\ExporterProject";

Write-Log "Creating the Exporter Unity project...";
Write-Log "PSScriptRoot = $PSScriptRoot";

# https://docs.unity3d.com/520/Documentation/Manual/CommandLineArguments.html
# | Out-Null forces PowerShell to wait for the application to finish before continuing
& $unityExe -batchmode -quit -createProject $exporterProjectPath | Out-Null;

Write-Log "Finished creating the project. Copying the files into the project...";

# Create the folders
New-Item -ItemType directory -Path $exporterProjectPath\Assets\NuGet\Resources
New-Item -ItemType directory -Path $exporterProjectPath\Assets\NuGet\Editor

Copy-Item "$PSScriptRoot\..\Assets\NuGet\Resources\defaultIcon.png" $exporterProjectPath\Assets\NuGet\Resources;
Copy-Item "$PSScriptRoot\..\CreateDLL\bin\Debug\NuGetForUnity.dll" $exporterProjectPath\Assets\NuGet\Editor;
Copy-Item "$PSScriptRoot\..\CreateDLL\bin\Debug\Ionic.Zip.dll" $exporterProjectPath\Assets\NuGet\Editor;
#Copy-Item "$PSScriptRoot\..\CreateDLL\bin\Debug\DotNetZip.dll" $exporterProjectPath\Assets\NuGet\Editor;

Write-Log "Exporting .unitypackage ...";

& $unityExe -batchmode -quit -exportPackage Assets/NuGet NuGetForUnity.unitypackage -projectPath $exporterProjectPath | Out-Null;

Write-Log "DONE!";