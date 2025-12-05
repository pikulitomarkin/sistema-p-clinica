# Script para limpar arquivos temporários e otimizar o workspace
# Execute: .\cleanup-workspace.ps1

Write-Host "🧹 Limpando workspace para otimizar VS Code..." -ForegroundColor Cyan

# Limpar diretórios bin e obj
Write-Host "`n📦 Limpando bin/ e obj/..." -ForegroundColor Yellow
Get-ChildItem -Path ".\src" -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Limpar arquivos de banco de dados temporários
Write-Host "`n🗄️ Limpando arquivos de banco temporários..." -ForegroundColor Yellow
Get-ChildItem -Path "." -Include *.db-shm,*.db-wal -Recurse -File | Remove-Item -Force -ErrorAction SilentlyContinue

# Limpar diretório .vs
Write-Host "`n🔧 Limpando cache do Visual Studio..." -ForegroundColor Yellow
if (Test-Path ".\.vs") {
    Remove-Item -Path ".\.vs" -Recurse -Force -ErrorAction SilentlyContinue
}

# Limpar OmniSharp cache
Write-Host "`n⚙️ Limpando cache do OmniSharp..." -ForegroundColor Yellow
if (Test-Path "$env:LOCALAPPDATA\OmniSharp") {
    Get-ChildItem -Path "$env:LOCALAPPDATA\OmniSharp" -Filter "*ClinicaPsi*" -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# Estatísticas
Write-Host "`n✅ Limpeza concluída!" -ForegroundColor Green
Write-Host "`n💡 Próximos passos:" -ForegroundColor Cyan
Write-Host "  1. Feche e reabra o VS Code"
Write-Host "  2. Execute: dotnet restore"
Write-Host "  3. Execute: dotnet build"
Write-Host ""
Write-Host "🚀 O VS Code deve iniciar mais rápido agora!" -ForegroundColor Green
