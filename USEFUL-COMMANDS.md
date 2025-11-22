# 🔍 Comandos Úteis - DNS e SSL

## Informações do Load Balancer

### Ver DNS do ALB
```powershell
aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].DNSName" --output text
```
**Resultado:** `clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com`

### Ver ARN do ALB
```powershell
aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].LoadBalancerArn" --output text
```

### Ver Listeners do ALB
```powershell
aws elbv2 describe-listeners --load-balancer-arn $(aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].LoadBalancerArn" --output text) --output table
```

---

## Verificar DNS

### Resolver DNS (Windows)
```powershell
# Resolver www
nslookup www.seudominio.com.br

# Resolver root
nslookup seudominio.com.br

# Resolver ALB
nslookup clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
```

### Limpar Cache DNS
```powershell
ipconfig /flushdns
```

### Ver Cache DNS
```powershell
ipconfig /displaydns | Select-String "seudominio.com.br" -Context 2,5
```

### Testar DNS em Múltiplos Servidores
```powershell
# Google DNS
nslookup www.seudominio.com.br 8.8.8.8

# Cloudflare DNS
nslookup www.seudominio.com.br 1.1.1.1

# OpenDNS
nslookup www.seudominio.com.br 208.67.222.222
```

---

## Certificados SSL (ACM)

### Listar Certificados
```powershell
aws acm list-certificates --region us-east-1 --output table
```

### Ver Detalhes do Certificado
```powershell
# Por ARN
aws acm describe-certificate --certificate-arn arn:aws:acm:us-east-1:XXXXX --region us-east-1

# Apenas status
aws acm describe-certificate --certificate-arn ARN --region us-east-1 --query "Certificate.Status" --output text
```

### Solicitar Novo Certificado
```powershell
aws acm request-certificate `
    --domain-name seudominio.com.br `
    --subject-alternative-names www.seudominio.com.br `
    --validation-method DNS `
    --region us-east-1
```

### Ver Registros de Validação
```powershell
aws acm describe-certificate `
    --certificate-arn ARN_DO_CERTIFICADO `
    --region us-east-1 `
    --query "Certificate.DomainValidationOptions[*].[DomainName,ResourceRecord.Name,ResourceRecord.Value]" `
    --output table
```

---

## Testar Site

### HTTP Request Simples
```powershell
curl -I http://www.seudominio.com.br
```

### HTTPS Request
```powershell
curl -I https://www.seudominio.com.br
```

### Seguir Redirects
```powershell
curl -L -I http://www.seudominio.com.br
```

### Ver Certificado SSL
```powershell
# PowerShell
$url = "https://www.seudominio.com.br"
$req = [Net.HttpWebRequest]::Create($url)
$req.GetResponse() | Out-Null
$req.ServicePoint.Certificate | Format-List *
```

### Testar com Verbose
```powershell
curl -v https://www.seudominio.com.br
```

---

## Target Group e Health Checks

### Ver Status do Target Group
```powershell
aws elbv2 describe-target-health `
    --target-group-arn arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/f84f061a24c7ec0f `
    --output table
```

### Ver Configuração do Target Group
```powershell
aws elbv2 describe-target-groups --names clinicapsi-tg --output table
```

---

## ECS Tasks

### Listar Tasks Rodando
```powershell
aws ecs list-tasks --cluster clinicapsi-cluster --service clinicapsi-service
```

### Ver Detalhes das Tasks
```powershell
$TASK_ARNS = (aws ecs list-tasks --cluster clinicapsi-cluster --service clinicapsi-service --query "taskArns" --output text)
aws ecs describe-tasks --cluster clinicapsi-cluster --tasks $TASK_ARNS --output table
```

### Ver Logs da Aplicação
```powershell
# Últimos 10 minutos
aws logs tail /ecs/clinicapsi --since 10m

# Seguir em tempo real
aws logs tail /ecs/clinicapsi --follow

# Filtrar por palavra
aws logs tail /ecs/clinicapsi --since 1h | Select-String "error"
```

---

## Security Groups

