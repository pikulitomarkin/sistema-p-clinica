# 🔧 Como Aplicar a Migration no Railway (PostgreSQL)

## ⚠️ Problema Encontrado

A página de detalhes de usuário está gerando erro porque a coluna `Formato` não existe na tabela `Consultas`.

**Erro:**
```
Erro ao carregar dados do usuário: 42703: column c.Formato does not exist
POSITION: 159
```

## ✅ Solução: Executar SQL Manualmente

### Opção 1: Via Railway Dashboard (Recomendado)

1. **Acesse o Railway Dashboard:**
   - https://railway.app

2. **Entre no Projeto:**
   - Selecione o projeto ClinicaPsi

3. **Acesse o PostgreSQL:**
   - Clique no serviço PostgreSQL
   - Vá na aba "Data" ou "Query"

4. **Execute o SQL:**
   Copie e cole o script abaixo:

```sql
-- Adicionar coluna Formato com valor padrão 1 (Presencial)
ALTER TABLE "Consultas" 
ADD COLUMN IF NOT EXISTS "Formato" integer NOT NULL DEFAULT 1;

-- Verificar se foi criada
SELECT column_name, data_type, column_default 
FROM information_schema.columns 
WHERE table_name = 'Consultas' AND column_name = 'Formato';

-- Atualizar consultas existentes
UPDATE "Consultas" 
SET "Formato" = 1 
WHERE "Formato" IS NULL OR "Formato" = 0;
```

5. **Confirme o sucesso:**
   - A query deve retornar a coluna criada
   - Verifique se o `column_default` é `1`

### Opção 2: Via Railway CLI

```bash
# 1. Instalar Railway CLI (se não tiver)
npm install -g @railway/cli

# 2. Login
railway login

# 3. Link ao projeto
railway link

# 4. Conectar ao PostgreSQL
railway connect postgres

# 5. No prompt do psql, execute:
ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "Formato" integer NOT NULL DEFAULT 1;

# 6. Verificar
SELECT column_name, data_type, column_default 
FROM information_schema.columns 
WHERE table_name = 'Consultas' AND column_name = 'Formato';
```

### Opção 3: Aplicar Migration Automaticamente

A migration já foi criada e está no código. Para aplicá-la automaticamente no próximo deploy:

**Arquivo:** `src/ClinicaPsi.Infrastructure/Migrations/20251205011435_AdicionarFormatoConsulta.cs`

A migration será aplicada automaticamente quando:
1. O app for deployado no Railway
2. O método `database.Migrate()` for chamado no startup

**Verifique no `Program.cs` se existe:**
```csharp
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate(); // Aplica migrations pendentes
}
```

## 📋 Valores do Enum FormatoConsulta

```csharp
public enum FormatoConsulta
{
    Presencial = 1,  // Padrão
    Online = 2
}
```

## ✅ Após Executar o SQL

1. **Recarregue a página** de detalhes do usuário
2. **Verifique** se o erro desapareceu
3. **Teste** agendar uma nova consulta com formato Online/Presencial

## 🔍 Verificação

Para confirmar que tudo está correto, execute:

```sql
-- Ver estrutura da tabela
SELECT column_name, data_type, is_nullable, column_default 
FROM information_schema.columns 
WHERE table_name = 'Consultas' 
ORDER BY ordinal_position;

-- Ver consultas com formato
SELECT "Id", "DataHorario", "Status", "Formato" 
FROM "Consultas" 
LIMIT 10;
```

---

**Arquivo SQL disponível:** `ADD_FORMATO_COLUMN.sql`
**Data:** 4 de dezembro de 2025
