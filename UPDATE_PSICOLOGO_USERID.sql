-- Script para atualizar o UserId do psicólogo cadastrado
-- Execute este script no banco de dados PostgreSQL do Railway

-- 1. Verificar o email do psicólogo cadastrado
SELECT "Id", "Nome", "Email", "CRP" FROM "Psicologos";

-- 2. Verificar os usuários cadastrados (AspNetUsers)
SELECT "Id", "UserName", "Email" FROM "AspNetUsers";

-- 3. Atualizar o psicólogo com o UserId correspondente ao email
-- SUBSTITUA 'psii.anasantos@gmail.com' pelo email correto se necessário
UPDATE "Psicologos" 
SET "UserId" = (
    SELECT "Id" 
    FROM "AspNetUsers" 
    WHERE "Email" = 'psii.anasantos@gmail.com'
)
WHERE "Email" = 'psii.anasantos@gmail.com';

-- 4. Verificar se foi atualizado corretamente
SELECT p."Id", p."Nome", p."Email", p."UserId", u."UserName"
FROM "Psicologos" p
LEFT JOIN "AspNetUsers" u ON p."UserId" = u."Id";

-- NOTAS:
-- - A migration já adicionou a coluna UserId na tabela Psicologos
-- - Este script vincula o psicólogo existente ao usuário do AspNetUsers
-- - Após executar, os prontuários serão filtrados corretamente por psicólogo
