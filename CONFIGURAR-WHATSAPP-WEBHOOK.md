# 📱 Como Configurar WhatsApp Bot - Meta/Facebook

## 🚨 Erro: "Não foi possível validar a URL de callback"

Esse erro acontece porque:
1. A Meta não consegue acessar seu webhook
2. O token de verificação está incorreto
3. O endpoint não está respondendo corretamente

---

## ✅ SOLUÇÃO: Configuração Passo a Passo

### 1️⃣ URLs do Webhook

**URL principal (ALB)**:
```
http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/api/whatsapp/webhook
```

**Token de Verificação**:
```
clinicapsi_webhook_token_2025
```

---

### 2️⃣ Configurar no Meta Developer Dashboard

1. **Acesse**: https://developers.facebook.com/apps
2. **Selecione seu App** (ou crie um novo)
3. **Adicione o produto**: WhatsApp > Configuration
4. **Configure o Webhook**:

#### Campos para preencher:

**Callback URL**:
```
http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/api/whatsapp/webhook
```

**Verify Token**:
```
clinicapsi_webhook_token_2025
```

**Campos para subscrever** (marque todos):
- ✅ messages
- ✅ message_status
- ✅ message_echoes (opcional)

5. **Clique em "Verificar e Salvar"**

---

### 3️⃣ Testar o Webhook Manualmente

#### Teste 1: Verificação GET (o que a Meta faz)

Abra no navegador:
```
http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/api/whatsapp/webhook?hub.mode=subscribe&hub.challenge=1234567890&hub.verify_token=clinicapsi_webhook_token_2025
```

**Resposta esperada**: `1234567890` (o valor do challenge)

Se não funcionar, o problema está no endpoint!

---

#### Teste 2: Verificar se endpoint está acessível

```powershell
# Via PowerShell
Invoke-WebRequest -Uri "http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/health" -Method GET

# Testar webhook
$uri = "http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/api/whatsapp/webhook?hub.mode=subscribe&hub.challenge=teste123&hub.verify_token=clinicapsi_webhook_token_2025"
Invoke-WebRequest -Uri $uri -Method GET
```

**Resposta esperada**: `teste123`

---

## 🔧 Problemas Comuns

### ❌ Problema 1: "Invalid Verify Token"
**Causa**: Token no appsettings.json diferente do informado na Meta

**Solução**:
1. Verifique que o token no appsettings.json é: `clinicapsi_webhook_token_2025`
2. Use EXATAMENTE o mesmo token na Meta
3. Faça deploy novamente

---

### ❌ Problema 2: "URL Unreachable"
**Causa**: Meta não consegue acessar a URL

**Possíveis causas**:
1. **ALB não está público** (verificar Security Group)
2. **Target Group não está healthy**
3. **Task do ECS não está rodando**

**Verificar**:
```powershell
# Status do ECS
aws ecs describe-services --cluster clinicapsi-cluster --services clinicapsi-service --region us-east-1 --query 'services[0].[serviceName,runningCount,desiredCount]'

# Health dos targets
aws elbv2 describe-target-health --target-group-arn arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/4dc35e5a7d1b0a17 --region us-east-1 --query 'TargetHealthDescriptions[*].[Target.Id,TargetHealth.State]' --output table
```

---

### ❌ Problema 3: Endpoint retorna 404

**Causa**: Rota não está configurada corretamente

**Verificar no código** (`Program.cs`):
- Linha ~167: `app.MapGet("/api/whatsapp/webhook", ...)`
- Linha ~183: `app.MapPost("/api/whatsapp/webhook", ...)`

Ambos devem estar presentes!

---

## 🚀 Deploy da Configuração Atualizada

Como adicionei a configuração WhatsApp no appsettings.json, precisamos fazer deploy:

```powershell
# 1. Build da imagem
cd C:\Users\Admin\sistema-p-clinica-clean
docker build -t clinicapsi:v3.6.3-whatsapp -f src/ClinicaPsi.Web/Dockerfile .

# 2. Tag para ECR
docker tag clinicapsi:v3.6.3-whatsapp 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:v3.6.3-whatsapp

# 3. Login no ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 507363615495.dkr.ecr.us-east-1.amazonaws.com

# 4. Push
docker push 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:v3.6.3-whatsapp

# 5. Registrar task definition
aws ecs register-task-definition --cli-input-json file://task-definition.json --region us-east-1

# 6. Atualizar serviço
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --force-new-deployment --region us-east-1
```

