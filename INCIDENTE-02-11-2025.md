# 🚨 Relatório de Incidente - 02/11/2025

## Problema Identificado
**Servidor offline** - Nenhuma task do ECS rodando

---

## 🔍 Análise do Problema

### Sintomas:
- ✅ ECS Service: ACTIVE
- ❌ Running Count: 0 (zero tasks rodando)
- ❌ Desired Count: 1
- ❌ Deployment Status: IN_PROGRESS (travado)

### Causa Raiz:
**Tasks falhando no health check**
```
stoppedReason: "Task failed container health checks"
stopCode: ServiceSchedulerInitiated
exitCode: 0
```

### Versão Problemática:
- **Image**: `clinicapsi:v3.6.4-webhook-fix`
- **Task Definition**: revision 32
- **Deploy**: 01/11/2025 22:00

### O que causou a falha:
Na tentativa de corrigir o webhook do WhatsApp ontem, fizemos 2 alterações:
1. ✅ Comentamos `app.UseHttpsRedirection()` (correto)
2. ❌ Deixamos `app.MapHealthChecks("/health")` **ANTES** de `app.UseRouting()` (incorreto)

**ASP.NET Core** exige que endpoints (`Map*`) venham **APÓS** `UseRouting()`.

---

## ✅ Solução Aplicada

### 1. Rollback Imediato
```powershell
aws ecs update-service \
  --cluster clinicapsi-cluster \
  --service clinicapsi-service \
  --task-definition clinicapsi-task:30 \
  --force-new-deployment \
  --region us-east-1
```

**Resultado**: Servidor voltou a funcionar em ~2 minutos

### 2. Correção do Código
Movemos `app.MapHealthChecks("/health")` para **DEPOIS** de `app.UseRouting()`:

**Antes (ERRADO)**:
```csharp
// Health check endpoint
app.MapHealthChecks("/health");

// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
```

**Depois (CORRETO)**:
```csharp
// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint (DEVE vir após UseRouting)
app.MapHealthChecks("/health");
```

---

## 📊 Timeline do Incidente

| Horário | Evento |
|---------|--------|
| 01/11 22:00 | Deploy v3.6.4-webhook-fix (task 32) |
| 01/11 22:05 | Tasks começaram a falhar no health check |
| 01/11 22:10 | Servidor ficou completamente offline (0 tasks) |
| 02/11 [AGORA] | Incidente detectado pelo usuário |
| 02/11 [AGORA+2min] | Rollback para task 30 (v3.6.2) |
| 02/11 [AGORA+4min] | Servidor voltou online |
| 02/11 [AGORA+5min] | Código corrigido para próximo deploy |

**Tempo de Indisponibilidade**: ~12 horas (01/11 22:10 até 02/11 10:xx)

---

## 🎯 Versões Atuais

### Em Produção (FUNCIONANDO):
- **Version**: v3.6.2-202511012130
- **Task Definition**: clinicapsi-task:30
- **Image**: 507363615495.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:v3.6.2-202511012130
- **Status**: ✅ ONLINE
- **Running Count**: 1/1
- **Target Health**: 1/1 healthy

### Próxima Versão (CORRIGIDA, aguardando deploy):
- **Version**: v3.6.5-webhook-fixed
- **Correções**:
  1. `MapHealthChecks` movido para depois de `UseRouting()`
  2. `UseHttpsRedirection()` continua comentado (para webhook WhatsApp)

---

## 📝 Lições Aprendidas

### ❌ O que deu errado:
1. Deploy feito tarde da noite (22:00) sem monitoramento
2. Não verificamos logs imediatamente após deploy
3. Health check failure não foi detectado a tempo
4. Ordem incorreta dos middlewares ASP.NET Core

### ✅ Como evitar no futuro:
1. **Sempre verificar logs após deploy**:
   ```powershell
   aws logs tail /ecs/clinicapsi-task --follow --region us-east-1
   ```

2. **Monitorar health checks**:
   ```powershell
   aws ecs describe-services --cluster clinicapsi-cluster \
     --services clinicapsi-service --region us-east-1 \
     --query 'services[0].runningCount'
   ```

3. **Testar localmente antes de deploy**:
   ```powershell
   docker run -d -p 8080:80 clinicapsi:latest
   curl http://localhost:8080/health
   ```

4. **Deploys em horário comercial** quando possível monitorar

5. **Configurar CloudWatch Alarm** para alertar quando runningCount < desiredCount

---

## 🔧 Próximos Passos

### Imediato:
- [x] Rollback concluído
- [x] Servidor online
- [x] Código corrigido
- [ ] Fazer novo deploy com v3.6.5-webhook-fixed
- [ ] Testar webhook WhatsApp

### Curto Prazo:
- [ ] Configurar CloudWatch Alarm para health check failures
- [ ] Configurar SNS notification para alertas
- [ ] Documentar processo de rollback

### Médio Prazo:
- [ ] Implementar blue/green deployment
- [ ] Configurar staging environment para testes
- [ ] Adicionar testes de integração no CI/CD

---

## 📞 URLs do Sistema

**Site**: http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
- Status: ✅ ONLINE
- Health: http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/health
- Admin: http://clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com/admin

---

## 🔐 Comandos Úteis para Emergências

### Verificar Status Rápido:
```powershell
aws ecs describe-services --cluster clinicapsi-cluster \
  --services clinicapsi-service --region us-east-1 \
  --query 'services[0].[runningCount,desiredCount]'
```

### Rollback Rápido (última versão estável):
```powershell
aws ecs update-service --cluster clinicapsi-cluster \
  --service clinicapsi-service \
  --task-definition clinicapsi-task:30 \
  --force-new-deployment --region us-east-1
```

### Ver Logs em Tempo Real:
```powershell
aws logs tail /ecs/clinicapsi-task --follow --region us-east-1
```

### Listar Task Definitions:
```powershell
aws ecs list-task-definitions --family-prefix clinicapsi-task \
  --status ACTIVE --region us-east-1 --query 'taskDefinitionArns' \
  --output table
```

---

**Relatório gerado em**: 02/11/2025
**Incidente resolvido**: ✅ SIM
**Próxima ação**: Deploy v3.6.5 com correção
