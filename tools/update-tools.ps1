$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path $PSScriptRoot\..
$currentLocation = Get-Location

function Get-LatestVersionGitHubTag {
    param (
        [string]$Organization,
        [string]$Repository
    )

    foreach ($pageSize in @(10, 100)) {
        $lastTags = (Invoke-WebRequest -Method GET -Uri https://api.github.com/repos/$Organization/$Repository/tags?per_page=$pageSize).Content | ConvertFrom-Json
        $latestStableTag = $lastTags | Where-Object { $_.name -match '^v?[\d\.]+$' }
        if ($latestStableTag -and $latestStableTag[0].name -match "v?([\d\.]+$)") {
            return $Matches[1]
        }
    }

    throw "Unable to find latest tag for GitHub repository $Organization/$Repository"
}

try {
    Set-Location $repositoryRoot
    Write-Host -ForegroundColor Green "Update ReSharper global tool"
    . dotnet tool update JetBrains.ReSharper.GlobalTools
    Write-Host -ForegroundColor Green "Update pre-commit"
    . pip install --upgrade pre-commit --user
    . pre-commit autoupdate
    $content = Get-Content -Path ".\.pre-commit-config.yaml" -Raw
    if ($content -match "(?smi)repo: https://github.com/pre-commit/mirrors-prettier\s*rev:\s*v([\d.]+)")
    {
        $prettierVersion = $Matches[1]
        $content = $content -replace "prettier@v([\d.]+)", "prettier@v$prettierVersion"

        $xrmlPluginVersion = Get-LatestVersionGitHubTag -Organization "prettier" -Repository "plugin-xml"
        $content = $content -replace "@prettier/plugin-xml@([\d.]+)", "@prettier/plugin-xml@$xrmlPluginVersion"

        $iniPluginVersion = Get-LatestVersionGitHubTag -Organization "kddnewton" -Repository "prettier-plugin-ini"
        $content = $content -replace "prettier-plugin-ini@v([\d.]+)", "prettier-plugin-ini@v$iniPluginVersion"
    }

    Write-Host -ForegroundColor Green "Fromat config with pre-commit"
    Set-Content -Path ".\.pre-commit-config.yaml" -Value $content
    . pre-commit run --verbose --files .\.pre-commit-config.yaml .\.config\dotnet-tools.json
}
finally {
    Set-Location $currentLocation
}
