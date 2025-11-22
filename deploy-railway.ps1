# Script rápido de deploy no Railway
# Certifique-se de ter o Railway CLI instalado: npm install -g @railway/cli

Write-Host "🚂 Deploy Rápido - Railway" -ForegroundColor Cyan
Write-Host "=" * 60
Write-Host ""

# Verificar se Railway CLI está instalado
try {
    railway --version | Out-Null
    Write-Host "✅ Railway CLI encontrado" -ForegroundColor Green
} catch {
    Write-Host "❌ Railway CLI não encontrado!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Instale com: npm install -g @railway/cli" -ForegroundColor Yellow
    Write-Host "Ou visite: https://docs.railway.app/develop/cli" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Login no Railway
Write-Host "🔐 Fazendo login no Railway..." -ForegroundColor Yellow
railway login

Write-Host ""

# Link do projeto
Write-Host "🔗 Conectando ao projeto..." -ForegroundColor Yellow
railway link

Write-Host ""

# Verificar variáveis de ambiente
Write-Host "📋 Variáveis de ambiente configuradas:" -ForegroundColor Yellow
railway variables

Write-Host ""
Write-Host "⚠️  Verifique se as variáveis necessárias estão configuradas:" -ForegroundColor Yellow
Write-Host "   - ConnectionStrings__DefaultConnection" -ForegroundColor White
Write-Host "   - ASPNETCORE_ENVIRONMENT=Production" -ForegroundColor White
Write-Host "   - ASPNETCORE_URLS=http://+:`$PORT" -ForegroundColor White
Write-Host ""

$confirm = Read-Host "Tudo configurado? Continuar com deploy? (S/N)"
if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "❌ Deploy cancelado" -ForegroundColor Red
    exit 0
}

Write-Host ""

# Deploy
Write-Host "🚀 Iniciando deploy..." -ForegroundColor Yellow
railway up

Write-Host ""
Write-Host "✅ Deploy concluído!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Ver logs:" -ForegroundColor Cyan
Write-Host "   railway logs" -ForegroundColor White
Write-Host ""
Write-Host "🌐 Abrir no browser:" -ForegroundColor Cyan
Write-Host "   railway open" -ForegroundColor White
Write-Host ""
Write-Host "📈 Ver métricas:" -ForegroundColor Cyan
Write-Host "   Acesse: https://railway.app/dashboard" -ForegroundColor White
