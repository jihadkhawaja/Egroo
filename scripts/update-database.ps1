param(
    [string]$Migration = ""
)

$ProjectPath = "$PSScriptRoot\..\src\Egroo.Server\Egroo.Server.csproj"

if ($Migration -ne "") {
    Write-Host "Updating database to migration '$Migration'..." -ForegroundColor Cyan
    dotnet ef database update $Migration `
        --project $ProjectPath `
        --context DataContext
} else {
    Write-Host "Updating database to latest migration..." -ForegroundColor Cyan
    dotnet ef database update `
        --project $ProjectPath `
        --context DataContext
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Database updated successfully." -ForegroundColor Green
} else {
    Write-Host "Failed to update database." -ForegroundColor Red
    exit $LASTEXITCODE
}
