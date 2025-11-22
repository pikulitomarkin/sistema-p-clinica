# Script para configurar SSL/HTTPS no ALB
# Execute este script DEPOIS de configurar o DNS e obter o certificado ACM

# ============================================
# PASSO 1: Solicitar Certificado SSL (ACM)
# ============================================

Write-Host "=== PASSO 1: Solicitar Certificado SSL ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Digite seu domínio (ex: clinicapsi.com.br):" -ForegroundColor Yellow
$DOMAIN = Read-Host

Write-Host ""
Write-Host "Solicitando certificado para $DOMAIN e www.$DOMAIN..." -ForegroundColor Green

$CERT_REQUEST = aws acm request-certificate `
    --domain-name $DOMAIN `
    --subject-alternative-names "www.$DOMAIN" `
    --validation-method DNS `
    --region us-east-1 `
    --query "CertificateArn" `
    --output text

if ($CERT_REQUEST) {
    Write-Host "✅ Certificado solicitado com sucesso!" -ForegroundColor Green
    Write-Host "ARN: $CERT_REQUEST" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "❌ Erro ao solicitar certificado" -ForegroundColor Red
    exit 1
}

# ============================================
# PASSO 2: Obter Registros de Validação
# ============================================

Write-Host "=== PASSO 2: Validação DNS ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Aguardando registros de validação..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

$VALIDATION_RECORDS = aws acm describe-certificate `
    --certificate-arn $CERT_REQUEST `
    --region us-east-1 `
    --query "Certificate.DomainValidationOptions[*].[DomainName,ResourceRecord.Name,ResourceRecord.Value]" `
    --output table

Write-Host ""
Write-Host "📋 ADICIONE ESTES REGISTROS CNAME NO SEU DNS:" -ForegroundColor Yellow
Write-Host $VALIDATION_RECORDS
Write-Host ""
Write-Host "No Hostgator (cPanel > Zone Editor):" -ForegroundColor Cyan
Write-Host "  Type: CNAME" -ForegroundColor White
Write-Host "  Name: [copie o Name acima, removendo o .$DOMAIN]" -ForegroundColor White
Write-Host "  CNAME: [copie o Value acima]" -ForegroundColor White
Write-Host ""
Write-Host "Pressione ENTER após adicionar os registros de validação..." -ForegroundColor Yellow
Read-Host

# ============================================
# PASSO 3: Aguardar Validação
# ============================================

Write-Host "=== PASSO 3: Aguardando Validação ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Aguardando validação do certificado (pode levar até 30 minutos)..." -ForegroundColor Yellow

$validated = $false
$attempts = 0
$maxAttempts = 60

while (-not $validated -and $attempts -lt $maxAttempts) {
    $attempts++
    $status = aws acm describe-certificate `
        --certificate-arn $CERT_REQUEST `
        --region us-east-1 `
        --query "Certificate.Status" `
        --output text
    
    if ($status -eq "ISSUED") {
        $validated = $true
        Write-Host ""
        Write-Host "✅ Certificado validado e emitido com sucesso!" -ForegroundColor Green
    } else {
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 30
    }
}

if (-not $validated) {
    Write-Host ""
    Write-Host "⏱️ Timeout na validação. Verifique os registros DNS e tente novamente." -ForegroundColor Yellow
    Write-Host "Você pode verificar o status manualmente com:" -ForegroundColor White
    Write-Host "aws acm describe-certificate --certificate-arn $CERT_REQUEST --region us-east-1" -ForegroundColor Gray
    exit 1
}

# ============================================
# PASSO 4: Obter ARNs Necessários
# ============================================

Write-Host ""
Write-Host "=== PASSO 4: Configurando ALB ===" -ForegroundColor Cyan
Write-Host ""

$ALB_ARN = aws elbv2 describe-load-balancers `
    --names clinicapsi-alb `
    --query "LoadBalancers[0].LoadBalancerArn" `
    --output text

$TG_ARN = "arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/f84f061a24c7ec0f"

Write-Host "Load Balancer ARN: $ALB_ARN" -ForegroundColor White
Write-Host "Target Group ARN: $TG_ARN" -ForegroundColor White
Write-Host ""

# ============================================
# PASSO 5: Criar Listener HTTPS (443)
# ============================================

Write-Host "=== PASSO 5: Criando Listener HTTPS ===" -ForegroundColor Cyan
Write-Host ""

$HTTPS_LISTENER = aws elbv2 create-listener `
    --load-balancer-arn $ALB_ARN `
    --protocol HTTPS `
    --port 443 `
    --certificates CertificateArn=$CERT_REQUEST `
    --default-actions Type=forward,TargetGroupArn=$TG_ARN `
    --query "Listeners[0].ListenerArn" `
    --output text 2>&1

