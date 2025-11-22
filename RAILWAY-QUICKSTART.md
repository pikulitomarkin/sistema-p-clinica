# ⚡ Quick Start - Deploy no Railway

## 🎯 Deploy em 10 minutos!

### 1️⃣ Criar Conta no Railway (2 min)
```
https://railway.app
```
- Click em **"Login with GitHub"**
- Autorize acesso aos repositórios

### 2️⃣ Criar Projeto (2 min)

**No Railway Dashboard:**
1. Click **"New Project"**
2. **"Deploy from GitHub repo"**
3. Selecione: `sistema-p-clinica`
4. Branch: `main`

Railway detectará automaticamente `.NET 9.0` ✅

### 3️⃣ Adicionar PostgreSQL (1 min)

**No mesmo projeto:**
1. Click **"+ New"**
2. **"Database"** → **"PostgreSQL"**
3. Aguarde ~30 segundos

Railway criará automaticamente a variável `DATABASE_URL` ✅

### 4️⃣ Configurar Variáveis (2 min)

**Click na sua aplicação → "Variables" → "New Variable":**

```bash
# Copie e cole cada linha:

ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:$PORT
WhatsApp__VerifyToken=clinicapsi_webhook_token_2025
```

**Opcional (se usar WhatsApp):**
```bash
WhatsApp__AccessToken=seu_token_aqui
WhatsApp__PhoneNumberId=seu_id_aqui
WhatsApp__AppSecret=seu_secret_aqui
```

### 5️⃣ Deploy! (2 min)

Railway fará automaticamente:
- ✅ Build do Docker
- ✅ Deploy da aplicação
- ✅ Configuração de rede
- ✅ SSL grátis

**Ver progresso:** Aba "Deployments"

### 6️⃣ Migrar Dados do AWS (1 min)

**No seu computador:**
```powershell
.\migrate-to-railway.ps1
```

O script fará tudo automaticamente! 🎉

---

## 🌐 Acessar Aplicação

**URL temporária Railway:**
```
https://seu-app.up.railway.app
```

**Configurar domínio customizado:**
1. Settings → Domains → Custom Domain
2. Digite: `www.psiianasantos.com.br`
3. Configure CNAME no seu DNS

---

## 📊 Verificar Funcionamento

```powershell
# Health check
curl https://seu-app.up.railway.app/health

# Página principal
curl https://seu-app.up.railway.app/
```

---

## 🔧 Comandos Úteis

```powershell
# Ver logs em tempo real
railway logs

# Abrir dashboard
railway open

# Status do deploy
railway status
```

---

## 💰 Custo

**Plano Hobby:** $5/mês
- ✅ Tudo incluído
- ✅ PostgreSQL grátis
- ✅ SSL grátis
- ✅ 500h/mês (suficiente para 24/7)

**Economia vs AWS:** ~87% ($80/mês → $5/mês) 🎉

---

## ✅ Pronto!

Seu sistema está rodando no Railway! 🚂

**Documentação completa:** `RAILWAY-DEPLOY.md`

**Problemas?** 
- 📚 https://docs.railway.app
- 💬 Discord: https://discord.gg/railway
