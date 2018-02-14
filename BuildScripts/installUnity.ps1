function Write-Log
{
    param([string]$message);
    $timestamp = "[{0}]" -f (Get-Date -Format g);
    return "$timestamp $message";
}

# Get direct URLs to the Unity installers here:
# https://unity3d.com/get-unity/download/archive
#$url = "https://download.unity3d.com/download_unity/e7947df39b5c/Windows64EditorInstaller/UnitySetup64-5.2.0f3.exe";
$url = "https://download.unity3d.com/download_unity/960ebf59018a/Windows64EditorInstaller/UnitySetup64-5.3.5f1.exe";

Write-Log "Downloading the Unity installer from `n $url";

# 30 minute timeout
$timeout = 30 * 60 * 1000; 

# Start-FileDownload is a cmdlet provided by AppVeyor that downloads a file into the current folder
# See here: https://www.appveyor.com/docs/how-to/download-file/
Start-FileDownload $url -Timeout $timeout;

Write-Log "Finished downloading. Running Unity installer...";

# https://docs.unity3d.com/Manual/InstallingUnity.html
# /S = Performs a silent (no questions asked) install.
# | Out-Null forces PowerShell to wait for the installer to finish before continuing
#.\UnitySetup64-5.2.0f3.exe /S | Out-Null
.\UnitySetup64-5.3.5f1.exe /S | Out-Null

Write-Log "Finished installing Unity.";

# Unity should now be installed in C:\Program Files\Unity\