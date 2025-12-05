-- Script para adicionar coluna Formato à tabela Consultas
-- Execute este script no banco de dados PostgreSQL do Railway

-- Adicionar coluna Formato com valor padrão 1 (Presencial)
ALTER TABLE "Consultas" 
ADD COLUMN IF NOT EXISTS "Formato" integer NOT NULL DEFAULT 1;

-- Verificar se a coluna foi criada
SELECT column_name, data_type, column_default 
FROM information_schema.columns 
WHERE table_name = 'Consultas' AND column_name = 'Formato';

-- Atualizar todas as consultas existentes para formato Presencial (caso a coluna já exista sem valor)
UPDATE "Consultas" 
SET "Formato" = 1 
WHERE "Formato" IS NULL OR "Formato" = 0;

COMMIT;
