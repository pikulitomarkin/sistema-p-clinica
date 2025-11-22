Write-Host '
========================================
   RAILWAY SETUP - PRONTO PARA DEPLOY
========================================
' -ForegroundColor Green

Write-Host 'ARQUIVOS CRIADOS:' -ForegroundColor Yellow
ls *railway*, nixpacks.toml, .railwayignore, .env.railway -ErrorAction SilentlyContinue | ft Name, Length

Write-Host '
ECONOMIA ESPERADA:' -ForegroundColor Yellow
Write-Host '  AWS Atual:  R$' 500'/mes' -ForegroundColor Red
Write-Host '  Railway:    R$' 25'/mes' -ForegroundColor Green  
Write-Host '  Economia:   R$' 475'/mes (95%)' -ForegroundColor Green

Write-Host '
PROXIMO PASSO:' -ForegroundColor Yellow
Write-Host '  1. Abra: RAILWAY-README.md' -ForegroundColor Cyan
Write-Host '  2. Acesse: https://railway.app' -ForegroundColor Cyan
Write-Host '  3. Execute: .\migrate-to-railway.ps1' -ForegroundColor Cyan
