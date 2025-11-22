# Script de Migração de Dados AWS RDS → Railway PostgreSQL
# Data: 21/11/2025

Write-Host "🚂 Migração ClinicaPsi: AWS RDS → Railway PostgreSQL" -ForegroundColor Cyan
Write-Host "=" * 60

# Verificar se pg_dump existe
try {
    $pgVersion = pg_dump --version
    Write-Host "✅ PostgreSQL Client encontrado: $pgVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ PostgreSQL Client não encontrado!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Por favor, instale PostgreSQL Client:" -ForegroundColor Yellow
    Write-Host "https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "📊 Etapa 1: Exportar dados do AWS RDS" -ForegroundColor Yellow
Write-Host "-" * 60

# Credenciais AWS RDS (hardcoded do appsettings.json atual)
$awsHost = "clinicapsi-db.cqbooyc6uuiz.us-east-1.rds.amazonaws.com"
$awsPort = "5432"
$awsDatabase = "clinicapsi"
$awsUser = "psiadmin"
$awsPassword = "1212Ervadoce"

$backupFile = "clinicapsi-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss').sql"

Write-Host "Host: $awsHost" -ForegroundColor Gray
Write-Host "Database: $awsDatabase" -ForegroundColor Gray
Write-Host "Arquivo backup: $backupFile" -ForegroundColor Gray
Write-Host ""

# Confirmar antes de prosseguir
$confirm = Read-Host "Deseja continuar com o backup? (S/N)"
if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "❌ Operação cancelada pelo usuário" -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "⏳ Exportando dados do AWS RDS..." -ForegroundColor Yellow

$env:PGPASSWORD = $awsPassword

try {
    pg_dump -h $awsHost `
            -p $awsPort `
            -U $awsUser `
            -d $awsDatabase `
            --no-owner `
            --no-acl `
            --clean `
            --if-exists `
            -f $backupFile

    if ($LASTEXITCODE -eq 0) {
        $fileSize = (Get-Item $backupFile).Length / 1KB
        Write-Host "✅ Backup criado com sucesso!" -ForegroundColor Green
        Write-Host "   Arquivo: $backupFile" -ForegroundColor Green
        Write-Host "   Tamanho: $([math]::Round($fileSize, 2)) KB" -ForegroundColor Green
    } else {
        throw "Erro ao criar backup"
    }
} catch {
    Write-Host "❌ Erro ao exportar dados: $_" -ForegroundColor Red
    exit 1
} finally {
    $env:PGPASSWORD = ""
}

Write-Host ""
Write-Host "📊 Etapa 2: Importar para Railway PostgreSQL" -ForegroundColor Yellow
Write-Host "-" * 60
Write-Host ""
Write-Host "⚠️  IMPORTANTE: Pegue as credenciais do Railway PostgreSQL" -ForegroundColor Yellow
Write-Host ""
Write-Host "No Railway Dashboard:" -ForegroundColor Cyan
Write-Host "1. Acesse seu projeto" -ForegroundColor White
Write-Host "2. Click no PostgreSQL" -ForegroundColor White
Write-Host "3. Aba 'Connect'" -ForegroundColor White
Write-Host "4. Copie os dados de conexão" -ForegroundColor White
Write-Host ""

# Pedir credenciais Railway
$railwayHost = Read-Host "Railway Host (ex: containers-us-west-xxx.railway.app)"
$railwayPort = Read-Host "Railway Port (ex: 5432)"
$railwayDatabase = Read-Host "Railway Database (ex: railway)"
$railwayUser = Read-Host "Railway User (ex: postgres)"
$railwayPassword = Read-Host "Railway Password" -AsSecureString

# Converter SecureString para texto
$railwayPasswordText = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($railwayPassword)
)

Write-Host ""
Write-Host "⏳ Importando dados para Railway..." -ForegroundColor Yellow

$env:PGPASSWORD = $railwayPasswordText

try {
    psql -h $railwayHost `
         -p $railwayPort `
         -U $railwayUser `
         -d $railwayDatabase `
         -f $backupFile

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Dados importados com sucesso!" -ForegroundColor Green
    } else {
        throw "Erro ao importar dados"
    }
} catch {
    Write-Host "❌ Erro ao importar dados: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Dica: Verifique se as credenciais estão corretas" -ForegroundColor Yellow
    exit 1
} finally {
    $env:PGPASSWORD = ""
}

Write-Host ""
Write-Host "=" * 60
Write-Host "✅ Migração concluída com sucesso!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Próximos passos:" -ForegroundColor Cyan
Write-Host "1. Configure as variáveis de ambiente no Railway" -ForegroundColor White
Write-Host "2. Faça deploy da aplicação" -ForegroundColor White
Write-Host "3. Teste o funcionamento" -ForegroundColor White
Write-Host "4. Configure o domínio customizado" -ForegroundColor White
Write-Host ""
Write-Host "📄 Arquivo de backup criado: $backupFile" -ForegroundColor Gray
Write-Host "   (Guarde este arquivo como backup de segurança!)" -ForegroundColor Gray
Write-Host ""
Write-Host "🎉 Pronto para deploy no Railway!" -ForegroundColor Green
