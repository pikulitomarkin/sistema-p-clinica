# 🚀 GUIA RÁPIDO: Configurar Railway Database

## ✅ STATUS ATUAL
- [x] Build bem-sucedido
- [x] Código corrigido para ler DATABASE_URL
- [ ] **Configurar PostgreSQL no Railway** ← VOCÊ ESTÁ AQUI
- [ ] Testar aplicação funcionando

---

## 📋 PASSO A PASSO

### 1️⃣ ADICIONAR POSTGRESQL (2 minutos)

1. Acesse: https://railway.app/dashboard
2. Abra seu projeto `sistema-p-clinica-production`
3. Clique em **"+ New"** (canto superior direito)
4. Selecione **"Database"** → **"Add PostgreSQL"**
5. Aguarde 30 segundos (Railway provisiona automaticamente)

### 2️⃣ COPIAR DATABASE_URL (1 minuto)

1. Clique no **PostgreSQL plugin** que acabou de criar
2. Vá na aba **"Variables"** (lado direito)
3. Encontre a variável **`DATABASE_URL`**
4. Clique em **"Copy"** para copiar o valor
   - Formato: `postgresql://postgres:[senha]@[host].railway.app:5432/railway`

### 3️⃣ ADICIONAR VARIÁVEL NO SERVIÇO WEB (1 minuto)

1. Volte para a visualização geral do projeto
2. Clique no seu serviço **Web** (não no PostgreSQL)
3. Vá em **"Variables"** (menu lateral)
4. Clique em **"+ New Variable"**
5. Cole:
   - **Nome**: `DATABASE_URL`
   - **Valor**: [o valor que você copiou do PostgreSQL]
6. Clique em **"Add"**

### 4️⃣ AGUARDAR REDEPLOY (2-3 minutos)

- Railway detecta automaticamente a mudança
- Vai fazer redeploy do serviço
- Aguarde os logs mostrarem: `Application started`

---

## 🔍 COMO VERIFICAR SE FUNCIONOU

### Railway Dashboard:
1. Vá em **Deployments** → último deployment
2. Clique em **"View Logs"**
3. Procure por:
   ```
   ✅ Database criado/verificado com sucesso!
   ✅ Usuário admin criado: marcos@admin.com
   ✅ Application started. Press Ctrl+C to shut down.
   ```

### Testar a URL:
1. Copie a URL do Railway (formato: `https://[projeto].up.railway.app`)
2. Abra no navegador
3. Deve carregar a página de login
4. Entre com:
   - **Email**: marcos@admin.com
   - **Senha**: marcos123

---

## ❌ PROBLEMAS COMUNS

### "Application failed to respond"
**Causa**: DATABASE_URL não configurada ou incorreta

**Solução**:
1. Verifique se adicionou DATABASE_URL no serviço Web (não no PostgreSQL)
2. Verifique se copiou o valor completo (começa com `postgresql://`)
3. Faça redeploy manualmente se necessário

### Build falhou após mudança
**Causa**: Improvável, o código está validado

**Solução**:
1. Veja os logs de build
2. Me envie o erro que vou corrigir

### Página carrega mas não conecta ao banco
**Causa**: DATABASE_URL incorreta

**Solução**:
1. No Railway, vá no PostgreSQL → Variables
2. Copie DATABASE_URL novamente
3. Substitua no serviço Web
4. Aguarde redeploy

---

## 📊 O QUE ACONTECE INTERNAMENTE

### Antes da correção:
```csharp
// Program.cs (antigo)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=clinicapsi.db";
```
❌ Tentava conectar ao AWS RDS (bloqueado)

### Depois da correção:
```csharp
// Program.cs (novo)
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=clinicapsi.db";
```
✅ Prioriza DATABASE_URL do Railway

---

## 🎯 PRÓXIMOS PASSOS APÓS FUNCIONAR

1. ✅ **Aplicação funcionando no Railway**
2. 📊 **Migrar dados da AWS** (usar script `migrate-to-railway.ps1`)
3. 🌐 **Configurar domínio** www.psiianasantos.com.br
4. 🛑 **Desligar AWS** (economizar $95/mês)

---

## 💰 ECONOMIA ESTIMADA

| Item | AWS (atual) | Railway (novo) | Economia |
|------|------------|----------------|----------|
| ECS Fargate | $55/mês | - | $55/mês |
| RDS PostgreSQL | $28/mês | - | $28/mês |
| Elastic IP | $3.6/mês | - | $3.6/mês |
| NAT Gateway | $32/mês | - | $32/mês |
| **TOTAL** | **$118.60/mês** | **$5/mês** | **$113.60/mês** |
| **Anual** | **$1,423/ano** | **$60/ano** | **$1,363/ano** |

---

## 🆘 PRECISA DE AJUDA?

Se encontrar qualquer erro:
1. Tire screenshot dos logs do Railway
2. Me envie o erro
3. Vou corrigir imediatamente!

---

**⏱️ TEMPO ESTIMADO TOTAL**: 6-7 minutos

**ÚLTIMA ATUALIZAÇÃO**: Commit `11bca20` - Program.cs corrigido para Railway
