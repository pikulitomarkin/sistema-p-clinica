#!/usr/bin/env pwsh
# Script para verificar status do Railway

Write-Host "`n===== VERIFICAÇÃO RAILWAY =====" -ForegroundColor Cyan

Write-Host "`n📋 CHECKLIST:" -ForegroundColor Yellow

Write-Host "`n1. DATABASE_URL configurada no Railway?" -ForegroundColor White
Write-Host "   Vá em: Railway Dashboard → Seu projeto → Variables" -ForegroundColor Gray
Write-Host "   Adicione um PostgreSQL plugin e copie DATABASE_URL" -ForegroundColor Gray

Write-Host "`n2. Variáveis de ambiente Railway:" -ForegroundColor White
Write-Host "   DATABASE_URL=postgresql://user:pass@host:5432/dbname" -ForegroundColor Gray
Write-Host "   ASPNETCORE_ENVIRONMENT=Production" -ForegroundColor Gray

Write-Host "`n3. Build bem-sucedido mas app não responde?" -ForegroundColor White
Write-Host "   PROBLEMA COMUM: Connection string AWS ainda configurada!" -ForegroundColor Red
Write-Host "   SOLUÇÃO: Railway sobrescreve com DATABASE_URL" -ForegroundColor Green

Write-Host "`n===== ERROS COMUNS =====" -ForegroundColor Cyan

Write-Host "`n[X] Application failed to respond" -ForegroundColor Red
Write-Host "   CAUSA 1: App tenta conectar ao AWS RDS" -ForegroundColor Yellow
Write-Host "   CAUSA 2: DATABASE_URL nao configurada no Railway" -ForegroundColor Yellow
Write-Host "   CAUSA 3: App quebra ao executar DbInitializer.SeedAsync" -ForegroundColor Yellow

Write-Host "`n✅ SOLUÇÃO:" -ForegroundColor Green
Write-Host "   1. No Railway Dashboard, adicione PostgreSQL plugin" -ForegroundColor White
Write-Host "   2. Copie DATABASE_URL do plugin" -ForegroundColor White
Write-Host "   3. Adicione como variável em Variables" -ForegroundColor White
Write-Host "   4. Faça redeploy (Railway detecta automaticamente)" -ForegroundColor White

Write-Host "`n===== COMANDOS ÚTEIS =====" -ForegroundColor Cyan
Write-Host "   Ver logs: Acesse Railway Dashboard → Deployments → View logs" -ForegroundColor White
Write-Host "   Redeploy: Push qualquer commit ou clique 'Redeploy' no dashboard" -ForegroundColor White

Write-Host "`n===== PRÓXIMOS PASSOS =====" -ForegroundColor Cyan
Write-Host "   1. ✅ Build bem-sucedido!" -ForegroundColor Green
Write-Host "   2. ⏳ Configurar DATABASE_URL no Railway" -ForegroundColor Yellow
Write-Host "   3. ⏳ Redeploy após configurar banco" -ForegroundColor Yellow
Write-Host "   4. ⏳ Testar URL Railway" -ForegroundColor Yellow
Write-Host ""