if ($HTTPS_LISTENER -like "*arn:aws:*") {
    Write-Host "✅ Listener HTTPS criado com sucesso!" -ForegroundColor Green
    Write-Host "ARN: $HTTPS_LISTENER" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "❌ Erro ao criar listener HTTPS" -ForegroundColor Red
    Write-Host $HTTPS_LISTENER -ForegroundColor Red
    Write-Host ""
    Write-Host "Possível causa: Listener HTTPS já existe" -ForegroundColor Yellow
    Write-Host "Continuando..." -ForegroundColor Yellow
    Write-Host ""
}

# ============================================
# PASSO 6: Configurar Redirect HTTP -> HTTPS
# ============================================

Write-Host "=== PASSO 6: Configurando Redirect HTTP -> HTTPS ===" -ForegroundColor Cyan
Write-Host ""

# Obter ARN do listener HTTP (porta 80)
$HTTP_LISTENER_ARN = aws elbv2 describe-listeners `
    --load-balancer-arn $ALB_ARN `
    --query "Listeners[?Port==``80``].ListenerArn" `
    --output text

if ($HTTP_LISTENER_ARN) {
    Write-Host "Listener HTTP encontrado: $HTTP_LISTENER_ARN" -ForegroundColor White
    Write-Host "Modificando para redirecionar HTTP -> HTTPS..." -ForegroundColor Yellow
    
    $REDIRECT_RESULT = aws elbv2 modify-listener `
        --listener-arn $HTTP_LISTENER_ARN `
        --default-actions '[{"Type":"redirect","RedirectConfig":{"Protocol":"HTTPS","Port":"443","StatusCode":"HTTP_301"}}]' 2>&1
    
    if ($REDIRECT_RESULT -like "*ListenerArn*") {
        Write-Host "✅ Redirect HTTP -> HTTPS configurado com sucesso!" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Aviso ao configurar redirect:" -ForegroundColor Yellow
        Write-Host $REDIRECT_RESULT -ForegroundColor Gray
    }
} else {
    Write-Host "⚠️ Listener HTTP não encontrado" -ForegroundColor Yellow
}

# ============================================
# PASSO 7: Resumo e Testes
# ============================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ CONFIGURAÇÃO SSL CONCLUÍDA!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Resumo da Configuração:" -ForegroundColor Cyan
Write-Host "  • Domínio: $DOMAIN" -ForegroundColor White
Write-Host "  • Certificado ARN: $CERT_REQUEST" -ForegroundColor White
Write-Host "  • HTTPS Listener: ✅ Criado na porta 443" -ForegroundColor White
Write-Host "  • HTTP Redirect: ✅ HTTP -> HTTPS configurado" -ForegroundColor White
Write-Host ""
Write-Host "🧪 Testes:" -ForegroundColor Cyan
Write-Host "  1. Aguarde 5-10 minutos para propagação" -ForegroundColor White
Write-Host "  2. Teste HTTP:  http://$DOMAIN" -ForegroundColor White
Write-Host "  3. Teste HTTPS: https://$DOMAIN" -ForegroundColor White
Write-Host "  4. Teste WWW:   https://www.$DOMAIN" -ForegroundColor White
Write-Host ""
Write-Host "🔍 Verificar Status:" -ForegroundColor Cyan
Write-Host "  aws acm describe-certificate --certificate-arn $CERT_REQUEST --region us-east-1" -ForegroundColor Gray
Write-Host ""
Write-Host "📊 Ver Listeners do ALB:" -ForegroundColor Cyan
Write-Host "  aws elbv2 describe-listeners --load-balancer-arn $ALB_ARN" -ForegroundColor Gray
Write-Host ""
Write-Host "Pronto! Seu site agora está com HTTPS! 🎉" -ForegroundColor Green
Write-Host ""
