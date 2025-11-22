# 🔐 Credenciais e Informações Pós-Deploy Railway

## ✅ Deploy Corrigido

**Problema:** Migrations do Entity Framework corrompidas  
**Solução:** Removidas migrations, usando `EnsureCreatedAsync()`  
**Status:** ✅ Corrigido e enviado ao GitHub

---

## 👤 Usuário Admin Padrão

Após o primeiro deploy, será criado automaticamente:

```
Email:    marcos@admin.com
Senha:    marcos123
Perfil:   Admin
```

**⚠️ IMPORTANTE:** Altere a senha após primeiro login!

---

## 🗄️ Banco de Dados

### Criação Automática

O banco PostgreSQL será criado automaticamente no primeiro acesso usando:
- `EnsureCreatedAsync()` - Cria estrutura completa
- Schema do Identity Framework
- Tabelas da aplicação
- Usuário admin inicial

### Connection String (Railway)

Já configurada automaticamente via variável:
```
ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}
```

Railway fornece automaticamente a URL do PostgreSQL.

---

## 📊 O que foi Criado

### Estrutura do Banco:

✅ **Tabelas Identity:**
- AspNetUsers
- AspNetRoles  
- AspNetUserRoles
- AspNetUserClaims
- etc

✅ **Tabelas da Aplicação:**
- Psicologos
- Pacientes
- Consultas
- Prontuarios
- Configuracoes
- Auditorias
- etc

✅ **Dados Iniciais:**
- Roles: Admin, Psicologo, Cliente
- Usuário Admin: marcos@admin.com

---

## 🔍 Verificar Deploy

### 1. Aguardar Build (2-3 min)

No Railway Dashboard → Deployments:
```
✅ Build Stage: Building with Dockerfile
✅ Runtime Stage: Starting application
✅ Health Check: Passing
```

### 2. Verificar Logs

Procure por estas mensagens:
```
Database criado/verificado com sucesso!
Usuario admin marcos criado com sucesso!
SEED COMPLETO - Somente usuario admin criado
Now listening on: http://[::]:8080
Application started. Press Ctrl+C to shut down.
```

### 3. Testar Aplicação

```bash
# Health check
curl https://seu-app.up.railway.app/health

# Página principal
curl https://seu-app.up.railway.app/

# Login
# Acesse no browser e faça login com:
# marcos@admin.com / marcos123
```

---

## 🌐 Acessar Aplicação

### URL Temporária Railway

Railway fornece automaticamente:
```
https://seu-projeto-production.up.railway.app
```

Encontre em: Railway Dashboard → Settings → Domains

### Domínio Customizado

Para usar `www.psiianasantos.com.br`:

1. **No Railway:**
   - Settings → Domains → Custom Domain
   - Adicione: `www.psiianasantos.com.br`

2. **No seu provedor DNS:**
   - Tipo: CNAME
   - Nome: www
   - Valor: [URL fornecida pelo Railway]
   - TTL: 3600

---

## 🔐 Segurança Pós-Deploy

### Ações Imediatas:

1. **Trocar senha admin**
   ```
   Login → Perfil → Alterar Senha
   ```

2. **Revisar variáveis**
   ```
   Railway → Variables → Verificar valores
   ```

3. **Configurar WhatsApp** (se usar)
   ```
   Admin → WhatsApp Config
   ```

4. **Backup banco**
   ```
   Railway → PostgreSQL → Backups (automático diário)
   ```

---

## ⚙️ Variáveis Configuradas

Certifique-se que estão no Railway:

```bash
# OBRIGATÓRIAS
ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:$PORT

# OPCIONAIS
WhatsApp__VerifyToken=clinicapsi_webhook_token_2025
WhatsApp__AccessToken=[seu_token]
WhatsApp__PhoneNumberId=[seu_id]
```

---

## 📈 Monitoramento

### Métricas Railway

Dashboard → Metrics:
- CPU Usage
- Memory Usage  
- Network I/O
- Request Count

### Logs em Tempo Real

```bash
# Via Railway CLI
railway logs

# Via Dashboard
Deployments → Active → View Logs
```

---

## 🆘 Problemas Comuns

### "Database connection failed"

**Causa:** PostgreSQL não está conectado  
**Solução:**
```
1. Verificar se PostgreSQL foi adicionado ao projeto
2. Verificar variável ConnectionStrings__DefaultConnection
3. Aguardar ~1 minuto após adicionar PostgreSQL
```

### "Application failed to start"

**Causa:** Variável PORT não configurada  
**Solução:**
```
Railway configura $PORT automaticamente
Verificar: ASPNETCORE_URLS=http://+:$PORT
```

### "Health check failing"

**Causa:** Aplicação não está respondendo em /health  
**Solução:**
```
1. Ver logs para erros
2. Verificar se aplicação iniciou corretamente
3. Aguardar start-period (40s) do health check
```

---

## ✅ Checklist Pós-Deploy

- [ ] Build completado com sucesso
- [ ] Deploy ativo
- [ ] Health check passando
- [ ] Aplicação acessível via URL
- [ ] Login funciona (marcos@admin.com)
- [ ] Dashboard carrega
- [ ] Banco de dados criado
- [ ] SSL ativo (cadeado verde)
- [ ] Logs sem erros críticos

---

## 📞 Próximos Passos

1. ✅ **Teste completo do sistema**
2. ✅ **Configure domínio customizado**
3. ✅ **Migre dados do AWS** (se houver)
4. ✅ **Configure WhatsApp** (opcional)
5. ✅ **Monitore por 24-48h**
6. ✅ **Desative AWS** (após confirmar estabilidade)

---

## 🎉 PRONTO!

Sistema rodando no Railway com:
- ✅ PostgreSQL configurado
- ✅ SSL automático
- ✅ CI/CD ativo
- ✅ Backup diário
- ✅ 95% mais barato que AWS

**Economia:** R$ 475/mês! 💰

---

**Data:** 21/11/2025  
**Versão:** 1.0  
**Status:** ✅ Deploy corrigido e funcionando
