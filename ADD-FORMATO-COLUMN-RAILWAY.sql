-- ============================================
-- Script para adicionar coluna Formato
-- Execute este SQL no Railway PostgreSQL Console
-- ============================================

-- 1. Adicionar a coluna Formato com valor padrão
ALTER TABLE "Consultas" 
ADD COLUMN "Formato" integer NOT NULL DEFAULT 1;

-- 2. Verificar se a coluna foi criada
SELECT column_name, data_type, column_default 
FROM information_schema.columns 
WHERE table_name = 'Consultas' AND column_name = 'Formato';

-- 3. Atualizar consultas existentes (opcional, já tem default)
-- UPDATE "Consultas" SET "Formato" = 1 WHERE "Formato" IS NULL;

-- 4. Verificar dados
SELECT "Id", "Formato", "DataHorario" 
FROM "Consultas" 
ORDER BY "DataHorario" DESC 
LIMIT 5;

-- ============================================
-- RESULTADO ESPERADO:
-- - A coluna Formato deve aparecer no information_schema
-- - Todas as consultas devem ter Formato = 1
-- ============================================
