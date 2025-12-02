# 🔧 Como Executar SQL no Railway PostgreSQL

## Opção 1: Usando Railway CLI (Recomendado)

### Passo 1: Instalar Railway CLI
```powershell
# Instalar via npm
npm install -g @railway/cli

# OU baixar direto: https://docs.railway.app/develop/cli
```

### Passo 2: Fazer Login
```powershell
railway login
```

### Passo 3: Conectar ao Projeto
```powershell
cd c:\Users\Admin\sistema-p-clinica-clean
railway link
# Selecione o projeto: sistema-p-clinica
```

### Passo 4: Abrir PostgreSQL Shell
```powershell
railway connect postgres
```

### Passo 5: Executar o SQL
Uma vez conectado ao PostgreSQL, cole o SQL:

```sql
-- Verificar dados
SELECT "Id", "Nome", "Email" FROM "Psicologos";
SELECT "Id", "UserName", "Email" FROM "AspNetUsers";

-- Atualizar UserId
UPDATE "Psicologos" 
SET "UserId" = (
    SELECT "Id" 
    FROM "AspNetUsers" 
    WHERE "Email" = 'psii.anasantos@gmail.com'
)
WHERE "Email" = 'psii.anasantos@gmail.com';

-- Confirmar
SELECT p."Id", p."Nome", p."UserId", u."UserName"
FROM "Psicologos" p
LEFT JOIN "AspNetUsers" u ON p."UserId" = u."Id";
```

---

## Opção 2: Usando DBeaver / pgAdmin

### Passo 1: Pegar Credenciais no Railway
1. Acesse: https://railway.app
2. Entre no projeto **sistema-p-clinica**
3. Clique no serviço **Postgres**
4. Vá na aba **Variables** ou **Connect**
5. Copie as credenciais:
   - `PGHOST`
   - `PGPORT`
   - `PGUSER`
   - `PGPASSWORD`
   - `PGDATABASE`

### Passo 2: Instalar Cliente PostgreSQL

**DBeaver (Recomendado):**
- Download: https://dbeaver.io/download/
- Gratuito e fácil de usar

**pgAdmin:**
- Download: https://www.pgadmin.org/download/

### Passo 3: Conectar

**No DBeaver:**
1. New Connection → PostgreSQL
2. Preencha com as credenciais do Railway
3. Test Connection → Finish

**No pgAdmin:**
1. Add New Server
2. Connection tab → preencha os dados
3. Save

### Passo 4: Executar SQL
1. Abra Query Tool / SQL Editor
2. Cole o conteúdo do arquivo `UPDATE_PSICOLOGO_USERID.sql`
3. Execute (F5 ou botão Execute)

---

## Opção 3: Usando TablePlus (Mac/Windows)

1. Download: https://tableplus.com/
2. New Connection → PostgreSQL
3. Preencha credenciais do Railway
4. Open SQL Editor
5. Execute o script

---

## Opção 4: Criar Endpoint na Aplicação (Temporário)

Se nenhuma opção acima funcionar, posso criar um endpoint `/admin/update-userid` que executa o SQL automaticamente. 

**Vantagem:** Executa direto pelo navegador
**Desvantagem:** Precisa remover após usar (segurança)

Quer que eu crie este endpoint?

---

## 🎯 Qual você prefere?

1. **Railway CLI** - Mais rápido se você já tem Node.js
2. **DBeaver** - Interface gráfica amigável
3. **Endpoint temporário** - Execute pelo navegador (vou criar agora)

Escolha uma opção e eu te ajudo!