---

## 📋 Checklist de Configuração

### Antes de configurar na Meta:

- [ ] appsettings.json tem seção WhatsApp com VerifyToken
- [ ] Deploy foi feito com a nova configuração
- [ ] Task está rodando (1/1)
- [ ] Target Group está healthy (1/1)
- [ ] Endpoint `/health` responde 200 OK
- [ ] Endpoint `/api/whatsapp/webhook?hub.mode=...` retorna o challenge

### Na Meta Developer:

- [ ] App criado no Meta Developers
- [ ] Produto WhatsApp adicionado
- [ ] Webhook configurado com URL correta
- [ ] Token de verificação correto (igual ao appsettings.json)
- [ ] Campos messages e message_status subscritos
- [ ] Webhook verificado com sucesso ✅

### Após configuração:

- [ ] Teste enviando mensagem para o número de teste
- [ ] Verifique logs do CloudWatch para ver se recebeu
- [ ] Configure AccessToken e PhoneNumberId no appsettings.json
- [ ] Faça novo deploy com as configurações completas

---

## 🔑 Obter Access Token e Phone Number ID

1. **Access Token**:
   - Vá para: Meta Developer Dashboard > WhatsApp > API Setup
   - Copie o "Temporary Access Token"
   - **IMPORTANTE**: Gere um token permanente depois!

2. **Phone Number ID**:
   - Mesmo lugar: API Setup
   - Procure por "Phone Number ID"
   - Copie o ID (não é o número de telefone!)

3. **Atualizar appsettings.json**:
```json
"WhatsApp": {
  "VerifyToken": "clinicapsi_webhook_token_2025",
  "AccessToken": "SEU_TOKEN_AQUI",
  "PhoneNumberId": "SEU_PHONE_ID_AQUI",
  "AppSecret": "SEU_APP_SECRET_AQUI"
}
```

4. **Fazer deploy novamente** com essas configurações

---

## 🧪 Testar Bot Funcionando

Depois de tudo configurado, teste:

1. **Envie mensagem** para o número de teste do WhatsApp
2. **Verifique logs**:
```powershell
# Ver logs do ECS
aws logs tail /ecs/clinicapsi-task --follow --region us-east-1
```

3. **Comandos de teste**:
   - "oi" → Deve responder com menu
   - "agendar" → Deve iniciar agendamento
   - "ajuda" → Deve mostrar comandos

---

## 🐛 Debug de Problemas

### Ver logs em tempo real:
```powershell
aws logs tail /ecs/clinicapsi-task --follow --region us-east-1 --filter-pattern "WhatsApp"
```

### Testar endpoint manualmente:
```powershell
# GET (verificação)
$uri = "http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/api/whatsapp/webhook"
$params = @{
    "hub.mode" = "subscribe"
    "hub.challenge" = "teste123"
    "hub.verify_token" = "clinicapsi_webhook_token_2025"
}
$query = ($params.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
Invoke-WebRequest -Uri "$uri?$query" -Method GET

# POST (mensagem simulada)
$body = @{
    entry = @(
        @{
            changes = @(
                @{
                    value = @{
                        messages = @(
                            @{
                                from = "5511999999999"
                                text = @{ body = "oi" }
                            }
                        )
                    }
                }
            )
        }
    )
} | ConvertTo-Json -Depth 10

Invoke-WebRequest -Uri $uri -Method POST -Body $body -ContentType "application/json"
```

---

## 📞 Precisa de Ajuda?

Se ainda não funcionar:
1. Me envie o erro exato que aparece na Meta
2. Me envie o resultado do teste do endpoint
3. Verifique os logs do CloudWatch

---

**Criado em**: 01/11/2025 21:45
**Versão**: v3.6.3-whatsapp
**Status**: Aguardando deploy
