# Script de Deploy - PsiiAnaSantos
# Este script faz build, push da imagem Docker e atualiza o ECS

Write-Host "=== Deploy PsiiAnaSantos para AWS ECS ===" -ForegroundColor Green
Write-Host ""

# Verificar se Docker esta rodando
Write-Host "Verificando Docker..." -ForegroundColor Yellow
try {
    docker version | Out-Null
    Write-Host "OK - Docker esta rodando" -ForegroundColor Green
} catch {
    Write-Host "ERRO - Docker nao esta rodando. Inicie o Docker Desktop e tente novamente." -ForegroundColor Red
    exit 1
}

# Variaveis
$timestamp = (Get-Date).ToString('yyyyMMddHHmm')
$tag = "v3.6.1-$timestamp"
$ecr = "507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi"
$imageUri = "${ecr}:$tag"
$region = "us-east-1"

Write-Host ""
Write-Host "Configuracao:" -ForegroundColor Cyan
Write-Host "  Tag: $tag"
Write-Host "  ECR: $ecr"
Write-Host "  Regiao: $region"
Write-Host ""

# 1. Build da imagem
Write-Host "1. Construindo imagem Docker..." -ForegroundColor Yellow
docker build -t clinicapsi:$tag -f src/ClinicaPsi.Web/Dockerfile .

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO - Falha no build da imagem" -ForegroundColor Red
    exit 1
}
Write-Host "OK - Build concluido" -ForegroundColor Green

# 2. Tag para ECR
Write-Host ""
Write-Host "2. Taggeando imagem para ECR..." -ForegroundColor Yellow
docker tag clinicapsi:$tag $imageUri
Write-Host "OK - Tag aplicada: $imageUri" -ForegroundColor Green

# 3. Login no ECR
Write-Host ""
Write-Host "3. Fazendo login no ECR..." -ForegroundColor Yellow
aws ecr get-login-password --region $region | docker login --username AWS --password-stdin 507363615495.dkr.ecr.$region.amazonaws.com

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO - Falha no login do ECR" -ForegroundColor Red
    exit 1
}
Write-Host "OK - Login no ECR realizado" -ForegroundColor Green

# 4. Push para ECR
Write-Host ""
Write-Host "4. Enviando imagem para ECR..." -ForegroundColor Yellow
docker push $imageUri

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO - Falha ao enviar imagem" -ForegroundColor Red
    exit 1
}
Write-Host "OK - Imagem enviada para ECR" -ForegroundColor Green

# 5. Criar nova task definition
Write-Host ""
Write-Host "5. Criando nova task definition..." -ForegroundColor Yellow

# Ler template e substituir imagem
$template = Get-Content .\task-definition-postgres.json -Raw -Encoding UTF8
$pattern = '"image"\s*:\s*".*?"'
$replacement = '"image": "' + $imageUri + '"'
$newTaskDef = [regex]::Replace($template, $pattern, $replacement)

# Salvar SEM BOM (UTF8NoBOM)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText("$PWD\task-definition-new.json", $newTaskDef, $utf8NoBom)

Write-Host "OK - Task definition criada: task-definition-new.json" -ForegroundColor Green

# 6. Registrar task definition
Write-Host ""
Write-Host "6. Registrando task definition no ECS..." -ForegroundColor Yellow
$regRaw = aws ecs register-task-definition --cli-input-json file://task-definition-new.json

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO - Falha ao registrar task definition" -ForegroundColor Red
    Write-Host $regRaw
    exit 1
}

$reg = $regRaw | ConvertFrom-Json
$arn = $reg.taskDefinition.taskDefinitionArn
Write-Host "OK - Task definition registrada: $arn" -ForegroundColor Green

# 7. Atualizar servico ECS
Write-Host ""
Write-Host "7. Atualizando servico ECS..." -ForegroundColor Yellow
$updRaw = aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --task-definition $arn --force-new-deployment --region $region

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO - Falha ao atualizar servico" -ForegroundColor Red
    Write-Host $updRaw
    exit 1
}

Write-Host "OK - Servico ECS atualizado" -ForegroundColor Green

# 8. Aguardar deployment
Write-Host ""
Write-Host "8. Aguardando deployment..." -ForegroundColor Yellow
Write-Host "   O deployment pode levar alguns minutos. Monitorando..."

$maxAttempts = 30
$attempt = 0
$deploymentComplete = $false

while ($attempt -lt $maxAttempts -and -not $deploymentComplete) {
    Start-Sleep -Seconds 10
    $attempt++
    
    $serviceRaw = aws ecs describe-services --cluster clinicapsi-cluster --services clinicapsi-service --region $region
    $service = $serviceRaw | ConvertFrom-Json
    
    $deployment = $service.services[0].deployments | Where-Object { $_.status -eq "PRIMARY" }
    
    if ($deployment.rolloutState -eq "COMPLETED") {
        $deploymentComplete = $true
        Write-Host "OK - Deployment concluido!" -ForegroundColor Green
    } else {
        Write-Host "   Aguardando... ($attempt/$maxAttempts) - Estado: $($deployment.rolloutState)" -ForegroundColor Gray
    }
}

if (-not $deploymentComplete) {
    Write-Host "AVISO - Timeout aguardando deployment. Verifique o console AWS." -ForegroundColor Yellow
}

# 9. Verificar health
Write-Host ""
Write-Host "9. Verificando health dos targets..." -ForegroundColor Yellow
$tgArn = "arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/f84f061a24c7ec0f"
$healthRaw = aws elbv2 describe-target-health --target-group-arn $tgArn --region $region
$health = $healthRaw | ConvertFrom-Json

$healthyCount = ($health.TargetHealthDescriptions | Where-Object { $_.TargetHealth.State -eq "healthy" }).Count
$totalCount = $health.TargetHealthDescriptions.Count

Write-Host "   Targets healthy: $healthyCount/$totalCount" -ForegroundColor Cyan

if ($healthyCount -eq $totalCount) {
    Write-Host "OK - Todos os targets estao healthy!" -ForegroundColor Green
} else {
    Write-Host "AVISO - Aguarde os targets ficarem healthy (pode levar alguns minutos)" -ForegroundColor Yellow
}

# Resumo final
Write-Host ""
Write-Host "=== Deploy Concluido ===" -ForegroundColor Green
Write-Host ""
Write-Host "Resumo:" -ForegroundColor Cyan
Write-Host "  Imagem: $imageUri"
Write-Host "  Task Definition: $arn"
Write-Host "  Cluster: clinicapsi-cluster"
Write-Host "  Service: clinicapsi-service"
Write-Host ""
Write-Host "Proximos passos:" -ForegroundColor Yellow
Write-Host "  1. Verifique os logs no CloudWatch: /ecs/clinicapsi"
Write-Host "  2. Teste o site via ALB"
Write-Host "  3. Configure WhatsApp em: https://seu-dominio.com/admin/whatsapp"
Write-Host ""
Write-Host "Deploy finalizado com sucesso!" -ForegroundColor Green
