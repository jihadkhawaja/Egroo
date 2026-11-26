param(
    [string]$Migration = '',

    [ValidateSet('SQLite', 'PostgreSQL')]
    [string]$Provider = 'SQLite'
)

$projectDirectory = Join-Path $PSScriptRoot '../src/Egroo.Server'
$context = if ($Provider -eq 'SQLite') { 'SqliteDataContext' } else { 'DataContext' }
[string[]]$migrationArgs = if ($Migration) { @($Migration) } else { @() }

Push-Location -LiteralPath $projectDirectory
try {
    dotnet ef database update @migrationArgs --context $context
    $updateExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

exit $updateExitCode
