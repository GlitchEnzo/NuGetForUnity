$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path $PSScriptRoot\..
$currentLocation = Get-Location

try {
    Set-Location $repositoryRoot
    . pre-commit run --verbose
}
finally {
    Set-Location $currentLocation
}
