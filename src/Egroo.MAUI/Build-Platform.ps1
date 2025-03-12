# Remove the parameter block and prompt for input interactively

$allowedTargets = @("ios", "android", "windows", "windows-arm64", "macos")
$targetInput = Read-Host "Enter target platform (ios, android, windows, windows-arm64, macos) [default: windows]"
if ([string]::IsNullOrWhiteSpace($targetInput)) {
    $Target = "windows"
} elseif ($allowedTargets -contains $targetInput) {
    $Target = $targetInput
} else {
    Write-Error "Invalid target '$targetInput'"
    exit 1
}

$allowedConfigurations = @("Debug", "Release")
$configInput = Read-Host "Enter configuration (Debug, Release) [default: Debug]"
if ([string]::IsNullOrWhiteSpace($configInput)) {
    $Configuration = "Debug"
} elseif ($allowedConfigurations -contains $configInput) {
    $Configuration = $configInput
} else {
    Write-Error "Invalid configuration '$configInput'"
    exit 1
}

switch ($Target) {
    "windows" {
        $runtime = "win-x64"
        $framework = "net8.0-windows10.0.19041.0"
        $platform = "x64"
    }
    "windows-arm64" {
        $runtime = "win-arm64"
        $framework = "net8.0-windows10.0.19041.0"
        $platform = "Arm64"
    }
    "macos" {
        $runtime = "osx-x64"
        $framework = "net8.0-macos10.15"
        $platform = "x64"
    }
    "ios" {
        $runtime = "ios"
        $framework = "net8.0-ios"
        $platform = $null
    }
    "android" {
        $runtime = "android"
        $framework = "net8.0-android"
        $platform = $null
    }
    default {
        Write-Error "Unsupported target '$Target'"
        exit 1
    }
}

$msbuildProperties = "-p:UseMonoRuntime=false"
if ($platform) {
    $msbuildProperties += " -p:Platform=$platform"
}

$buildCmd = "dotnet build -r $runtime -f $framework -c $Configuration $msbuildProperties"
Write-Host "Building for $Target with runtime $runtime, framework $framework and configuration $Configuration"
Write-Host "Executing: $buildCmd"
Invoke-Expression $buildCmd
pause