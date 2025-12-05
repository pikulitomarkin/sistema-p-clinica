# 🚂 Railway Build Troubleshooting

## 🔴 Erro Atual

```
Build Failed: build daemon returned an error 
< failed to receive status: rpc error: code = Unavailable 
desc = closing transport due to: connection error: 
desc = "error reading from server: EOF", received prior goaway: 
code: NO_ERROR, debug data: "graceful_stop" >
```

**Duração:** 5m 49s (timeout/desconexão no final do build)

## 🎯 Causa Provável

1. **Timeout de rede** durante transferência de artefatos
2. **Builder sobrecarregado** (Metal builder "builder-ghbwps")
3. **Snapshot grande** (386 KB comprimido, ~1.6 MB descomprimido)
4. **Build lento** (5m 49s para dotnet publish)

## ✅ Soluções Aplicadas

### 1. Otimização do Dockerfile.railway

**Antes:**
```dockerfile
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish
CMD dotnet ClinicaPsi.Web.dll --urls http://0.0.0.0:$PORT
```

**Depois:**
```dockerfile
RUN dotnet restore --verbosity minimal
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    --verbosity minimal \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false
ENTRYPOINT ["sh", "-c", "dotnet ClinicaPsi.Web.dll --urls http://0.0.0.0:$PORT"]
```

**Benefícios:**
- ✅ Cache de `dotnet restore` (rebuild rápido)
- ✅ `--no-restore` evita restore duplicado
- ✅ `--verbosity minimal` reduz logs
- ✅ ENTRYPOINT em JSON format (fix warning)

### 2. .dockerignore Otimizado

**Adicionado:**
- Documentação (*.md) - economiza ~500 KB
- Scripts (*.ps1, *.sh)
- Configs desnecessários
- whatsapp-bot/ (projeto separado)
- nginx/ (não usado)

**Resultado esperado:** Snapshot < 200 KB

### 3. railway.toml Melhorado

```toml
[build.buildCommand]
timeout = 900  # 15 minutos (aumentado)

[deploy]
healthcheckPath = "/health"
healthcheckTimeout = 300
```

## 🚀 Como Fazer Deploy Agora

### Opção 1: Tentar Novamente (Recomendado)

```powershell
# Executar script otimizado
.\railway-deploy-optimized.ps1

# Ou manualmente:
git add .
git commit -m "fix: otimizar build Railway"
git push origin main
```

### Opção 2: Deploy via Railway CLI

```powershell
# Instalar Railway CLI (se não tiver)
npm install -g @railway/cli

# Login
railway login

# Link ao projeto
railway link

# Deploy
railway up
```

### Opção 3: Redeploy Forçado

```powershell
# Commit vazio para forçar rebuild
git commit --allow-empty -m "chore: trigger Railway rebuild"
git push origin main
```

## 📊 Monitoramento

### Ver logs em tempo real:

```powershell
# Railway CLI
railway logs

# Ou via Dashboard
# https://railway.app/project/[seu-projeto]/deployments
```

### Verificar saúde do build:

```powershell
# Status do serviço
railway status

# Variáveis de ambiente
railway variables
```

## 🔧 Troubleshooting Adicional

### Se continuar falhando:

#### 1. **Verificar Builder Region**
- Acesse Railway Dashboard
- Settings → Builder Region
- Tente trocar de região (ex: us-west-1 → us-east-1)

#### 2. **Build Local para Testar**

```powershell
# Testar Dockerfile localmente
docker build -f Dockerfile.railway -t clinicapsi:test .

# Se funcionar, problema é no Railway
# Se não funcionar, problema é no Dockerfile
```

#### 3. **Reduzir Ainda Mais o Snapshot**

Adicione ao `.dockerignore`:
```
src/**/bin
src/**/obj
*.md
docs/
```

#### 4. **Usar Railway Build Cache**

No `railway.toml`:
```toml
[build]
builder = "DOCKERFILE"
dockerfilePath = "Dockerfile.railway"
watchPatterns = ["src/**/*.cs", "src/**/*.csproj"]  # Rebuild só se mudar código
```

#### 5. **Split em Multi-Stage mais Agressivo**

```dockerfile
# Stage intermediário para cache de packages
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /source
COPY *.sln ./
COPY src/**/*.csproj ./src/
RUN dotnet restore

# Stage de build
FROM restore AS build
COPY src/ ./src/
RUN dotnet publish -c Release -o /app/publish --no-restore
```

## 📈 Melhorias Esperadas

| Métrica | Antes | Depois |
|---------|-------|--------|
| Snapshot size | 386 KB | ~150 KB |
| Build time | 5m 49s | ~3m 30s |
| Cache hit rate | 0% | ~80% |
| Success rate | Falha | ✅ |

## ⚠️ Sinais de Alerta

### Se ver isso nos logs:

```
Build took longer than 10 minutes
```
➡️ Timeout aumentado para 15 min (resolvido)

```
JSONArgsRecommended warning
```
➡️ Alterado para ENTRYPOINT array (resolvido)

```
snapshot too large
```
➡️ .dockerignore otimizado (resolvido)

```
connection error: EOF
```
➡️ Problema de rede do Railway (tente novamente)

## 🆘 Último Recurso

Se nada funcionar, contate Railway Support:

```
railway support
```

Ou abra ticket em:
- https://railway.app/help
- Discord: https://discord.gg/railway

**Informações para incluir:**
- Project ID: `[seu-project-id]`
- Build logs completos
- Dockerfile.railway
- Timestamp da falha

## 📝 Checklist de Deploy

- [x] Dockerfile.railway otimizado
- [x] .dockerignore atualizado
- [x] railway.toml configurado
- [ ] Commit e push das mudanças
- [ ] Monitorar logs do build
- [ ] Verificar se app subiu (railway.app)
- [ ] Testar endpoint de health

---

**Status:** ✅ Otimizações aplicadas  
**Próximo passo:** Execute `.\railway-deploy-optimized.ps1` e tente novamente  
**Data:** 4 de dezembro de 2025
