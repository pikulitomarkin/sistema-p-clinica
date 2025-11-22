# 💰 Análise de Custos AWS - PsiiAnaSantos

## 📊 Custos Mensais Estimados

### ⚠️ RECURSOS CAROS (Total: ~$61-85/mês)

#### 1. **AWS Fargate** - ~$30-40/mês ⚠️ MAIOR CUSTO
- **Atual**: 2 tasks rodando 24/7
- **Configuração**: 512 CPU / 1024 MB RAM por task
- **Custo**: 
  - Por task: ~$15-20/mês
  - Total (2 tasks): ~$30-40/mês
- **Por que custa**: Cobra por CPU e memória usados por hora

#### 2. **Application Load Balancer (ALB)** - ~$16-20/mês ⚠️
- **O que faz**: Distribui tráfego entre as tasks
- **Custo fixo**: ~$16/mês + tráfego
- **Por que custa**: Cobra por hora + processamento de dados

#### 3. **RDS PostgreSQL** - ~$15-25/mês ⚠️
- **Tipo**: db.t4g.micro (menor instância)
- **Storage**: 20 GB
- **Multi-AZ**: Não (single-AZ)
- **Custo**: ~$15-25/mês
- **Por que custa**: Instância rodando 24/7

---

### ✅ RECURSOS BARATOS (Total: ~$2-5/mês)

#### 4. **EFS (Elastic File System)** - ~$1-3/mês ✓
- **Uso**: Compartilha chaves de criptografia entre tasks
- **Tamanho atual**: ~0 GB (quase vazio)
- **Custo**: ~$0.30/GB por mês

#### 5. **ECR (Container Registry)** - ~$1-2/mês ✓
- **Uso**: Armazena imagens Docker
- **Custo**: ~$0.10/GB por mês

#### 6. **VPC, Subnets, Security Groups** - $0/mês ✓
- **Free Tier**: Não cobra

---

## 💵 **CUSTO TOTAL MENSAL: ~$63-90 USD**

---

## 🔧 Como REDUZIR Custos

### Opção 1: Reduzir para 1 Task (Economia: ~$15-20/mês)
**Novo custo: ~$48-70/mês**

```powershell
# Reduzir de 2 para 1 task
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --desired-count 1 --region us-east-1
```

**Prós:**
- ✅ Economia imediata de 25-30%
- ✅ Sistema continua rodando 24/7

**Contras:**
- ❌ Menos disponibilidade (se 1 task cair, site fica offline até subir outra)
- ❌ Menos performance (1 task processa todas requisições)

---

### Opção 2: Parar Quando Não Usar (Economia: até 100% enquanto parado)
**Custo: $0 quando parado + custos normais quando ligado**

#### Parar Tudo:
```powershell
# Parar ECS tasks
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --desired-count 0 --region us-east-1

# Parar banco de dados (RDS para automaticamente após 7 dias)
aws rds stop-db-instance --db-instance-identifier clinicapsi-db --region us-east-1
```

#### Ligar Novamente:
```powershell
# Ligar ECS tasks
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --desired-count 2 --region us-east-1

# Ligar banco de dados
aws rds start-db-instance --db-instance-identifier clinicapsi-db --region us-east-1
```

**Prós:**
- ✅ Economia total de Fargate e RDS enquanto parado
- ✅ ALB continua cobrando (mas pouco: ~$16/mês)
- ✅ Bom para desenvolvimento/testes

**Contras:**
- ❌ Site fica offline quando parado
- ❌ Precisa ligar manualmente quando precisar usar
- ❌ RDS reinicia automaticamente após 7 dias

---

### Opção 3: Deletar Tudo (Economia: 100%)
**Custo: $0**

⚠️ **CUIDADO**: Isso apaga TUDO (código, banco de dados, configurações)

Só faça isso se não precisar mais do sistema ou tiver backup completo!

---

