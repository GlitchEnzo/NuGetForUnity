$ErrorActionPreference = 'Stop'

$packageJsonFilePath = 'src/NuGetForUnity/package.json'
$nugetPreferencesCsFilePath = 'src/NuGetForUnity/Editor/Ui/NugetPreferences.cs'

$oldLocation = Get-Location
Set-Location $PSScriptRoot/..

try {
    $newVersionNumber = Read-Host -Prompt "New version number"
    $packageJsonContent = Get-Content -Raw -Path $packageJsonFilePath
    $packageJsonContent = $packageJsonContent -replace '("version": ")[^"]+(")', "`${1}$newVersionNumber`${2}"
    Set-Content -Value $packageJsonContent -Path $packageJsonFilePath -Encoding utf8NoBOM -NoNewline

    $nugetPreferencesCsFileContent = Get-Content -Raw -Path $nugetPreferencesCsFilePath
    $nugetPreferencesCsFileContent = $nugetPreferencesCsFileContent -replace '(public const string NuGetForUnityVersion = ")[^"]+(";)', "`${1}$newVersionNumber`${2}"
    Set-Content -Value $nugetPreferencesCsFileContent -Path $nugetPreferencesCsFilePath -Encoding utf8BOM -NoNewline
}
finally {
    Set-Location $oldLocation
}
