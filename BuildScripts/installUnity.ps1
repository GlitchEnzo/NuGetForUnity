# First set up the Chocolatey environment so that the Install commandlet is available
# See here: https://stackoverflow.com/questions/35558911/why-is-the-uninstall-chocolateypackage-cmdlet-not-recognized
Write-Host "Setting up the Chocolatey environment...";
#& "C:\ProgramData\chocolatey\helpers\chocolateyInstaller.psm1";
Import-Module -Name "C:\ProgramData\chocolatey\helpers\chocolateyInstaller.psm1" -Verbose;

# The direct URL to the Unity installer:
# https://download.unity3d.com/download_unity/e7947df39b5c/Windows64EditorInstaller/UnitySetup64-5.2.0f3.exe

$version = "5.2.0";
$versionName = "5.2.0f3";
$buildName = "e7947df39b5c";
$packageName = 'unity';
$installerType = 'exe';
$url =      "http://download.unity3d.com/download_unity/{0}/Windows32EditorInstaller/UnitySetup32-{1}.exe" -f $buildName,$versionName;
$url64bit = "http://download.unity3d.com/download_unity/{0}/Windows64EditorInstaller/UnitySetup64-{1}.exe" -f $buildName,$versionName;
$silentArgs = '/S';
$validExitCodes = @(0);

Write-Host "Installing Unity via Chocolatey...";
Install-ChocolateyPackage -packageName $packageName -fileType $installerType -silentArgs $silentArgs -url $url -url64bit $url64bit -validExitCodes $validExitCodes -allowEmptyChecksums;