# 🔧 FIX: Erro "Error creating build plan with Railpack"

## ✅ CORREÇÃO APLICADA

Os arquivos foram corrigidos e enviados ao GitHub:
- ✅ `railway.json` - Configurado para usar DOCKERFILE
- ✅ `railway.toml` - Configuração simplificada
- ✅ `Dockerfile.railway` - Dockerfile otimizado
- ✅ Removido `nixpacks.toml` (causava conflito)

---

## 🎯 SOLUÇÃO NO RAILWAY

### Opção 1: Redesploy Automático (Recomendado)

O Railway detectará automaticamente o push e tentará fazer deploy novamente.

**Aguarde 1-2 minutos** e verifique no Dashboard.

---

### Opção 2: Deploy Manual no Railway

Se não redesployer automaticamente:

1. **Acesse o Railway Dashboard**
   ```
   https://railway.app/project/seu-projeto
   ```

2. **Vá em Settings do seu serviço**
   - Click no serviço "ClinicaPsi" ou "sistema-p-clinica"
   - Aba **"Settings"**

3. **Configure o Builder**
   - Scroll até **"Build"**
   - **Builder:** Selecione `Dockerfile`
   - **Dockerfile Path:** Digite `Dockerfile.railway`
   - **Docker Build Context:** Deixe `./` (raiz)

4. **Salvar e Redesploy**
   - Click em **"Save"**
   - Volte para **"Deployments"**
   - Click em **"Redeploy"** no último deployment

---

### Opção 3: Recriar Serviço (Se necessário)

Se ainda assim não funcionar:

1. **Deletar serviço atual**
   - Settings → Perigos → Delete Service

2. **Criar novo serviço**
   - + New → GitHub Repo
   - Selecione: `sistema-p-clinica`
   - Branch: `main`

3. **Railway detectará automaticamente:**
   - ✅ `railway.toml` → Usa Dockerfile
   - ✅ `Dockerfile.railway` → Build .NET 9.0
   - ✅ Configurações corretas

---

## 🔍 VERIFICAR SE FUNCIONOU

### No Railway Dashboard:

1. **Build deve mostrar:**
   ```
   ✅ Using Dockerfile builder
   ✅ Building with Dockerfile.railway
   ✅ .NET SDK 9.0 detected
   ```

2. **Deploy deve mostrar:**
   ```
   ✅ Build successful
   ✅ Deploy started
   ✅ Health check passing
   ```

### Logs esperados:

```
Building with Dockerfile.railway...
Step 1/15 : FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
Step 2/15 : WORKDIR /source
...
Step 15/15 : CMD ASPNETCORE_URLS=http://+:$PORT dotnet ClinicaPsi.Web.dll
Successfully built
Deploy successful!
```

---

## ⚠️ SE AINDA DER ERRO

### Erro: "Dockerfile.railway not found"

**Solução:**
```powershell
# Verificar se arquivo existe
ls Dockerfile.railway

# Se não existir, commit novamente
git add -f Dockerfile.railway
git commit -m "add: Dockerfile.railway"
git push origin main
```

### Erro: "Port $PORT not defined"

**Solução:**
No Railway → Variables → Adicione:
```
PORT=8080
```

### Erro: "Connection refused"

**Solução:**
Verifique as variáveis:
```
ASPNETCORE_URLS=http://+:$PORT
ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}
```

---

## 📊 VARIÁVEIS NECESSÁRIAS

Certifique-se de ter configurado no Railway → Variables:

```bash
# OBRIGATÓRIAS
ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:$PORT

# OPCIONAIS (WhatsApp)
WhatsApp__VerifyToken=clinicapsi_webhook_token_2025
WhatsApp__AccessToken=seu_token
WhatsApp__PhoneNumberId=seu_id
```

---

## ✅ CHECKLIST PÓS-FIX

- [ ] Push feito com sucesso no GitHub
- [ ] Railway detectou novo commit
- [ ] Build usando Dockerfile
- [ ] Build completado com sucesso
- [ ] Deploy ativo
- [ ] Health check passando
- [ ] Aplicação acessível

---

## 🎉 PRONTO!

Agora o Railway deve fazer build corretamente usando o Dockerfile.

**Tempo estimado:** 3-5 minutos para build completo

**Acompanhe em tempo real:**
```
Railway Dashboard → Deployments → View Logs
```

---

**Próximo erro?** Me avise e vamos resolver! 🚀

---

**Data:** 21/11/2025
**Status:** ✅ Correção aplicada e commitada
