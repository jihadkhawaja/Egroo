param(
    [string]$CoverageFile = "src/Egroo.Server.Test/TestResults/Coverage/coverage.cobertura.xml",
    [string]$OutputDir = "src/Egroo.Server.Test/TestResults/CoverageReport",
    [string]$Title = "Egroo Server Coverage",
    [string]$ReportTypes = "Html;HtmlSummary;TextSummary;Badges",
    [string]$License = ""
)

$ErrorActionPreference = "Stop"

$resolvedLicense = if ([string]::IsNullOrWhiteSpace($License)) { $env:REPORTGENERATOR_LICENSE } else { $License }

function Get-NuGetPackagesRoot {
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        return $env:NUGET_PACKAGES
    }

    foreach ($homeDirectory in @($env:USERPROFILE, $env:HOME)) {
        if ([string]::IsNullOrWhiteSpace($homeDirectory)) {
            continue
        }

        $candidate = Join-Path $homeDirectory ".nuget/packages"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $nugetLocalsOutput = & dotnet nuget locals global-packages --list 2>$null
    foreach ($line in $nugetLocalsOutput) {
        if ($line -match "global-packages:\s*(.+)$") {
            return $Matches[1].Trim()
        }
    }

    throw "Unable to resolve the NuGet global packages directory."
}

function Get-ReportGeneratorDllPath {
    $packageRoot = Join-Path (Get-NuGetPackagesRoot) "reportgenerator"

    if (-not (Test-Path $packageRoot)) {
        throw "ReportGenerator package directory was not found at $packageRoot"
    }

    $packageDirectories = Get-ChildItem -Path $packageRoot -Directory | Sort-Object {
        try {
            [version]$_.Name
        }
        catch {
            [version]"0.0"
        }
    } -Descending

    foreach ($packageDirectory in $packageDirectories) {
        $candidate = Join-Path $packageDirectory.FullName "tools/net8.0/ReportGenerator.dll"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "ReportGenerator.dll was not found under $packageRoot"
}

$reportGeneratorDll = Get-ReportGeneratorDllPath

if (-not (Test-Path $reportGeneratorDll)) {
    throw "ReportGenerator was not found at $reportGeneratorDll"
}

if (-not (Test-Path $CoverageFile)) {
    throw "Coverage file was not found at $CoverageFile"
}

$previousLicense = $env:REPORTGENERATOR_LICENSE

try {
    if (-not [string]::IsNullOrWhiteSpace($resolvedLicense)) {
        $env:REPORTGENERATOR_LICENSE = $resolvedLicense
    }

    & dotnet $reportGeneratorDll "-reports:$CoverageFile" "-targetdir:$OutputDir" "-reporttypes:$ReportTypes" "-title:$Title"
}
finally {
    if ([string]::IsNullOrWhiteSpace($previousLicense)) {
        Remove-Item Env:REPORTGENERATOR_LICENSE -ErrorAction SilentlyContinue
    }
    else {
        $env:REPORTGENERATOR_LICENSE = $previousLicense
    }
}