# 🌐 Configuração de Domínio Hostgator para ClinicaPsi

## 📋 Informações Necessárias

### Load Balancer AWS (ALB)
```
DNS Name: clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
Hosted Zone ID: Z35SXDOTRQ7X7K
Region: us-east-1
```

---

## 🎯 Opção 1: DNS no Hostgator (Recomendado - Mais Simples)

Esta é a opção mais simples se você quiser manter a gestão do DNS no Hostgator.

### Passo 1: Acessar cPanel do Hostgator

1. Faça login em: https://hostgator.com.br/cpanel
2. Encontre a seção **"Domínios"** ou **"Zone Editor"**
3. Selecione seu domínio

### Passo 2: Criar Registro CNAME (www)

**Para www.seudominio.com.br:**

```
Type: CNAME
Name: www
CNAME: clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
TTL: 14400 (4 horas) ou 3600 (1 hora)
```

### Passo 3: Criar Registro A (root/apex)

**Para seudominio.com.br (sem www):**

⚠️ **IMPORTANTE**: O Hostgator NÃO suporta ALIAS para domínio raiz, então você tem 2 opções:

#### Opção 3A: Usar IP do ALB (Não Recomendado)
```
Type: A
Name: @ (ou deixe em branco)
Address: [Obter IP do ALB - veja abaixo]
TTL: 14400
```

Para obter o IP atual do ALB (pode mudar!):
```powershell
nslookup clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
```

⚠️ **Problema**: IPs do ALB podem mudar! Não é ideal.

#### Opção 3B: Redirecionar root para www (Recomendado)
```
1. Configure apenas o CNAME para www (passo 2)
2. No cPanel, use "Redirects" para redirecionar:
   - De: seudominio.com.br
   - Para: https://www.seudominio.com.br
   - Tipo: Permanent (301)
```

### Passo 4: Tempo de Propagação
- **Tempo estimado**: 4-48 horas
- **Geralmente**: 1-4 horas
- **Verificar**: Use https://dnschecker.org

---

## 🚀 Opção 2: DNS na AWS Route 53 (Recomendado - Melhor Performance)

Esta opção oferece melhor performance e recursos, mas requer migrar o DNS para AWS.

### Vantagens do Route 53:
- ✅ Suporte a ALIAS records (funciona perfeitamente com ALB)
- ✅ Propagação mais rápida
- ✅ Health checks integrados
- ✅ Failover automático
- ✅ Latency-based routing
- ✅ Custo: ~$0.50/mês por hosted zone + $0.40 por milhão de queries

### Passo 1: Criar Hosted Zone na AWS

```powershell
# Criar hosted zone
aws route53 create-hosted-zone --name seudominio.com.br --caller-reference $(Get-Date -Format "yyyyMMddHHmmss")
```

Anote os **nameservers** que aparecerem (exemplo):
```
ns-1234.awsdns-12.org
ns-5678.awsdns-34.com
ns-9012.awsdns-56.net
ns-3456.awsdns-78.co.uk
```

### Passo 2: Atualizar Nameservers no Hostgator

1. Acesse o painel do Hostgator
2. Vá em **"Domínios"** > **"Gerenciar Domínios"**
3. Clique no seu domínio
4. Procure **"Nameservers"** ou **"Servidores de Nome"**
5. Selecione **"Usar nameservers personalizados"**
6. Adicione os 4 nameservers da AWS

⏱️ **Aguarde 24-48h** para propagação dos nameservers.

### Passo 3: Criar Registros DNS na Route 53

Depois que os nameservers propagarem, crie este arquivo:

**route53-records.json:**
```json
{
  "Comment": "Create alias records for ClinicaPsi",
  "Changes": [
    {
      "Action": "CREATE",
      "ResourceRecordSet": {
        "Name": "seudominio.com.br",
        "Type": "A",
        "AliasTarget": {
          "HostedZoneId": "Z35SXDOTRQ7X7K",
          "DNSName": "clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com",
          "EvaluateTargetHealth": true
        }
      }
    },
    {
      "Action": "CREATE",
      "ResourceRecordSet": {
        "Name": "www.seudominio.com.br",
        "Type": "A",
        "AliasTarget": {
          "HostedZoneId": "Z35SXDOTRQ7X7K",
          "DNSName": "clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com",
          "EvaluateTargetHealth": true
        }
      }
    }
  ]
}
```

Execute:
```powershell
# Obter o Hosted Zone ID (substitua pelo seu domínio)
$HOSTED_ZONE_ID = (aws route53 list-hosted-zones-by-name --dns-name seudominio.com.br --query "HostedZones[0].Id" --output text)

# Aplicar os registros
aws route53 change-resource-record-sets --hosted-zone-id $HOSTED_ZONE_ID --change-batch file://route53-records.json
```

---

## 🔒 Configurar HTTPS/SSL

Após o DNS configurado, você precisa de certificado SSL.

### Opção 1: AWS Certificate Manager (GRÁTIS)

```powershell
# Solicitar certificado
aws acm request-certificate --domain-name seudominio.com.br --subject-alternative-names www.seudominio.com.br --validation-method DNS

# Anotar o ARN do certificado que aparecer
# Exemplo: arn:aws:acm:us-east-1:507363615495:certificate/xxxxx
```

Você receberá registros CNAME para validação. Adicione-os:
- **Hostgator**: No Zone Editor
- **Route 53**: Automaticamente com AWS CLI

