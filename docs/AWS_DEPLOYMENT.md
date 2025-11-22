# AWS Elastic Beanstalk Deployment Guide

## 📋 Pré-requisitos

- AWS CLI instalado e configurado
- EB CLI instalado: `pip install awsebcli`
- Conta AWS ativa
- Docker instalado (para testes locais)

## 🚀 Deploy para AWS Elastic Beanstalk

### 1. Inicializar Elastic Beanstalk

```bash
# No diretório raiz do projeto
eb init -p docker clinicapsi --region us-east-1
```

### 2. Criar ambiente

```bash
# Criar ambiente de produção
eb create clinicapsi-prod --database --database.engine postgres

# Ou sem banco RDS (usar SQLite)
eb create clinicapsi-prod
```

### 3. Configurar variáveis de ambiente

```bash
# Configurar variáveis via CLI
eb setenv \
  ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__DefaultConnection="Data Source=/app/data/clinicapsi.db" \
  WhatsApp__AccessToken="seu_token" \
  WhatsApp__PhoneNumberId="seu_phone_id" \
  WhatsApp__BusinessAccountId="seu_business_id"
```

### 4. Deploy da aplicação

```bash
# Deploy
eb deploy

# Abrir aplicação no navegador
eb open

# Ver logs
eb logs
```

## 🗄️ Opção: Usar RDS PostgreSQL (Recomendado para Produção)

### 1. Criar banco RDS

```bash
# Via CLI
aws rds create-db-instance \
  --db-instance-identifier clinicapsi-db \
  --db-instance-class db.t3.micro \
  --engine postgres \
  --master-username admin \
  --master-user-password SUA_SENHA_SEGURA \
  --allocated-storage 20
```

### 2. Atualizar connection string

```bash
eb setenv ConnectionStrings__DefaultConnection="Host=seu-rds-endpoint;Database=clinicapsi;Username=admin;Password=SUA_SENHA"
```

### 3. Atualizar ClinicaPsi.Infrastructure

Adicionar pacote PostgreSQL:

```bash
cd src/ClinicaPsi.Infrastructure
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Atualizar `Program.cs`:

```csharp
// Detectar provider baseado na connection string
if (connectionString.Contains("Host=") || connectionString.Contains("Server="))
{
    // PostgreSQL para produção
    builder.Services.AddDbContext<AppDbContext>(options => 
        options.UseNpgsql(connectionString));
}
else
{
    // SQLite para desenvolvimento
    builder.Services.AddDbContext<AppDbContext>(options => 
        options.UseSqlite(connectionString));
}
```

## 🐳 Deploy com ECS (Elastic Container Service)

### 1. Criar repositório ECR

```bash
# Criar repositório
aws ecr create-repository --repository-name clinicapsi

# Login no ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin SEU_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com
```

### 2. Build e push da imagem

```bash
# Build
docker build -t clinicapsi .

# Tag
docker tag clinicapsi:latest SEU_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest

# Push
docker push SEU_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest
```

### 3. Criar cluster ECS

Via Console AWS ou CLI:

```bash
aws ecs create-cluster --cluster-name clinicapsi-cluster
```

### 4. Criar Task Definition

Criar arquivo `task-definition.json`:

```json
{
  "family": "clinicapsi",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "256",
  "memory": "512",
  "containerDefinitions": [
    {
      "name": "clinicapsi-app",
      "image": "SEU_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest",
      "portMappings": [
        {
          "containerPort": 80,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__DefaultConnection",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:ACCOUNT_ID:secret:clinicapsi/db"
        },
        {
          "name": "WhatsApp__AccessToken",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:ACCOUNT_ID:secret:clinicapsi/whatsapp"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/clinicapsi",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "ecs"
        }
      }
    }
  ]
}
```

Registrar task:

```bash
aws ecs register-task-definition --cli-input-json file://task-definition.json
```

### 5. Criar serviço ECS

```bash
aws ecs create-service \
  --cluster clinicapsi-cluster \
  --service-name clinicapsi-service \
  --task-definition clinicapsi \
  --desired-count 1 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[subnet-xxx],securityGroups=[sg-xxx],assignPublicIp=ENABLED}"
