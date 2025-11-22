# 🚂 Deploy no Railway - ClinicaPsi

## 📋 Pré-requisitos

1. ✅ Conta no Railway: https://railway.app
2. ✅ Repositório GitHub conectado
3. ✅ Dados do PostgreSQL atual (para migração)

---

## 🚀 Passo 1: Criar Projeto no Railway

### 1.1 Acessar Railway
```
https://railway.app
```

### 1.2 Criar Novo Projeto
1. Click em **"New Project"**
2. Selecione **"Deploy from GitHub repo"**
3. Conecte sua conta GitHub
4. Selecione o repositório: `sistema-p-clinica`
5. Selecione a branch: `main`

### 1.3 Railway detectará automaticamente:
- ✅ .NET 9.0
- ✅ Dockerfile
- ✅ Configurações necessárias

---

## 🗄️ Passo 2: Adicionar PostgreSQL

### 2.1 No Dashboard do Railway:
1. Click em **"+ New"**
2. Selecione **"Database"**
3. Escolha **"PostgreSQL"**
4. Aguarde provisionamento (~30 segundos)

### 2.2 Railway criará automaticamente:
- ✅ Banco PostgreSQL 15
- ✅ Variável `DATABASE_URL`
- ✅ Conectado ao seu serviço

---

## ⚙️ Passo 3: Configurar Variáveis de Ambiente

### 3.1 No serviço ClinicaPsi:
1. Click na sua aplicação
2. Vá em **"Variables"**
3. Adicione as seguintes variáveis:

```bash
# Connection String (Railway fornece automaticamente DATABASE_URL)
# Mas precisamos no formato correto para .NET
ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}

# ASP.NET Core
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:$PORT

# WhatsApp (se usar)
WhatsApp__AccessToken=seu_access_token_aqui
WhatsApp__PhoneNumberId=seu_phone_number_id
WhatsApp__VerifyToken=clinicapsi_webhook_token_2025
WhatsApp__AppSecret=seu_app_secret

# OpenAI (se usar bot)
OpenAI__ApiKey=sua_api_key_aqui
```

### 3.2 Variáveis Automáticas do Railway:
Railway já configura automaticamente:
- ✅ `PORT` - Porta da aplicação
- ✅ `DATABASE_URL` - URL do PostgreSQL
- ✅ `RAILWAY_ENVIRONMENT` - Nome do ambiente

---

## 📊 Passo 4: Migrar Dados do AWS RDS

### 4.1 Exportar dados do AWS RDS:

**No seu computador local:**

```powershell
# Instalar PostgreSQL client (se não tiver)
# Windows: https://www.postgresql.org/download/windows/

# Fazer backup do banco AWS
$env:PGPASSWORD="1212Ervadoce"
pg_dump -h clinicapsi-db.cqbooyc6uuiz.us-east-1.rds.amazonaws.com `
        -U psiadmin `
        -d clinicapsi `
        -F c `
        -b `
        -v `
        -f "clinicapsi-backup.dump"
```

### 4.2 Importar para Railway PostgreSQL:

```powershell
# Pegar credenciais do Railway
# No dashboard Railway > PostgreSQL > Connect > Copy Connection String

# Importar dados
$env:PGPASSWORD="senha_do_railway"
pg_restore --verbose --clean --no-acl --no-owner `
           -h railway-host.railway.app `
           -U postgres `
           -d railway `
           clinicapsi-backup.dump
```

**OU usar SQL simples:**

```powershell
# Export como SQL
pg_dump -h clinicapsi-db.cqbooyc6uuiz.us-east-1.rds.amazonaws.com `
        -U psiadmin `
        -d clinicapsi `
        > clinicapsi-backup.sql

# Import
psql -h railway-host.railway.app `
     -U postgres `
     -d railway `
     < clinicapsi-backup.sql
```

---

## 🌐 Passo 5: Configurar Domínio Customizado

### 5.1 No Railway:
1. Click na sua aplicação
2. Vá em **"Settings"**
3. Scroll até **"Domains"**
4. Click em **"Generate Domain"** (Railway fornece domínio grátis)

### 5.2 Configurar seu domínio (www.psiianasantos.com.br):

1. No Railway, click em **"Custom Domain"**
2. Digite: `www.psiianasantos.com.br`
3. Railway mostrará os registros DNS necessários

### 5.3 No seu provedor de domínio (HostGator):

