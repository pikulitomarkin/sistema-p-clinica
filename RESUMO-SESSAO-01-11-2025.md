# 📋 Resumo - Sessão 01/11/2025

## ✅ O que foi feito hoje:

### 1. Análise de Custos AWS
- **Problema**: Fatura de R$1500/mês (muito acima do esperado)
- **Descobertas**:
  - 2 EC2s extras rodando (t2.micro e t3.micro) → **DELETADOS**
  - ECS com 2 tasks Fargate → **REDUZIDO para 1**
  - Elastic IP não utilizado → Precisa liberar manualmente
  - NAT Gateway com custo alto (~$32-45/mês + tráfego)

### 2. Recursos Desativados
- ✅ EC2 t2.micro (i-0bf005a101527f9ce) - Economia: ~$8-10/mês
- ✅ EC2 t3.micro (i-06cac84f1798c59d3) - Economia: ~$10-12/mês
- ✅ ECS Fargate: 2 tasks → 1 task - Economia: ~$15-20/mês

**Economia Total: ~$33-42 USD/mês = R$165-210/mês**

### 3. Custos Atuais (após redução)
```
- ECS Fargate (1 task):    ~$15-20/mês
- RDS PostgreSQL:          ~$15-25/mês
- ALB:                     ~$16-20/mês
- NAT Gateway:             ~$32-45/mês ⚠️ (maior custo individual)
- EFS + ECR:               ~$2-5/mês
- Elastic IP não usado:    ~$3-4/mês

TOTAL: ~$83-119 USD/mês = R$415-595/mês
```

### 4. Configuração WhatsApp Webhook
- **Problema**: "Não foi possível validar a URL de callback"
- **Causa**: 
  1. Faltava configuração WhatsApp no appsettings.json
  2. App estava forçando HTTPS redirect (impedia Meta de validar via HTTP)

- **Correções aplicadas**:
  - ✅ Adicionado seção WhatsApp no appsettings.json
  - ✅ Comentado `app.UseHttpsRedirection()` para aceitar HTTP no webhook
  - ✅ Deploy v3.6.4-webhook-fix realizado

### 5. Versões Deployadas Hoje
1. **v3.6.2** (21:30) - Correções de frontend (email, Configurações, botão WhatsApp)
2. **v3.6.3-whatsapp** (21:50) - Adicionado config WhatsApp no appsettings.json
3. **v3.6.4-webhook-fix** (22:00) - Removido HTTPS redirect para webhook funcionar

**Versão Atual em Produção**: v3.6.4-webhook-fix (task definition revision 32)

---

## 📝 Para Amanhã:

### 1. Testar Webhook WhatsApp
```
URL: http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/api/whatsapp/webhook
Token: clinicapsi_webhook_token_2025
```

**Como testar**:
1. Abrir no navegador:
```
http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/api/whatsapp/webhook?hub.mode=subscribe&hub.challenge=teste123&hub.verify_token=clinicapsi_webhook_token_2025
```
**Resultado esperado**: Deve mostrar `teste123`

2. Configurar na Meta Developer:
   - Callback URL: (URL acima sem parâmetros)
   - Verify Token: `clinicapsi_webhook_token_2025`
   - Subscrições: messages, message_status

### 2. Completar Configuração WhatsApp
Após webhook validar, adicionar no appsettings.json:
```json
"WhatsApp": {
  "VerifyToken": "clinicapsi_webhook_token_2025",
  "AccessToken": "[PEGAR NA META]",
  "PhoneNumberId": "[PEGAR NA META]",
  "AppSecret": "[PEGAR NA META - OPCIONAL]"
}
```

Depois fazer novo deploy com essas configs.

### 3. Investigar Custo de R$1500
**Principais suspeitos**:
1. **Tráfego do NAT Gateway** (~$0.045/GB) - MAIS PROVÁVEL
   - Verificar no CloudWatch Metrics
   - Se muito alto, considerar remover NAT Gateway
   
2. **Snapshots de RDS**
   - Verificar quantos snapshots existem
   - Deletar antigos se necessário

3. **CloudWatch Logs**
   - Verificar tamanho dos log groups
   - Configurar retenção menor

**Como verificar no Console AWS**:
- Cost Explorer: https://console.aws.amazon.com/cost-management/home#/cost-explorer
- Agrupar por: Service
- Período: Outubro 2025

### 4. Liberar Elastic IP
**Manualmente no Console AWS**:
1. EC2 Console > Network & Security > Elastic IPs
2. Selecionar: 34.229.68.19 (eipalloc-081c57683c7d7e4a8)
3. Actions > Release Elastic IP address
4. Economia: ~$3-4/mês

---

## 📁 Arquivos de Referência Criados

1. **CUSTOS-AWS.md** - Análise completa de custos e opções de redução
2. **COMO-LIBERAR-ELASTIC-IP.md** - Instruções detalhadas para liberar IP e investigar custos
3. **CONFIGURAR-WHATSAPP-WEBHOOK.md** - Guia completo de configuração do webhook

---

## 🔗 Links Importantes

**Site**: http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com

**Endpoints**:
- Admin: /admin
- WhatsApp Config: /admin/whatsapp
- Webhook: /api/whatsapp/webhook
- Health: /health

**AWS**:
- Região: us-east-1
- Cluster: clinicapsi-cluster
- Service: clinicapsi-service
- RDS: clinicapsi-db
- ALB: clinicapsi-alb

---

## 🎯 Status Atual

✅ **Sistema Online** (1/1 task rodando)
✅ **Target Healthy** (1/1 healthy)
✅ **Custos Reduzidos** (~35% economia)
⏳ **Webhook Pronto** (aguardando validação Meta)
⏳ **Custo R$1500** (aguardando investigação detalhada)

---

**Última atualização**: 01/11/2025 22:05
**Próxima sessão**: 02/11/2025

Boa noite! 🌙
