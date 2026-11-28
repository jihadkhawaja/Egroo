param(
    [Parameter(Mandatory = $true)]
    [string]$MigrationName,

    [ValidateSet('Both', 'SQLite', 'PostgreSQL')]
    [string]$Provider = 'Both'
)

$projectDirectory = Join-Path $PSScriptRoot '../src/Egroo.Server'
$contexts = switch ($Provider) {
    'SQLite' { 'SqliteDataContext' }
    'PostgreSQL' { 'DataContext' }
    'Both' { 'SqliteDataContext'; 'DataContext' }
}

Push-Location -LiteralPath $projectDirectory
try {
    foreach ($context in $contexts) {
        Write-Host "Adding migration '$MigrationName' for $context..." -ForegroundColor Cyan
        dotnet ef migrations add $MigrationName --context $context
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
} finally {
    Pop-Location
}
