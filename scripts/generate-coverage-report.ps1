param(
    [string]$CoverageFile = "src/Egroo.Server.Test/TestResults/Coverage/coverage.cobertura.xml",
    [string]$OutputDir = "src/Egroo.Server.Test/TestResults/CoverageReport",
    [string]$Title = "Egroo Server Coverage",
    [string]$ReportTypes = "Html;HtmlSummary;TextSummary;Badges",
    [string]$License = ""
)

$ErrorActionPreference = "Stop"

$resolvedLicense = if ([string]::IsNullOrWhiteSpace($License)) { $env:REPORTGENERATOR_LICENSE } else { $License }

$reportGeneratorDll = Join-Path $env:USERPROFILE ".nuget\packages\reportgenerator\5.4.6\tools\net8.0\ReportGenerator.dll"

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