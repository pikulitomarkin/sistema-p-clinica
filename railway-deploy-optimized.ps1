# Script otimizado de deploy para Railway
# Execute: .\railway-deploy-optimized.ps1

Write-Host "Preparando deploy otimizado para Railway..." -ForegroundColor Cyan

# 1. Limpar cache local para garantir build limpo
Write-Host "`n1. Limpando cache local..." -ForegroundColor Yellow
dotnet clean --verbosity minimal

# 2. Remover bin/obj para reduzir tamanho do snapshot
Write-Host "`n2. Removendo bin/obj (reduzir snapshot)..." -ForegroundColor Yellow
Get-ChildItem -Path ".\src" -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# 3. Verificar se railway.toml existe
if (-not (Test-Path "railway.toml")) {
    Write-Host "`nERRO: railway.toml nao encontrado!" -ForegroundColor Red
    exit 1
}

# 4. Verificar tamanho do snapshot
Write-Host "`n3. Verificando tamanho do projeto..." -ForegroundColor Yellow
$totalSize = (Get-ChildItem -Path "." -Recurse -File -Exclude @("*.db","*.db-shm","*.db-wal",".git") | Measure-Object -Property Length -Sum).Sum
$sizeMB = [math]::Round($totalSize/1MB, 2)
Write-Host "   Tamanho estimado: $sizeMB MB" -ForegroundColor Cyan

if ($sizeMB -gt 100) {
    Write-Host "   AVISO: Snapshot maior que 100 MB pode causar timeout!" -ForegroundColor Yellow
}

# 5. Fazer commit das mudanças (se houver)
Write-Host "`n4. Verificando mudancas..." -ForegroundColor Yellow
$hasChanges = git status --porcelain
if ($hasChanges) {
    Write-Host "   Mudancas detectadas. Fazendo commit..." -ForegroundColor Cyan
    git add .
    git commit -m "chore: otimizacoes de build para Railway"
    git push origin main
} else {
    Write-Host "   Nenhuma mudanca detectada." -ForegroundColor Cyan
}

# 6. Instruções para Railway CLI
Write-Host "`n5. Proximo passo: Deploy no Railway" -ForegroundColor Green
Write-Host ""
Write-Host "Opcao 1 - Railway CLI (recomendado):" -ForegroundColor Cyan
Write-Host "  railway up" -ForegroundColor White
Write-Host ""
Write-Host "Opcao 2 - Redeploy via Git:" -ForegroundColor Cyan
Write-Host "  git commit --allow-empty -m 'trigger: redeploy'" -ForegroundColor White
Write-Host "  git push origin main" -ForegroundColor White
Write-Host ""
Write-Host "Opcao 3 - Railway Dashboard:" -ForegroundColor Cyan
Write-Host "  1. Acesse dashboard.railway.app" -ForegroundColor White
Write-Host "  2. Selecione o projeto" -ForegroundColor White
Write-Host "  3. Clique em 'Deploy'" -ForegroundColor White
Write-Host ""

# 7. Monitoramento
Write-Host "6. Monitorar deploy:" -ForegroundColor Green
Write-Host "  railway logs" -ForegroundColor White
Write-Host ""
Write-Host "Otimizacoes aplicadas:" -ForegroundColor Green
Write-Host "  [x] Cache de dependencias (dotnet restore)" -ForegroundColor White
Write-Host "  [x] Build incremental (--no-restore)" -ForegroundColor White
Write-Host "  [x] Verbosity minimal (logs menores)" -ForegroundColor White
Write-Host "  [x] JSONArgs format (fix warning)" -ForegroundColor White
Write-Host "  [x] Timeout estendido (15 min)" -ForegroundColor White
Write-Host ""
