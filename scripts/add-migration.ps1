param(
    [Parameter(Mandatory = $true)]
    [string]$MigrationName
)

$ProjectPath = "$PSScriptRoot\..\src\Egroo.Server\Egroo.Server.csproj"

Write-Host "Adding migration '$MigrationName'..." -ForegroundColor Cyan

dotnet ef migrations add $MigrationName `
    --project $ProjectPath `
    --context DataContext

if ($LASTEXITCODE -eq 0) {
    Write-Host "Migration '$MigrationName' added successfully." -ForegroundColor Green
} else {
    Write-Host "Failed to add migration." -ForegroundColor Red
    exit $LASTEXITCODE
}
