# Script de Informações Rápidas - Railway Setup

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  🚂 RAILWAY SETUP - STATUS E INFORMAÇÕES                      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

Write-Host "`n📦 ARQUIVOS RAILWAY:" -ForegroundColor Yellow
Get-ChildItem *railway*, *Railway*, nixpacks.toml, .railwayignore, .env.railway -ErrorAction SilentlyContinue | 
    Select-Object Name, @{N="Tamanho";E={"{0:N2} KB" -f ($_.Length/1KB)}}, LastWriteTime |
    Format-Table -AutoSize

Write-Host "`n💰 COMPARAÇÃO DE CUSTOS:" -ForegroundColor Yellow
Write-Host "┌─────────────────┬──────────────┬──────────────┐" -ForegroundColor Gray
Write-Host "│ Plataforma      │ Custo/mês    │ Custo/ano    │" -ForegroundColor White
Write-Host "├─────────────────┼──────────────┼──────────────┤" -ForegroundColor Gray
Write-Host "│ AWS (atual)     │ " -NoNewline -ForegroundColor White
Write-Host "R$ 500       " -NoNewline -ForegroundColor Red
Write-Host "│ " -NoNewline -ForegroundColor White
Write-Host "R$ 6.000     " -NoNewline -ForegroundColor Red
Write-Host "│" -ForegroundColor White
Write-Host "│ Railway         │ " -NoNewline -ForegroundColor White
Write-Host "R$ 25        " -NoNewline -ForegroundColor Green
Write-Host "│ " -NoNewline -ForegroundColor White
Write-Host "R$ 300       " -NoNewline -ForegroundColor Green
Write-Host "│" -ForegroundColor White
Write-Host "├─────────────────┼──────────────┼──────────────┤" -ForegroundColor Gray
Write-Host "│ " -NoNewline -ForegroundColor White
Write-Host "ECONOMIA        " -NoNewline -ForegroundColor Yellow
Write-Host "│ " -NoNewline -ForegroundColor White
Write-Host "R$ 475       " -NoNewline -ForegroundColor Green
Write-Host "│ " -NoNewline -ForegroundColor White
Write-Host "R$ 5.700     " -NoNewline -ForegroundColor Green
Write-Host "│" -ForegroundColor White
Write-Host "└─────────────────┴──────────────┴──────────────┘" -ForegroundColor Gray

Write-Host "`n📚 DOCUMENTAÇÃO DISPONÍVEL:" -ForegroundColor Yellow
$docs = @(
    @{Nome="RAILWAY-README.md"; Desc="⭐ COMECE AQUI - Resumo executivo"},
    @{Nome="RAILWAY-QUICKSTART.md"; Desc="🚀 Deploy em 10 minutos"},
    @{Nome="RAILWAY-DEPLOY.md"; Desc="📖 Guia completo passo-a-passo"},
    @{Nome="RAILWAY-MIGRATION-ANALYSIS.md"; Desc="💰 Análise detalhada de custos"}
)

foreach ($doc in $docs) {
    if (Test-Path $doc.Nome) {
        Write-Host "   ✅ " -NoNewline -ForegroundColor Green
        Write-Host "$($doc.Nome.PadRight(35))" -NoNewline -ForegroundColor Cyan
        Write-Host " $($doc.Desc)" -ForegroundColor Gray
    } else {
        Write-Host "   ❌ " -NoNewline -ForegroundColor Red
        Write-Host "$($doc.Nome.PadRight(35))" -NoNewline -ForegroundColor Red
        Write-Host " Não encontrado" -ForegroundColor Gray
    }
}

Write-Host "`n🛠️  SCRIPTS DISPONÍVEIS:" -ForegroundColor Yellow
$scripts = @(
    @{Nome="migrate-to-railway.ps1"; Desc="Migrar dados AWS → Railway"},
    @{Nome="deploy-railway.ps1"; Desc="Deploy via Railway CLI"}
)