Adicione os registros CNAME:

```
Type: CNAME
Name: www
Value: seu-app.up.railway.app
TTL: 3600
```

**Tempo de propagação**: 5 minutos a 48 horas (geralmente 10-30 min)

---

## ✅ Passo 6: Verificar Deploy

### 6.1 No Railway Dashboard:
- ✅ Build status: **Success**
- ✅ Deploy status: **Active**
- ✅ Health check: **Passing**

### 6.2 Testar a aplicação:

```powershell
# Testar health endpoint
curl https://seu-app.up.railway.app/health

# Testar página principal
curl https://seu-app.up.railway.app/
```

### 6.3 Ver logs em tempo real:

No Railway:
1. Click na sua aplicação
2. Vá em **"Deployments"**
3. Click no deployment ativo
4. Veja os logs em tempo real

---

## 🔧 Comandos Úteis Railway CLI

### Instalar Railway CLI:

```powershell
# Windows (via npm)
npm install -g @railway/cli

# OU via Scoop
scoop install railway
```

### Comandos úteis:

```powershell
# Login
railway login

# Conectar ao projeto
railway link

# Ver logs
railway logs

# Abrir no browser
railway open

# Deploy manual
railway up

# Ver variáveis
railway variables

# Executar comando no container
railway run [comando]
```

---

## 💰 Custos Railway

### Plano Hobby (Recomendado):
- **$5 USD/mês** ($500 horas de execução)
- ✅ 8GB RAM
- ✅ 8 vCPU compartilhados
- ✅ 100GB bandwidth
- ✅ PostgreSQL incluído
- ✅ SSL automático
- ✅ Domínio customizado

### Plano Pro (Se precisar escalar):
- **$20 USD/mês** + uso adicional
- ✅ Recursos dedicados
- ✅ Prioridade no suporte
- ✅ Métricas avançadas

**Comparação com AWS**: ~87% de economia! 🎉

---

## 🔄 CI/CD Automático

Railway já configura CI/CD automaticamente:

```
Git Push → Railway detecta → Build automático → Deploy automático
```

**Workflow:**
1. Você faz alterações no código
2. `git push origin main`
3. Railway detecta o push
4. Faz build automático
5. Deploy em produção
6. Rollback fácil se der erro

---

## 📈 Monitoramento

### No Dashboard Railway:

1. **Metrics**:
   - CPU usage
   - Memory usage
   - Network I/O
   - Request count

2. **Logs**:
   - Application logs
   - Build logs
   - Deploy logs

3. **Health Checks**:
   - Status do endpoint `/health`
   - Alertas automáticos

---

## 🆘 Troubleshooting

### Build falhou?

```powershell
# Ver logs completos
railway logs --deployment [deployment-id]

# Verificar Dockerfile
railway run cat Dockerfile.railway
```

### Aplicação não inicia?

```powershell
# Ver logs do container
railway logs

# Verificar variáveis de ambiente
railway variables

# Testar localmente
docker build -f Dockerfile.railway -t clinicapsi-test .
docker run -p 5000:5000 -e PORT=5000 clinicapsi-test
```

### Banco de dados não conecta?

```powershell
# Verificar connection string
railway variables | grep DATABASE

# Testar conexão
railway run psql $DATABASE_URL
```

---

## 📞 Suporte

- 📚 Docs: https://docs.railway.app
- 💬 Discord: https://discord.gg/railway
- 📧 Email: team@railway.app
- 🐦 Twitter: @Railway

---

## ✅ Checklist Final

Antes de desligar AWS:

- [ ] Deploy no Railway funcionando
- [ ] Dados migrados com sucesso
- [ ] Domínio configurado e funcionando
- [ ] SSL ativo (Railway fornece grátis)
- [ ] Todas variáveis configuradas
- [ ] Logs sem erros
- [ ] Health check passando
- [ ] Backup dos dados AWS feito
- [ ] Testar login
- [ ] Testar criação de consulta
- [ ] Testar WhatsApp (se usar)
- [ ] Monitorar por 24-48h

---

## 🎯 Próximos Passos

Depois do deploy estável:

1. ✅ Desligar recursos AWS
2. ✅ Liberar Elastic IP
3. ✅ Cancelar serviços AWS
4. ✅ Economia de ~$80/mês! 💰

---

**Data**: 21/11/2025
**Versão**: v1.0
**Status**: ✅ Pronto para deploy
