Invoke-RestMethod -Uri "https://whatsapp-bot-production-0624.up.railway.app/qrcode?sessionName=default"# 🚀 Configuração Railway - WhatsApp Web Integration

## Status Atual
✅ **Fase 1** - Estrutura de Banco de Dados - COMPLETO  
✅ **Fase 2** - Container Venom-Bot Node.js - COMPLETO  
✅ **Fase 3** - Página Admin WhatsApp - COMPLETO  
⏳ **Fase 4** - Integração WhatsAppBotService - PENDENTE  
⏳ **Fase 5** - Testes e Deploy Final - PENDENTE  

## 📋 Checklist de Deploy

### 1️⃣ Verificar Deploy do Container Venom-Bot

O serviço Venom-Bot já deve estar sendo deployado no Railway. Acesse:

```
https://railway.app/
→ Seu Projeto
→ Procure pelo serviço "whatsapp-bot"
```

**O que verificar:**
- ✅ Status: "Active" (verde)
- ✅ Build: Concluído com sucesso
- ✅ Logs: Sem erros críticos
- ✅ URL: Anote a URL pública do serviço (ex: `https://whatsapp-bot-production-xxxx.up.railway.app`)

### 2️⃣ Configurar Variáveis de Ambiente

#### Serviço Venom-Bot (Node.js)

No Railway Dashboard → whatsapp-bot → Variables:

```bash
NODE_ENV=production
DATABASE_URL=postgresql://user:pass@host:port/dbname  # Copie do serviço PostgreSQL
PORT=3000  # Railway define automaticamente
```

#### Serviço ASP.NET (ClinicaPsi.Web)

No Railway Dashboard → ASP.NET service → Variables:

```bash
VENOM_BOT_URL=https://whatsapp-bot-production-xxxx.up.railway.app
```

⚠️ **IMPORTANTE**: Substitua `xxxx` pela URL real do seu serviço Venom-Bot!

### 3️⃣ Aplicar Migrations no Railway

As migrations foram criadas mas não aplicadas ainda:

**Opção A - Via Railway Dashboard:**
1. Vá em PostgreSQL → Query
2. Execute manualmente (se disponível):

```sql
-- Adicionar coluna UserId
ALTER TABLE "Psicologos" ADD COLUMN IF NOT EXISTS "UserId" TEXT NULL;

-- Criar tabela WhatsAppSessions
CREATE TABLE IF NOT EXISTS "WhatsAppSessions" (
    "Id" SERIAL PRIMARY KEY,
    "SessionName" TEXT NOT NULL UNIQUE,
    "Status" TEXT NOT NULL,
    "QRCode" TEXT NULL,
    "QRCodeExpiry" TIMESTAMP NULL,
    "AuthToken" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "LastConnection" TIMESTAMP NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

**Opção B - Via Aplicação (Recomendado):**
O DbInitializer já aplica migrations automaticamente no startup. Apenas faça redeploy:

```bash
# Via Railway Dashboard
Settings → Triggers → Deploy
```

### 4️⃣ Testar Conexão WhatsApp

1. Acesse: `https://www.psiianasantos.com.br/admin/whatsapp`
2. Clique em **"Gerar QR Code"**
3. Aguarde o QR Code aparecer (pode levar 10-30 segundos)
4. Abra WhatsApp no celular → Menu → WhatsApp Web
5. Escaneie o QR Code
6. Aguarde mensagem de sucesso: **"Conectado"**

### 5️⃣ Comandos Railway CLI

Para monitorar e debugar via terminal:

```bash
# Login
npx @railway/cli login

# Ligar projeto
npx @railway/cli link

# Ver logs do Venom-Bot em tempo real
npx @railway/cli logs --service whatsapp-bot

# Ver logs do ASP.NET
npx @railway/cli logs --service web

# Ver variáveis de ambiente
npx @railway/cli variables

# Status dos serviços
npx @railway/cli status
```

## 🔍 Verificação de Erros Comuns

### ❌ Erro: "Cannot connect to Venom-Bot service"

**Causa:** VENOM_BOT_URL não configurado ou serviço Venom-Bot offline

**Solução:**
1. Verifique se o serviço Venom-Bot está "Active" no Railway
2. Confirme VENOM_BOT_URL no serviço ASP.NET
3. Teste manualmente: `curl https://your-venom-bot-url.railway.app/health`

### ❌ Erro: "QR Code not generating"

**Causa:** Chromium não inicializado ou problema com tokens

**Solução:**
1. Ver logs do Venom-Bot: `npx @railway/cli logs --service whatsapp-bot`
2. Procure por erros do Puppeteer/Chromium
3. Verifique se /app/tokens tem permissões corretas
4. Redeploy do serviço Venom-Bot

### ❌ Erro: "Database connection failed"

**Causa:** DATABASE_URL incorreto no Venom-Bot

**Solução:**
1. Copie DATABASE_URL do serviço PostgreSQL
2. Cole exatamente no serviço Venom-Bot
3. Redeploy

### ❌ Erro: "Column UserId does not exist"

**Causa:** Migration não aplicada

**Solução:**
1. Acesse: `https://www.psiianasantos.com.br/fixuserid`
2. Execute o script
3. OU redeploy do serviço ASP.NET (aplica migrations automaticamente)

## 📊 Endpoints da API Venom-Bot

Após deploy, estes endpoints estarão disponíveis:

```bash
# Health check
GET https://your-venom-bot.railway.app/health
Response: {"status":"ok","sessions":1}

# Status da conexão
GET https://your-venom-bot.railway.app/status?session=default
Response: {"connected":true,"phoneNumber":"5511999999999","status":"Conectado"}

# Gerar QR Code
GET https://your-venom-bot.railway.app/qrcode?session=default
Response: {"success":true,"qrcode":"base64...","session":"default"}

# Enviar mensagem (requer POST)
POST https://your-venom-bot.railway.app/send
Body: {"session":"default","number":"5511999999999","message":"Olá!"}
Response: {"success":true,"messageId":"..."}

# Desconectar
POST https://your-venom-bot.railway.app/disconnect?session=default
Response: {"success":true}
```

## 🎯 Próximos Passos (Fase 4)

Após confirmar que tudo está funcionando:

1. ✅ WhatsApp conectado e QR Code funcionando
2. ✅ Mensagem de teste enviada com sucesso
3. ⏭️ **Atualizar WhatsAppBotService** para usar WhatsAppWebService
4. ⏭️ Testar fluxo completo de notificações automáticas
5. ⏭️ Deploy final e documentação

## 📞 Suporte

Se encontrar problemas:

1. Verifique logs: `npx @railway/cli logs --service <nome-servico>`
2. Teste endpoints manualmente com curl/Postman
3. Verifique variáveis de ambiente no Railway Dashboard
4. Confira se DATABASE_URL está idêntico em ambos os serviços

---

**Última atualização:** Fase 3 completa - Commit 486115e  
**Próxima Fase:** Integração WhatsAppBotService (Fase 4)
