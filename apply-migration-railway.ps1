# Script para aplicar migration usando Railway CLI
Write-Host ""
Write-Host "Aplicando migration da coluna Formato no Railway" -ForegroundColor Cyan
Write-Host ""

# Ler connection string do arquivo
$connFile = "railway-connection.txt"
if (Test-Path $connFile) {
    Write-Host "Lendo arquivo railway-connection.txt..." -ForegroundColor Yellow
    $allLines = Get-Content $connFile
    # Pegar apenas a primeira linha que começa com postgresql://
    $connectionString = ($allLines | Where-Object { $_ -match '^postgresql://' } | Select-Object -First 1).Trim()
    
    if ($connectionString) {
        Write-Host "Connection string encontrada!" -ForegroundColor Green
    } else {
        Write-Host "Connection string invalida no arquivo!" -ForegroundColor Red
        Write-Host "O arquivo deve conter apenas a connection string na primeira linha" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "Arquivo railway-connection.txt NAO encontrado" -ForegroundColor Red
    Write-Host ""
    Write-Host "Crie o arquivo e cole a connection string PUBLICA do Railway" -ForegroundColor Yellow
    Write-Host "Exemplo: postgresql://postgres:senha@switchyard.proxy.rlwy.net:35011/railway" -ForegroundColor Gray
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
