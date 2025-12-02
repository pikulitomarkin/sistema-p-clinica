# 🔧 Instruções para Configurar UserId do Psicólogo

## Problema Resolvido
- ✅ Psicólogo agora relacionado com usuário logado via UserId (GUID)
- ✅ Prontuários filtrados automaticamente por psicólogo
- ✅ Psicólogo pré-selecionado no formulário de cadastro
- ✅ Cada psicólogo vê apenas seus próprios prontuários

## ⚠️ AÇÃO NECESSÁRIA: Atualizar Banco de Dados

### Passo 1: Acessar Railway Database
1. Acesse: https://railway.app
2. Entre no projeto **sistema-p-clinica**
3. Clique no serviço **PostgreSQL**
4. Clique na aba **Query**

### Passo 2: Executar Script SQL

Cole e execute o seguinte SQL:

```sql
-- 1. Verificar dados atuais
SELECT "Id", "Nome", "Email" FROM "Psicologos";
SELECT "Id", "UserName", "Email" FROM "AspNetUsers";

-- 2. Atualizar o UserId do psicólogo
UPDATE "Psicologos" 
SET "UserId" = (
    SELECT "Id" 
    FROM "AspNetUsers" 
    WHERE "Email" = 'psii.anasantos@gmail.com'
)
WHERE "Email" = 'psii.anasantos@gmail.com';

-- 3. Confirmar atualização
SELECT p."Id", p."Nome", p."Email", p."UserId", u."UserName"
FROM "Psicologos" p
LEFT JOIN "AspNetUsers" u ON p."UserId" = u."Id";
```

### Passo 3: Verificar Resultado

Após executar, você deve ver:
- A coluna `UserId` do psicólogo preenchida com um GUID
- O GUID deve corresponder ao `Id` do usuário em AspNetUsers

**Exemplo esperado:**
```
Id | Nome         | Email                    | UserId                               | UserName
1  | Ana Santos   | psii.anasantos@gmail.com | f125388d-b088-43ab-8856-ad854a03db13 | psii.anasantos@gmail.com
```

## 🎯 O Que Mudou no Sistema

### Antes:
- ❌ Campo de seleção manual do psicólogo
- ❌ Possibilidade de ver prontuários de outros psicólogos
- ❌ Erro ao tentar converter GUID para int

### Depois:
- ✅ Psicólogo identificado automaticamente pelo login
- ✅ Campo de psicólogo desabilitado (pré-selecionado)
- ✅ Listagem filtra apenas prontuários do psicólogo logado
- ✅ Sistema multiusuário funcionando corretamente

## 📝 Fluxo Atual

1. **Login:** Psicólogo faz login com email/senha
2. **Sistema identifica:** Busca UserId (GUID) do usuário logado
3. **Busca Psicólogo:** Encontra registro em Psicologos onde UserId corresponde
4. **Novo Prontuário:** PsicologoId pré-selecionado automaticamente
5. **Listagem:** Mostra apenas prontuários deste psicólogo

## 🔄 Para Novos Psicólogos

Quando cadastrar um novo psicólogo no sistema, execute:

```sql
UPDATE "Psicologos" 
SET "UserId" = (
    SELECT "Id" 
    FROM "AspNetUsers" 
    WHERE "Email" = 'email.do.psicologo@exemplo.com'
)
WHERE "Email" = 'email.do.psicologo@exemplo.com';
```

## 🚀 Próximos Passos

1. Execute o script SQL no Railway
2. Aguarde o deploy completar (~2-3 min)
3. Faça logout e login novamente
4. Crie um novo prontuário
5. Verifique que aparece na lista de prontuários

## 📊 Commits Relacionados

- `f5a3ee1` - feat: relacionar psicologo com usuario logado e filtrar prontuarios
- `1da5099` - fix: migrar Bootstrap para CDN (página agendamento)
- `e880934` - fix: corrigir erro GUID ao salvar prontuario

---
**Status:** ✅ Código deployado | ⏳ Aguardando atualização SQL no banco