foreach ($script in $scripts) {
    if (Test-Path $script.Nome) {
        Write-Host "   ✅ " -NoNewline -ForegroundColor Green
        Write-Host "$($script.Nome.PadRight(35))" -NoNewline -ForegroundColor Cyan
        Write-Host " $($script.Desc)" -ForegroundColor Gray
    } else {
        Write-Host "   ❌ " -NoNewline -ForegroundColor Red
        Write-Host "$($script.Nome.PadRight(35))" -NoNewline -ForegroundColor Red
        Write-Host " Não encontrado" -ForegroundColor Gray
    }
}

Write-Host "`n✅ CHECKLIST PRÉ-DEPLOY:" -ForegroundColor Yellow
$checklist = @(
    @{Item="Conta Railway criada (https://railway.app)"; Done=$false},
    @{Item="GitHub conectado ao Railway"; Done=$false},
    @{Item="PostgreSQL Client instalado"; Done=$false},
    @{Item="Backup do banco AWS feito"; Done=$false},
    @{Item="Lido RAILWAY-README.md"; Done=$false}
)

foreach ($item in $checklist) {
    Write-Host "   ⬜ " -NoNewline -ForegroundColor Gray
    Write-Host "$($item.Item)" -ForegroundColor White
}

Write-Host "`n🎯 PRÓXIMOS PASSOS:" -ForegroundColor Yellow
Write-Host "   1. " -NoNewline -ForegroundColor White
Write-Host "code RAILWAY-README.md" -ForegroundColor Cyan
Write-Host "      (Abrir documentação principal)" -ForegroundColor Gray

Write-Host "`n   2. " -NoNewline -ForegroundColor White
Write-Host "Criar conta: https://railway.app" -ForegroundColor Cyan
Write-Host "      (Login com GitHub)" -ForegroundColor Gray

Write-Host "`n   3. " -NoNewline -ForegroundColor White
Write-Host ".\migrate-to-railway.ps1" -ForegroundColor Cyan
Write-Host "      (Migrar dados do AWS)" -ForegroundColor Gray

Write-Host "`n   4. " -NoNewline -ForegroundColor White
Write-Host "Deploy no Railway Dashboard" -ForegroundColor Cyan
Write-Host "      (New Project → Deploy from GitHub)" -ForegroundColor Gray

Write-Host "`n📊 INFORMAÇÕES DO PROJETO:" -ForegroundColor Yellow
Write-Host "   Nome:         " -NoNewline -ForegroundColor Gray
Write-Host "ClinicaPsi" -ForegroundColor White
Write-Host "   Framework:    " -NoNewline -ForegroundColor Gray
Write-Host ".NET 9.0" -ForegroundColor White
Write-Host "   Tipo:         " -NoNewline -ForegroundColor Gray
Write-Host "Blazor Server" -ForegroundColor White
Write-Host "   Banco:        " -NoNewline -ForegroundColor Gray
Write-Host "PostgreSQL 15" -ForegroundColor White
Write-Host "   Repositório:  " -NoNewline -ForegroundColor Gray
Write-Host "https://github.com/pikulitomarkin/sistema-p-clinica" -ForegroundColor Cyan

Write-Host "`n🔗 LINKS ÚTEIS:" -ForegroundColor Yellow
Write-Host "   Railway:      " -NoNewline -ForegroundColor Gray
Write-Host "https://railway.app" -ForegroundColor Cyan
Write-Host "   Docs Railway: " -NoNewline -ForegroundColor Gray
Write-Host "https://docs.railway.app" -ForegroundColor Cyan
Write-Host "   Discord:      " -NoNewline -ForegroundColor Gray
Write-Host "https://discord.gg/railway" -ForegroundColor Cyan
Write-Host "   GitHub:       " -NoNewline -ForegroundColor Gray
Write-Host "https://github.com/pikulitomarkin/sistema-p-clinica" -ForegroundColor Cyan

Write-Host "`n═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   Tempo estimado de migração: " -NoNewline -ForegroundColor Gray
Write-Host "10-15 minutos ⏱️" -ForegroundColor Green
Write-Host "   Economia anual esperada:    " -NoNewline -ForegroundColor Gray
Write-Host "R$ 5.700 💰" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════`n" -ForegroundColor Cyan