```

## 🔐 AWS Secrets Manager

### Armazenar secrets

```bash
# Connection string
aws secretsmanager create-secret \
  --name clinicapsi/db \
  --secret-string "Data Source=/app/data/clinicapsi.db"

# WhatsApp credentials
aws secretsmanager create-secret \
  --name clinicapsi/whatsapp \
  --secret-string '{"AccessToken":"xxx","PhoneNumberId":"xxx","BusinessAccountId":"xxx"}'
```

### Atualizar Program.cs para ler do Secrets Manager

```bash
dotnet add package AWSSDK.SecretsManager
```

## 📊 CloudWatch Logs

### Configurar logs

```bash
# Criar log group
aws logs create-log-group --log-group-name /aws/clinicapsi
```

### Adicionar Serilog para melhor logging

```bash
cd src/ClinicaPsi.Web
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.CloudWatch
```

## 🌐 Load Balancer & Auto Scaling

### Application Load Balancer

```bash
# Criar ALB
aws elbv2 create-load-balancer \
  --name clinicapsi-alb \
  --subnets subnet-xxx subnet-yyy \
  --security-groups sg-xxx \
  --scheme internet-facing \
  --type application
```

### Auto Scaling

Configurar via Console ou CLI para escalar baseado em:
- CPU > 70%
- Memory > 80%
- Request count

## 💰 Estimativa de Custos (AWS)

### Opção 1: Elastic Beanstalk + SQLite
- EC2 t3.micro (750h free tier): ~$8.50/mês
- EBS 20GB: ~$2/mês
- **Total: ~$10/mês**

### Opção 2: ECS Fargate + RDS
- Fargate (0.25 vCPU, 0.5GB): ~$15/mês
- RDS db.t3.micro PostgreSQL: ~$15/mês
- ALB: ~$16/mês
- **Total: ~$46/mês**

### Opção 3: App Runner (Mais simples)
- App Runner: ~$25/mês
- RDS: ~$15/mês
- **Total: ~$40/mês**

## 🛠️ Comandos Úteis

```bash
# EB CLI
eb status                    # Ver status
eb logs                      # Ver logs
eb ssh                       # SSH no servidor
eb health                    # Health check
eb config                    # Editar configuração
eb terminate                 # Destruir ambiente

# Docker
docker-compose up -d         # Subir local
docker-compose logs -f       # Ver logs
docker-compose down          # Parar

# ECS
aws ecs list-clusters        # Listar clusters
aws ecs list-services        # Listar serviços
aws ecs describe-services    # Detalhes do serviço
```

## ✅ Checklist de Deploy

- [ ] Variáveis de ambiente configuradas
- [ ] Secrets no Secrets Manager
- [ ] Banco de dados criado (RDS ou volume)
- [ ] Security groups configurados
- [ ] Load balancer configurado
- [ ] SSL/TLS certificate (ACM)
- [ ] Domain configurado (Route 53)
- [ ] CloudWatch logs habilitado
- [ ] Health checks funcionando
- [ ] Backup strategy definida
- [ ] Monitoring/alertas configurados

## 🔒 Segurança

1. **IAM Roles**: Criar role com permissões mínimas
2. **Security Groups**: Abrir apenas portas necessárias
3. **HTTPS**: Usar ACM certificate
4. **Secrets**: Nunca commitar secrets no código
5. **WAF**: Considerar AWS WAF para proteção
6. **Backup**: Habilitar backup automático do RDS

## 📚 Recursos

- [AWS Elastic Beanstalk Docs](https://docs.aws.amazon.com/elasticbeanstalk/)
- [AWS ECS Docs](https://docs.aws.amazon.com/ecs/)
- [AWS App Runner](https://docs.aws.amazon.com/apprunner/)
- [.NET on AWS](https://aws.amazon.com/developer/language/net/)