Após validação (5-30 min), adicione o certificado ao ALB:

```powershell
# Obter ARN do certificado
$CERT_ARN = (aws acm list-certificates --query "CertificateSummaryList[?DomainName=='seudominio.com.br'].CertificateArn" --output text)

# Obter ARN do ALB
$ALB_ARN = (aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].LoadBalancerArn" --output text)

# Criar listener HTTPS
aws elbv2 create-listener --load-balancer-arn $ALB_ARN --protocol HTTPS --port 443 --certificates CertificateArn=$CERT_ARN --default-actions Type=forward,TargetGroupArn=arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/xxxxx

# Criar regra de redirect HTTP -> HTTPS no listener 80
# (Obter ARN do listener HTTP primeiro)
$HTTP_LISTENER_ARN = (aws elbv2 describe-listeners --load-balancer-arn $ALB_ARN --query "Listeners[?Port==80].ListenerArn" --output text)

aws elbv2 modify-listener --listener-arn $HTTP_LISTENER_ARN --default-actions Type=redirect,RedirectConfig="{Protocol=HTTPS,Port=443,StatusCode=HTTP_301}"
```

---

## 📧 Configurar Email (Opcional)

Se você quiser manter emails no Hostgator, adicione estes registros MX:

### No Hostgator (DNS no Hostgator):
Já estão configurados automaticamente.

### Na Route 53 (DNS na AWS):
```json
{
  "Action": "CREATE",
  "ResourceRecordSet": {
    "Name": "seudominio.com.br",
    "Type": "MX",
    "TTL": 14400,
    "ResourceRecords": [
      {"Value": "10 seudominio.com.br"}
    ]
  }
}
```

---

## ✅ Checklist de Configuração

### Opção 1: DNS no Hostgator
- [ ] Criar CNAME para www → ALB
- [ ] Criar redirect de @ → www (ou criar A record com IP do ALB)
- [ ] Aguardar propagação (4-48h)
- [ ] Testar: http://www.seudominio.com.br
- [ ] Solicitar certificado SSL (ACM)
- [ ] Adicionar certificado ao ALB
- [ ] Configurar redirect HTTP → HTTPS
- [ ] Testar: https://www.seudominio.com.br

### Opção 2: DNS no Route 53
- [ ] Criar Hosted Zone na Route 53
- [ ] Anotar nameservers da AWS
- [ ] Atualizar nameservers no Hostgator
- [ ] Aguardar propagação (24-48h)
- [ ] Criar ALIAS records (@ e www)
- [ ] Solicitar certificado SSL (ACM)
- [ ] Validar certificado via DNS
- [ ] Adicionar certificado ao ALB
- [ ] Configurar redirect HTTP → HTTPS
- [ ] Testar: https://seudominio.com.br e https://www.seudominio.com.br

---

## 🔍 Verificar Configuração

### Verificar DNS
```powershell
# Windows
nslookup www.seudominio.com.br
nslookup seudominio.com.br

# Verificar propagação global
# Acesse: https://dnschecker.org
```

### Verificar SSL
```powershell
# Testar conexão SSL
curl -I https://www.seudominio.com.br
```

### Verificar ALB Health
```powershell
# Ver status do target group
aws elbv2 describe-target-health --target-group-arn arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/xxxxx
```

---

## 🚨 Troubleshooting

### "Site não carrega"
1. Verificar DNS propagou: `nslookup www.seudominio.com.br`
2. Verificar ALB está healthy: Console AWS > EC2 > Load Balancers
3. Verificar ECS tasks rodando: `aws ecs list-tasks --cluster clinicapsi-cluster`

### "Certificado SSL inválido"
1. Verificar certificado foi emitido (ACM console)
2. Verificar listener HTTPS configurado no ALB
3. Limpar cache do browser (Ctrl+F5)

### "DNS não propaga"
1. Aguardar mais tempo (até 48h)
2. Limpar cache DNS: `ipconfig /flushdns`
3. Testar em: https://dnschecker.org

### "Email parou de funcionar"
1. Verificar registros MX estão configurados
2. Se migrou para Route 53, adicionar MX records manualmente

---

## 💰 Custos

### Opção 1: DNS no Hostgator
- **Hostgator**: R$ 0 (incluso no plano)
- **AWS ACM**: Grátis
- **Total**: **R$ 0/mês**

### Opção 2: DNS no Route 53
- **Route 53 Hosted Zone**: $0.50/mês
- **Route 53 Queries**: $0.40/milhão (muito baixo)
- **AWS ACM**: Grátis
- **Total**: **~R$ 3-5/mês**

---

## 📞 Suporte

### Hostgator Support
- Site: https://suporte.hostgator.com.br
- Chat ao vivo disponível

### AWS Support
- Console: https://console.aws.amazon.com/support
- Documentação: https://docs.aws.amazon.com

---

## 🎯 Recomendação Final

**Para começar rapidamente**: Use **Opção 1** (DNS no Hostgator)
- ✅ Mais simples
- ✅ Grátis
- ✅ Funciona bem

**Para produção profissional**: Migre para **Opção 2** (Route 53) depois
- ✅ Melhor performance
- ✅ Mais confiável
- ✅ Mais recursos
- ✅ Custo baixo (~R$ 3-5/mês)

---

**Última atualização:** 22/10/2025
**Versão:** 1.0
