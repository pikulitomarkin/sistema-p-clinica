# 🚨 Como Liberar Elastic IP e Reduzir Custos AWS

## ❌ Recursos que NÃO consegui desativar (falta permissão)

### Elastic IP: 34.229.68.19
**Custo**: ~$3-4/mês (se não estiver associado a nenhuma instância)

---

## 📋 Passo a Passo para Liberar o Elastic IP

### Opção 1: Via Console AWS (Mais Fácil)

1. **Acesse o Console AWS**
   - Vá para: https://console.aws.amazon.com/ec2/

2. **Navegue até Elastic IPs**
   - No menu lateral esquerdo, procure por **"Network & Security"**
   - Clique em **"Elastic IPs"**

3. **Encontre o IP**
   - Procure por: **34.229.68.19**
   - Allocation ID: **eipalloc-081c57683c7d7e4a8**
   - Região: **us-east-1 (N. Virginia)**

4. **Verifique se está em uso**
   - Se a coluna **"Instance"** estiver vazia = NÃO ESTÁ SENDO USADO
   - Se tiver alguma instância = ESTÁ SENDO USADO (não libere!)

5. **Liberar o IP** (se não estiver em uso)
   - Selecione o Elastic IP (checkbox)
   - Clique em **"Actions"** (Ações)
   - Clique em **"Release Elastic IP address"**
   - Confirme clicando em **"Release"**

---

### Opção 2: Via AWS CLI (Mais Rápido)

Execute este comando com um usuário que tenha permissão:

```powershell
aws ec2 release-address --allocation-id eipalloc-081c57683c7d7e4a8 --region us-east-1
```

**Erro comum**: Se der erro de permissão, você precisa adicionar a policy `ec2:ReleaseAddress` ao seu usuário IAM.

---

## 🔍 Investigar Custos de R$1500

### O que já foi desativado:
✅ 2 EC2s extras deletados (economia ~$18-22/mês)
✅ ECS reduzido de 2 para 1 task (economia ~$15-20/mês)

### Custo atual esperado: ~$83-119 USD/mês = R$415-595/mês

### Se ainda está pagando R$1500 (~$300 USD), o problema pode ser:

---

## 🚨 PRINCIPAIS SUSPEITOS

### 1. **NAT Gateway - Tráfego de Dados** ⚠️ MAIS PROVÁVEL
- **Custo base**: ~$32/mês (sempre cobra)
- **Custo por tráfego**: ~$0.045 por GB processado
- **Se processar 2 TB/mês**: +$90 USD extras!
- **Se processar 4 TB/mês**: +$180 USD extras!

#### Como verificar:
1. Acesse: https://console.aws.amazon.com/vpc/
2. Clique em **"NAT Gateways"**
3. Anote o ID: **nat-05b9c15e0ce8b31b9**
4. Vá para CloudWatch para ver o tráfego:
   - https://console.aws.amazon.com/cloudwatch/
   - Métricas > VPC > NAT Gateway Metrics
   - Procure por **"BytesOutToSource"** e **"BytesOutToDestination"**

#### Solução se tráfego alto:
```powershell
# Deletar NAT Gateway (ATENÇÃO: site pode ficar offline!)
aws ec2 delete-nat-gateway --nat-gateway-id nat-05b9c15e0ce8b31b9 --region us-east-1
```

⚠️ **CUIDADO**: Deletar NAT Gateway pode fazer o site parar de funcionar!

---

### 2. **Snapshots de RDS Automáticos**
- RDS cria backups automáticos
- Cobram ~$0.095 por GB-mês
- Se tiver 200 GB de snapshots = ~$19/mês

#### Como verificar:
1. Acesse: https://console.aws.amazon.com/rds/
2. Clique em **"Snapshots"** no menu lateral
3. Veja quantos snapshots existem e o tamanho total

#### Solução:
- Delete snapshots antigos manualmente
- Configure retenção menor (ex: 7 dias ao invés de 30)

---

### 3. **CloudWatch Logs**
- Logs podem acumular MUITO espaço
- Cobram ~$0.50 por GB armazenado
- Cobram ~$0.50 por GB ingerido

#### Como verificar:
1. Acesse: https://console.aws.amazon.com/cloudwatch/
2. Clique em **"Logs"** > **"Log groups"**
3. Veja o tamanho de cada grupo de logs