### Ver Security Groups do ALB
```powershell
$ALB_ARN = (aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].LoadBalancerArn" --output text)
aws elbv2 describe-load-balancers --load-balancer-arns $ALB_ARN --query "LoadBalancers[0].SecurityGroups" --output table
```

### Ver Regras do Security Group
```powershell
aws ec2 describe-security-groups --group-ids sg-0265151bb034d763f --output table
```

### Adicionar Regra (HTTPS 443)
```powershell
aws ec2 authorize-security-group-ingress `
    --group-id sg-0265151bb034d763f `
    --protocol tcp `
    --port 443 `
    --cidr 0.0.0.0/0
```

---

## Route 53 (Se usar DNS na AWS)

### Listar Hosted Zones
```powershell
aws route53 list-hosted-zones --output table
```

### Ver Registros de uma Hosted Zone
```powershell
aws route53 list-resource-record-sets --hosted-zone-id /hostedzone/XXXXX --output table
```

### Criar Registro ALIAS
```powershell
# Criar arquivo change-batch.json primeiro, depois:
aws route53 change-resource-record-sets `
    --hosted-zone-id /hostedzone/XXXXX `
    --change-batch file://change-batch.json
```

---

## Monitoramento

### CloudWatch Metrics do ALB
```powershell
aws cloudwatch get-metric-statistics `
    --namespace AWS/ApplicationELB `
    --metric-name RequestCount `
    --dimensions Name=LoadBalancer,Value=app/clinicapsi-alb/XXXXXX `
    --start-time (Get-Date).AddHours(-1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss") `
    --end-time (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss") `
    --period 300 `
    --statistics Sum
```

### Ver Alarmes
```powershell
aws cloudwatch describe-alarms --output table
```

---

## Troubleshooting

### Testar Conectividade HTTP
```powershell
# Testar ALB diretamente
Test-NetConnection -ComputerName clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com -Port 80

# Testar HTTPS
Test-NetConnection -ComputerName clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com -Port 443
```

### Ver IP do ALB
```powershell
# Resolver DNS
[System.Net.Dns]::GetHostAddresses("clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com")
```

### Traceroute
```powershell
tracert clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
```

### Verificar Porta Aberta
```powershell
# PowerShell
Test-NetConnection -ComputerName www.seudominio.com.br -Port 443 -InformationLevel Detailed
```

---

## Backup e Restore

### Exportar Configuração do ALB
```powershell
aws elbv2 describe-load-balancers --names clinicapsi-alb > alb-backup.json
aws elbv2 describe-listeners --load-balancer-arn $(aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].LoadBalancerArn" --output text) > listeners-backup.json
aws elbv2 describe-target-groups --names clinicapsi-tg > target-group-backup.json
```

### Listar Certificados para Backup
```powershell
aws acm list-certificates --region us-east-1 > certificates-backup.json
```

---

## Limpeza (Cuidado!)

### Deletar Listener
```powershell
aws elbv2 delete-listener --listener-arn ARN_DO_LISTENER
```

### Deletar Certificado
```powershell
aws acm delete-certificate --certificate-arn ARN_DO_CERTIFICADO --region us-east-1
```

⚠️ **ATENÇÃO**: Só delete recursos se tiver certeza!

---

## Aliases Úteis (Adicionar ao Profile)

Adicione ao seu `$PROFILE` do PowerShell:

```powershell
# Abrir profile
notepad $PROFILE

# Adicionar estas funções:
function Get-AlbDns {
    aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].DNSName" --output text
}

function Get-AlbHealth {
    aws elbv2 describe-target-health --target-group-arn arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/f84f061a24c7ec0f --output table
}

function Get-EcsTasks {
    aws ecs list-tasks --cluster clinicapsi-cluster --service clinicapsi-service
}

function Watch-Logs {
    aws logs tail /ecs/clinicapsi --follow
}

# Usar depois:
# Get-AlbDns
# Get-AlbHealth
# Watch-Logs
```

---

**Salve este arquivo para referência rápida!** 📌
