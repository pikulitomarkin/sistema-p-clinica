# Deploy para AWS - Script Automatizado

## Variáveis
$AWS_REGION = "us-east-1"
$APP_NAME = "clinicapsi"
$AWS_ACCOUNT_ID = "507363615495"
$ECR_REPO = "$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$APP_NAME"

Write-Host "Iniciando deploy da ClinicaPsi para AWS..." -ForegroundColor Green
Write-Host ""

## 1. Criar repositório ECR (se não existir)
Write-Host "Criando repositorio ECR..." -ForegroundColor Yellow
aws ecr describe-repositories --repository-names $APP_NAME --region $AWS_REGION 2>$null
if ($LASTEXITCODE -ne 0) {
    aws ecr create-repository --repository-name $APP_NAME --region $AWS_REGION
    Write-Host "Repositorio ECR criado" -ForegroundColor Green
} else {
    Write-Host "Repositorio ECR ja existe" -ForegroundColor Green
}
Write-Host ""

## 2. Login no ECR
Write-Host "Fazendo login no ECR..." -ForegroundColor Yellow
aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $ECR_REPO
Write-Host "Login realizado" -ForegroundColor Green
Write-Host ""

## 3. Tag da imagem
Write-Host "Taggeando imagem..." -ForegroundColor Yellow
docker tag clinicapsi:latest "$ECR_REPO:latest"
docker tag clinicapsi:latest "$ECR_REPO:v1.0.0"
Write-Host "Imagem taggeada" -ForegroundColor Green
Write-Host ""

## 4. Push para ECR
Write-Host "Enviando imagem para ECR..." -ForegroundColor Yellow
docker push "$ECR_REPO:latest"
docker push "$ECR_REPO:v1.0.0"
Write-Host "Imagem enviada" -ForegroundColor Green
Write-Host ""

Write-Host "Deploy concluido com sucesso!" -ForegroundColor Green
Write-Host ""
Write-Host "Proximos passos:" -ForegroundColor Cyan
Write-Host "1. Acesse o AWS Console" -ForegroundColor White
Write-Host "2. Va para ECS ou App Runner" -ForegroundColor White
Write-Host "3. Crie um servico usando a imagem: $ECR_REPO:latest" -ForegroundColor White
Write-Host "4. Configure as variaveis de ambiente necessarias" -ForegroundColor White
Write-Host ""
Write-Host "URI da imagem: $ECR_REPO:latest" -ForegroundColor Yellow