#### Solução:
```powershell
# Listar log groups grandes
aws logs describe-log-groups --region us-east-1 --query 'logGroups[*].[logGroupName,storedBytes]' --output table

# Deletar logs antigos (exemplo)
aws logs delete-log-group --log-group-name "/ecs/clinicapsi-task" --region us-east-1
```

---

### 4. **Volumes EBS Órfãos**
- Volumes que sobraram de EC2s deletados
- Continuam cobrando mesmo sem uso

#### Como verificar:
```powershell
# Listar volumes disponíveis (não anexados)
aws ec2 describe-volumes --region us-east-1 --filters "Name=status,Values=available" --query 'Volumes[*].[VolumeId,Size,VolumeType]' --output table
```

#### Solução:
```powershell
# Deletar volume órfão (exemplo)
aws ec2 delete-volume --volume-id vol-XXXXXXXXX --region us-east-1
```

---

### 5. **Outros Serviços Escondidos**

#### S3 (Storage):
```powershell
# Ver quanto tem em cada bucket
aws s3 ls --summarize --human-readable --recursive s3://nome-bucket
```

#### Lambda:
```powershell
# Listar todas as funções Lambda
aws lambda list-functions --region us-east-1 --query 'Functions[*].[FunctionName,Runtime,LastModified]' --output table
```

#### CloudFront (CDN):
```powershell
# Listar distribuições CloudFront
aws cloudfront list-distributions --query 'DistributionList.Items[*].[Id,DomainName,Status]' --output table
```

---

## 💰 Como Ver Custos Reais no Console AWS

### Método 1: Cost Explorer
1. Acesse: https://console.aws.amazon.com/cost-management/home#/cost-explorer
2. Selecione período: **Último mês (outubro)**
3. Agrupe por: **Service** (Serviço)
4. Veja qual serviço está cobrando mais

### Método 2: Billing Dashboard
1. Acesse: https://console.aws.amazon.com/billing/home
2. Clique em **"Bills"** no menu lateral
3. Veja a fatura detalhada de outubro
4. Expanda cada serviço para ver detalhes

---

## ✅ Checklist de Ações Imediatas

### Já Feito:
- [x] Deletar EC2 t2.micro
- [x] Deletar EC2 t3.micro
- [x] Reduzir ECS de 2 para 1 task

### Fazer Manualmente:
- [ ] Liberar Elastic IP (34.229.68.19)
- [ ] Verificar tráfego do NAT Gateway no CloudWatch
- [ ] Verificar snapshots de RDS
- [ ] Verificar CloudWatch Logs grandes
- [ ] Verificar volumes EBS órfãos
- [ ] Ver Cost Explorer para identificar o vilão

---

## 🎯 Redução de Custos Drástica

Se quiser **ZERAR os custos** quando não estiver usando:

```powershell
# Parar ECS (tasks vão para 0)
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --desired-count 0 --region us-east-1

# Parar RDS (banco fica offline)
aws rds stop-db-instance --db-instance-identifier clinicapsi-db --region us-east-1

# Deletar NAT Gateway (CUIDADO!)
aws ec2 delete-nat-gateway --nat-gateway-id nat-05b9c15e0ce8b31b9 --region us-east-1
```

⚠️ **ATENÇÃO**: Isso deixa o site OFFLINE!

### Para ligar novamente:
```powershell
# Ligar RDS
aws rds start-db-instance --db-instance-identifier clinicapsi-db --region us-east-1

# Ligar ECS
aws ecs update-service --cluster clinicapsi-cluster --service clinicapsi-service --desired-count 1 --region us-east-1
```

---

## 📞 Próximos Passos

1. **Libere o Elastic IP** agora (ganho imediato de $3-4/mês)
2. **Acesse o Cost Explorer** e veja qual serviço está cobrando R$1500
3. **Me avise** qual serviço está com custo alto que eu te ajudo a resolver
4. **Verifique o NAT Gateway** no CloudWatch (maior suspeito!)

---

**Criado em**: 01/11/2025
**Economia já aplicada**: ~$33-42 USD/mês = R$165-210/mês
**Custo atual estimado**: ~$83-119 USD/mês = R$415-595/mês
**Meta**: Descobrir por que está em R$1500 ao invés de R$415-595
