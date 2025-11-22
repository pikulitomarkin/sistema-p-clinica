# Setup PostgreSQL RDS para ClinicaPsi

## 1. Criar RDS via Console AWS

1. Acesse: https://console.aws.amazon.com/rds/
2. Clique em "Create database"
3. Configure:

### Engine Options
- **Engine type**: PostgreSQL
- **Engine Version**: PostgreSQL 16.x (latest)

### Templates
- **Template**: Free tier (ou Production se necessário)

### Settings
- **DB instance identifier**: `clinicapsi-db`
- **Master username**: `clinicapsi_admin`
- **Master password**: (gere uma senha forte e anote!)
- **Confirm password**: (repita a senha)

### Instance Configuration
- **DB instance class**: db.t3.micro (Free tier) ou db.t4g.micro
- **Storage type**: General Purpose SSD (gp3)
- **Allocated storage**: 20 GB

### Connectivity
- **VPC**: `vpc-046274331a2a956ad`
- **Subnet group**: Criar novo ou usar existente
  - Subnets: `subnet-082dc3d3367d6cb2e`, `subnet-095c4d5d4acf65848`
- **Public access**: **No**
- **VPC security group**: Criar novo "clinicapsi-rds-sg"
  - Inbound rule: PostgreSQL (5432) from `sg-0265151bb034d763f` (ECS tasks)

### Database Options
- **Initial database name**: `clinicapsi`
- **Port**: 5432

### Backup
- **Backup retention period**: 7 days
- **Enable encryption**: Yes (default KMS key)

### Monitoring
- **Enable Enhanced Monitoring**: Opcional (recomendado)

### Maintenance
- **Enable auto minor version upgrade**: Yes

4. Clique em "Create database"
5. **Aguarde 5-10 minutos** para o RDS ficar disponível

## 2. Anotar Informações do RDS

Após criação, anote:
- **Endpoint**: `clinicapsi-db.xxxxx.us-east-1.rds.amazonaws.com`
- **Port**: `5432`
- **Database name**: `clinicapsi`
- **Username**: `clinicapsi_admin`
- **Password**: (a senha que você criou)

## 3. Connection String

Formato da connection string PostgreSQL:

```
Host=clinicapsi-db.xxxxx.us-east-1.rds.amazonaws.com;Port=5432;Database=clinicapsi;Username=clinicapsi_admin;Password=SUA_SENHA_AQUI;SSL Mode=Require;Trust Server Certificate=true
```

Exemplo completo:
```
Host=clinicapsi-db.c9akr7fhfjvs.us-east-1.rds.amazonaws.com;Port=5432;Database=clinicapsi;Username=clinicapsi_admin;Password=Clinic@2025!Strong;SSL Mode=Require;Trust Server Certificate=true
```

## 4. Atualizar Task Definition

Edite o arquivo `task-definition.json` e substitua a variável de ambiente:

```json
{
  "name": "ConnectionStrings__DefaultConnection",
  "value": "Host=SEU_ENDPOINT_RDS;Port=5432;Database=clinicapsi;Username=clinicapsi_admin;Password=SUA_SENHA;SSL Mode=Require;Trust Server Certificate=true"
}
```

## 5. Comandos para Deploy

```powershell
# 1. Restaurar pacotes (incluindo Npgsql)
cd src\ClinicaPsi.Web
dotnet restore

# 2. Build da imagem
cd ..\..
docker build -t clinicapsi:v2.0.0-postgres -f src/ClinicaPsi.Web/Dockerfile .

# 3. Tag para ECR
docker tag clinicapsi:v2.0.0-postgres 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:v2.0.0-postgres
docker tag clinicapsi:v2.0.0-postgres 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest

# 4. Login ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 507363615495.dkr.ecr.us-east-1.amazonaws.com

# 5. Push para ECR
docker push 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:v2.0.0-postgres
docker push 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest

# 6. Atualizar task definition
aws ecs register-task-definition --cli-input-json file://task-definition.json

# 7. Atualizar serviço (força novo deployment)
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --force-new-deployment --desired-count 2
```

## 6. Security Group do RDS

Se precisar criar/atualizar o security group do RDS manualmente:

```powershell
# Obter security group do ECS
$ECS_SG = "sg-0265151bb034d763f"

# Obter security group do RDS (após criação)
$RDS_SG = (aws rds describe-db-instances --db-instance-identifier clinicapsi-db --query "DBInstances[0].VpcSecurityGroups[0].VpcSecurityGroupId" --output text)

# Adicionar regra inbound no RDS SG permitindo ECS
aws ec2 authorize-security-group-ingress --group-id $RDS_SG --protocol tcp --port 5432 --source-group $ECS_SG
```

## 7. Verificar Migrations

Após o deploy, as migrations serão aplicadas automaticamente no PostgreSQL:

```powershell
# Ver logs da aplicação
aws logs tail /ecs/clinicapsi --follow

# Procurar por:
# - "Applying migration"
# - "Database migration completed"
# - "Creating default users"
```

## 8. Vantagens do PostgreSQL

✅ **Não corrompe** com múltiplas instâncias
✅ **Mais rápido** que SQLite em rede
✅ **Suporta concorrência** real
✅ **Backups automáticos** pelo RDS
✅ **Alta disponibilidade** com Multi-AZ (opcional)
✅ **Escalabilidade** vertical fácil

## 9. Custos Estimados (Free Tier)

- **db.t3.micro**: Grátis 750h/mês (1 ano)
- **Storage 20GB**: Grátis 20GB (1 ano)
- **Backup 20GB**: Grátis igual ao storage
- **Após Free Tier**: ~$15-20/mês

## 10. Remover EFS (Opcional)

Após migrar para PostgreSQL, o EFS não é mais necessário (exceto se quiser manter Data Protection keys):

```powershell
# Listar mount targets do EFS
aws efs describe-mount-targets --file-system-id fs-059bb5b59fe64d817

# Deletar mount targets (um por vez)
aws efs delete-mount-target --mount-target-id fsmt-xxxxx

# Deletar EFS (após deletar todos os mount targets)
aws efs delete-file-system --file-system-id fs-059bb5b59fe64d817
```

## Troubleshooting

### Erro de conexão
- Verifique security group do RDS permite porta 5432 do ECS
- Verifique se RDS está no mesmo VPC que ECS
- Verifique se connection string está correta

### Migration falha
- Verifique se database existe: `clinicapsi`
- Verifique se usuário tem permissões de CREATE TABLE
- Verifique logs: `aws logs tail /ecs/clinicapsi --follow`

### Performance
- Considere aumentar para db.t3.small se necessário
- Configure connection pooling (já está configurado no EF Core)
- Ative Performance Insights no RDS para monitorar