### Opção 4: Migrar para Alternativas Mais Baratas

#### 4A: AWS Lightsail (Economia: ~40-60%)
- **Custo**: $10-20/mês (tudo incluído)
- **O que é**: VPS simples (como DigitalOcean)
- **Prós**: Muito mais barato, preço fixo
- **Contras**: Menos escalável, precisa gerenciar servidor

#### 4B: Render.com / Railway.app (Free Tier + Pagos)
- **Custo**: 
  - Free tier: $0 (com limitações)
  - Paid: $7-15/mês
- **Prós**: Muito simples de usar, CI/CD automático
- **Contras**: Menos controle

---

## 📋 Minha Recomendação

### Para DESENVOLVIMENTO/TESTES:
✅ **Opção 2** - Parar quando não usar
- Liga quando for trabalhar
- Desliga no final do dia
- Economia de ~50-70%

### Para PRODUÇÃO (site precisa ficar online):
✅ **Opção 1** - Reduzir para 1 task
- Site fica sempre online
- Economia de ~25%
- Custo: ~$48-70/mês

### Para ECONOMIZAR MUITO:
✅ **Opção 4A** - Migrar para Lightsail
- Site sempre online
- Custo fixo: $10-20/mês
- Economia de ~60-80%

---

## 🎯 Ação Imediata para Reduzir 25%

Execute agora para reduzir custos imediatamente:

```powershell
# Reduzir de 2 para 1 task
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --desired-count 1 --region us-east-1

# Verificar
aws ecs describe-services --cluster clinicapsi-cluster --services clinicapsi-service --region us-east-1 --query 'services[0].[desiredCount,runningCount]' --output table
```

**Economia imediata**: ~$15-20/mês

---

## 📞 Precisa de Mais Ajuda?

- Posso ajudar a implementar qualquer dessas opções
- Posso criar scripts para ligar/desligar automaticamente
- Posso ajudar a migrar para alternativa mais barata

---

## ✅ AÇÕES JÁ EXECUTADAS (01/11/2025)

### Recursos Desativados:
- ✅ **EC2 t2.micro** (i-0bf005a101527f9ce) - DELETADO
  - Economia: ~$8-10/mês
- ✅ **EC2 t3.micro** (i-06cac84f1798c59d3) - DELETADO
  - Economia: ~$10-12/mês
- ✅ **ECS Fargate**: Reduzido de 2 para 1 task
  - Economia: ~$15-20/mês

### Economia Total: ~$33-42 USD/mês = R$165-210/mês

### ⚠️ Recursos que PRECISAM ser liberados manualmente:
- ❌ **Elastic IP** (34.229.68.19) - ~$3-4/mês
  - Allocation ID: eipalloc-081c57683c7d7e4a8
  - **Ver arquivo**: `COMO-LIBERAR-ELASTIC-IP.md`

### Custos Atuais (após redução):
- ECS Fargate (1 task): ~$15-20/mês
- RDS PostgreSQL: ~$15-25/mês
- ALB: ~$16-20/mês
- NAT Gateway: ~$32-45/mês ⚠️
- EFS + ECR: ~$2-5/mês
- Elastic IP não usado: ~$3-4/mês

**Total**: ~$83-119 USD/mês = R$415-595/mês

### 🚨 Se a fatura for R$1500, o problema é:
1. **Tráfego de dados pelo NAT Gateway** (~$0.045/GB)
   - 2 TB/mês = +$90 USD extras
   - 4 TB/mês = +$180 USD extras
2. **Snapshots de RDS acumulados**
3. **CloudWatch Logs grandes**
4. **Outros serviços não detectados**

**Ver instruções completas**: `COMO-LIBERAR-ELASTIC-IP.md`

---

**Última atualização**: 01/11/2025 21:35
**Versão atual**: v3.6.2
**Tasks rodando**: 1 x Fargate (512 CPU / 1024 MB)
**Site**: http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
