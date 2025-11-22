# 🚀 Migração para PostgreSQL - Checklist

## Status Atual
- ✅ Código atualizado para suportar PostgreSQL (Npgsql.EntityFrameworkCore.PostgreSQL 9.0.0)
- ✅ Program.cs detecta automaticamente SQLite vs PostgreSQL
- ✅ Dockerfile com curl instalado para health checks
- 🔄 **BUILD EM ANDAMENTO**: clinicapsi:v2.0.0-postgres
- ⏳ RDS PostgreSQL precisa ser criado

## Próximos Passos

### 1. Criar RDS PostgreSQL (Manual - Console AWS)
👉 **Siga o guia detalhado em: `rds-setup.md`**

**Resumo:**
```
- Engine: PostgreSQL 16
- Instance: db.t3.micro (Free tier)
- DB name: clinicapsi
- Username: clinicapsi_admin
- Password: [CRIE UMA SENHA FORTE]
- VPC: vpc-046274331a2a956ad
- Subnets: subnet-082dc3d3367d6cb2e, subnet-095c4d5d4acf65848
- Public access: NO
- Security Group: Permitir porta 5432 do sg-0265151bb034d763f
```

⏱️ **Tempo estimado: 10-15 minutos**

### 2. Anotar Endpoint do RDS
Após criação, anote o **endpoint**:
```
clinicapsi-db.XXXXXXX.us-east-1.rds.amazonaws.com
```

### 3. Atualizar task-definition-postgres.json
Edite o arquivo e substitua:
```json
"value": "Host=SEU_ENDPOINT_AQUI;Port=5432;Database=clinicapsi;Username=clinicapsi_admin;Password=SUA_SENHA_AQUI;SSL Mode=Require;Trust Server Certificate=true"
```

### 4. Push da Nova Imagem
Após o build completar:
```powershell
# Tag para ECR
docker tag clinicapsi:v2.0.0-postgres 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:v2.0.0-postgres
docker tag clinicapsi:v2.0.0-postgres 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest

# Login ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 507363615495.dkr.ecr.us-east-1.amazonaws.com

# Push
docker push 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:v2.0.0-postgres
docker push 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest
```

### 5. Deploy no ECS
```powershell
# Registrar nova task definition
aws ecs register-task-definition --cli-input-json file://task-definition-postgres.json

# Atualizar serviço com 2 instâncias
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --force-new-deployment --desired-count 2
```

### 6. Verificar Logs
```powershell
# Acompanhar logs em tempo real
aws logs tail /ecs/clinicapsi --follow

# Procurar por:
# ✅ "Applying migration"
# ✅ "Creating default users" 
# ✅ "Seed completed"
```

### 7. Testar Aplicação
```
http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/
```

**Login Admin:**
- Email: `ana.santos@psii.com`
- Senha: `Ana123!`

## Benefícios da Migração

### 🎯 Problemas Resolvidos
- ❌ **ACABOU** corrupção de banco ("database disk image is malformed")
- ❌ **ACABOU** limitação de 1 instância
- ❌ **ACABOU** lentidão do SQLite em rede

### ✅ Vantagens do PostgreSQL
- ✅ **2+ instâncias** simultâneas sem problemas
- ✅ **Backups automáticos** (RDS gerenciado)
- ✅ **Alta disponibilidade** (Multi-AZ opcional)
- ✅ **Performance superior** em produção
- ✅ **Escalabilidade** fácil (vertical e horizontal)
- ✅ **Monitoramento** built-in (CloudWatch + RDS Insights)

## Arquivos Modificados

```
✏️  src/ClinicaPsi.Infrastructure/ClinicaPsi.Infrastructure.csproj
    - Adicionado: Npgsql.EntityFrameworkCore.PostgreSQL 9.0.0

✏️  src/ClinicaPsi.Web/Program.cs
    - Detecção automática SQLite vs PostgreSQL
    - UseNpgsql() quando connection string é PostgreSQL

✏️  Dockerfile
    - Adicionado curl para health checks

📄  task-definition-postgres.json (novo)
    - Template com connection string PostgreSQL

📄  rds-setup.md (novo)
    - Guia completo de criação do RDS

📄  MIGRATION-CHECKLIST.md (este arquivo)
    - Checklist e instruções
```

## Rollback (se necessário)

Se algo der errado, pode voltar para SQLite:
```powershell
# Usar task definition antiga
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --task-definition clinicapsi-task:4 --desired-count 1
```

## Custos

### Free Tier (12 meses)
- RDS db.t3.micro: **GRÁTIS** 750h/mês
- Storage 20GB: **GRÁTIS**
- Backups: **GRÁTIS** (igual ao storage)

### Após Free Tier
- RDS db.t3.micro: ~$15-20/mês
- Storage 20GB: ~$2/mês
- Total: **~$17-22/mês**

### Economia Opcional
Pode desligar EFS após migração: **-$3/mês**

## Suporte

Em caso de problemas:
1. Verifique logs: `aws logs tail /ecs/clinicapsi --follow`
2. Verifique security groups (porta 5432)
3. Verifique connection string está correta
4. Verifique RDS está "Available"
5. Teste conexão: `psql -h ENDPOINT -U clinicapsi_admin -d clinicapsi`

## Timeline Estimado

1. Criar RDS: **10-15 min**
2. Push imagem: **5 min**
3. Deploy ECS: **3-5 min**
4. Migrations: **1-2 min**
5. Testes: **5 min**

**⏱️ Total: ~25-30 minutos**

---

**Última atualização:** 22/10/2025
**Versão:** v2.0.0-postgres
