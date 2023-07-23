$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path $PSScriptRoot\..
$currentLocation = Get-Location

try {
    Set-Location $repositoryRoot
    . pre-commit run --verbose --all-files --hook-stage manual
}
finally {
    Set-Location $currentLocation
}
