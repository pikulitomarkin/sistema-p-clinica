# Script de Verificacao - Docker e AWS
# Verifica se tudo esta pronto para deploy

Write-Host ""
Write-Host "=== Verificacao de Pre-Deploy ===" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# 1. Verificar Docker
Write-Host "1. Verificando Docker Desktop..." -ForegroundColor Yellow
try {
    $dockerVersion = docker version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   OK - Docker esta rodando" -ForegroundColor Green
        $dockerServerVersion = docker version --format '{{.Server.Version}}' 2>$null
        Write-Host "   Versao: $dockerServerVersion" -ForegroundColor Gray
    } else {
        Write-Host "   ERRO - Docker NAO esta rodando" -ForegroundColor Red
        Write-Host "   Inicie o Docker Desktop e aguarde a baleia ficar verde" -ForegroundColor Yellow
        $allGood = $false
    }
} catch {
    Write-Host "   ERRO - Docker nao encontrado ou nao esta rodando" -ForegroundColor Red
    Write-Host "   Inicie o Docker Desktop" -ForegroundColor Yellow
    $allGood = $false
}

Write-Host ""

# 2. Verificar AWS CLI
Write-Host "2. Verificando AWS CLI..." -ForegroundColor Yellow
try {
    $awsVersion = aws --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   OK - AWS CLI instalado" -ForegroundColor Green
        Write-Host "   $awsVersion" -ForegroundColor Gray
    } else {
        Write-Host "   ERRO - AWS CLI nao encontrado" -ForegroundColor Red
        $allGood = $false
    }
} catch {
    Write-Host "   ERRO - AWS CLI nao instalado" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

# 3. Verificar credenciais AWS
Write-Host "3. Verificando credenciais AWS..." -ForegroundColor Yellow
try {
    $identity = aws sts get-caller-identity 2>&1 | ConvertFrom-Json
    Write-Host "   OK - Credenciais validas" -ForegroundColor Green
    Write-Host "   Account: $($identity.Account)" -ForegroundColor Gray
    Write-Host "   User: $($identity.Arn)" -ForegroundColor Gray
} catch {
    Write-Host "   ERRO - Credenciais AWS nao configuradas ou invalidas" -ForegroundColor Red
    Write-Host "   Execute: aws configure" -ForegroundColor Yellow
    $allGood = $false
}

Write-Host ""

# 4. Verificar acesso ao ECR
Write-Host "4. Verificando acesso ao ECR..." -ForegroundColor Yellow
try {
    $repo = aws ecr describe-repositories --repository-names clinicapsi --region us-east-1 2>&1 | ConvertFrom-Json
    Write-Host "   OK - Repositorio ECR acessivel" -ForegroundColor Green
    Write-Host "   URI: $($repo.repositories[0].repositoryUri)" -ForegroundColor Gray
} catch {
    Write-Host "   ERRO - Nao foi possivel acessar repositorio ECR" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

# 5. Verificar Dockerfile
Write-Host "5. Verificando Dockerfile..." -ForegroundColor Yellow
if (Test-Path "src/ClinicaPsi.Web/Dockerfile") {
    Write-Host "   OK - Dockerfile encontrado" -ForegroundColor Green
} else {
    Write-Host "   ERRO - Dockerfile nao encontrado em src/ClinicaPsi.Web/Dockerfile" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

# 6. Verificar ECS Cluster
Write-Host "6. Verificando ECS Cluster..." -ForegroundColor Yellow
try {
    $cluster = aws ecs describe-clusters --clusters clinicapsi-cluster --region us-east-1 2>&1 | ConvertFrom-Json
    $status = $cluster.clusters[0].status
    if ($status -eq "ACTIVE") {
        Write-Host "   OK - Cluster ECS esta ATIVO" -ForegroundColor Green
        Write-Host "   Running tasks: $($cluster.clusters[0].runningTasksCount)" -ForegroundColor Gray
    } else {
        Write-Host "   AVISO - Cluster status: $status" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ERRO - Nao foi possivel verificar cluster ECS" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

# 7. Verificar ECS Service
Write-Host "7. Verificando ECS Service..." -ForegroundColor Yellow
try {
    $service = aws ecs describe-services --cluster clinicapsi-cluster --services clinicapsi-service --region us-east-1 2>&1 | ConvertFrom-Json
    $svcStatus = $service.services[0].status
    if ($svcStatus -eq "ACTIVE") {
        Write-Host "   OK - Service ECS esta ATIVO" -ForegroundColor Green
        Write-Host "   Desired count: $($service.services[0].desiredCount)" -ForegroundColor Gray
        Write-Host "   Running count: $($service.services[0].runningCount)" -ForegroundColor Gray
    } else {
        Write-Host "   AVISO - Service status: $svcStatus" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ERRO - Nao foi possivel verificar service ECS" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

# 8. Verificar Target Group Health
Write-Host "8. Verificando Target Group..." -ForegroundColor Yellow
try {
    $tgArn = "arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/f84f061a24c7ec0f"
    $health = aws elbv2 describe-target-health --target-group-arn $tgArn --region us-east-1 2>&1 | ConvertFrom-Json
    $healthyCount = ($health.TargetHealthDescriptions | Where-Object { $_.TargetHealth.State -eq "healthy" }).Count
    $totalCount = $health.TargetHealthDescriptions.Count
    
    if ($healthyCount -eq $totalCount -and $healthyCount -gt 0) {
        Write-Host "   OK - Todos os targets estao healthy ($healthyCount/$totalCount)" -ForegroundColor Green
    } elseif ($healthyCount -gt 0) {
        Write-Host "   AVISO - Alguns targets nao estao healthy ($healthyCount/$totalCount)" -ForegroundColor Yellow
    } else {
        Write-Host "   ERRO - Nenhum target healthy" -ForegroundColor Red
    }
} catch {
    Write-Host "   ERRO - Nao foi possivel verificar target group" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

if ($allGood) {
    Write-Host "OK - TUDO PRONTO! Pode fazer deploy!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Execute agora:" -ForegroundColor Cyan
    Write-Host "  .\deploy.ps1" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "ERRO - Existem problemas que precisam ser corrigidos" -ForegroundColor Red
    Write-Host ""
    Write-Host "Principais verificacoes:" -ForegroundColor Yellow
    Write-Host "  1. Docker Desktop deve estar rodando (icone da baleia verde)" -ForegroundColor White
    Write-Host "  2. AWS CLI deve estar instalado e configurado" -ForegroundColor White
    Write-Host "  3. Credenciais AWS devem estar validas" -ForegroundColor White
    Write-Host ""
}
