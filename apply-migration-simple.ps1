# Script para aplicar migration no Railway
Write-Host ""
Write-Host "Aplicando migration da coluna Formato no Railway" -ForegroundColor Cyan
Write-Host ""

# Pedir connection string
Write-Host "Cole a CONNECTION STRING do Railway PostgreSQL:" -ForegroundColor Yellow
Write-Host "Encontre em: Railway -> PostgreSQL -> Connect -> DATABASE_URL" -ForegroundColor Gray
Write-Host ""
$connectionString = Read-Host "Connection String"

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    Write-Host ""
    Write-Host "Connection string vazia. Abortando." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Aplicando migration usando Entity Framework..." -ForegroundColor Yellow
Write-Host ""

# Executar dotnet ef database update
dotnet ef database update --project src/ClinicaPsi.Infrastructure/ClinicaPsi.Infrastructure.csproj --startup-project src/ClinicaPsi.Web/ClinicaPsi.Web.csproj --connection "$connectionString" --verbose

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "SUCESSO! Migration aplicada." -ForegroundColor Green
    Write-Host "Aguarde 1-2 minutos e teste as paginas do cliente" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "Erro ao aplicar migration." -ForegroundColor Red
    Write-Host ""
}

Write-Host "Pressione Enter para sair..."
Read-Host
