# 🚀 Guia Rápido - Configurar Domínio no Hostgator

## 📍 Você está aqui
- ✅ Aplicação rodando na AWS
- ✅ Load Balancer funcionando
- ⏳ **Agora**: Configurar domínio

---

## 🎯 Configuração Simples (10 minutos)

### Qual é seu domínio?
Exemplo: `clinicapsi.com.br` ou `meusistema.com.br`

---

## 📝 PASSO A PASSO

### 1️⃣ Acessar cPanel Hostgator
1. Faça login em: https://hostgator.com.br/cpanel
2. Procure **"Zone Editor"** ou **"Editor de Zona"**
3. Selecione seu domínio

### 2️⃣ Adicionar Registro para WWW

**Criar CNAME Record:**
```
Type: CNAME
Name: www
Points to: clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
TTL: 14400 (ou deixe padrão)
```

Clique em **"Add Record"** ou **"Adicionar Registro"**

### 3️⃣ Configurar Domínio Raiz (sem www)

**Opção A - Redirect (Recomendado):**
1. No cPanel, procure **"Redirects"** ou **"Redirecionamentos"**
2. Configure:
   ```
   Type: Permanent (301)
   From: seudominio.com.br
   To: https://www.seudominio.com.br
   ```

**Opção B - A Record (Alternativa):**
```
Type: A
Name: @ (ou deixe em branco)
Points to: [IP obtido com nslookup do ALB]
TTL: 14400
```

### 4️⃣ Aguardar Propagação
⏱️ **Tempo**: 1-6 horas (geralmente)
🔍 **Verificar**: https://dnschecker.org

### 5️⃣ Testar
Abra no navegador:
```
http://www.seudominio.com.br
```

✅ **Funcionou?** Continue para HTTPS!

---

## 🔒 CONFIGURAR HTTPS/SSL

### Script Automático (Recomendado)

Execute o script PowerShell:
```powershell
cd C:\Users\Admin\sistema-p-clinica-clean
.\configure-ssl.ps1
```

O script vai:
1. ✅ Solicitar certificado SSL grátis (AWS ACM)
2. ✅ Fornecer registros CNAME para validação
3. ✅ Aguardar validação automática
4. ✅ Configurar HTTPS no ALB
5. ✅ Redirecionar HTTP → HTTPS

### Manual (Passo a Passo)

#### 1. Solicitar Certificado
```powershell
aws acm request-certificate `
    --domain-name seudominio.com.br `
    --subject-alternative-names www.seudominio.com.br `
    --validation-method DNS `
    --region us-east-1
```

Anote o **Certificate ARN** que aparecer.

#### 2. Ver Registros de Validação
```powershell
aws acm describe-certificate `
    --certificate-arn SEU_ARN_AQUI `
    --region us-east-1
```

Adicione os registros CNAME no Hostgator (Zone Editor).

#### 3. Aguardar Validação
```powershell
# Verificar status (deve mostrar "ISSUED")
aws acm describe-certificate `
    --certificate-arn SEU_ARN_AQUI `
    --region us-east-1 `
    --query "Certificate.Status"
```

#### 4. Adicionar ao ALB
```powershell
# Obter ARN do ALB
$ALB_ARN = (aws elbv2 describe-load-balancers --names clinicapsi-alb --query "LoadBalancers[0].LoadBalancerArn" --output text)

# Criar listener HTTPS
aws elbv2 create-listener `
    --load-balancer-arn $ALB_ARN `
    --protocol HTTPS `
    --port 443 `
    --certificates CertificateArn=SEU_CERTIFICATE_ARN `
    --default-actions Type=forward,TargetGroupArn=arn:aws:elasticloadbalancing:us-east-1:507363615495:targetgroup/clinicapsi-tg/f84f061a24c7ec0f
```

#### 5. Redirecionar HTTP → HTTPS
```powershell
# Obter listener HTTP
$HTTP_LISTENER = (aws elbv2 describe-listeners --load-balancer-arn $ALB_ARN --query "Listeners[?Port==``80``].ListenerArn" --output text)

# Modificar para redirect
aws elbv2 modify-listener `
    --listener-arn $HTTP_LISTENER `
    --default-actions '[{"Type":"redirect","RedirectConfig":{"Protocol":"HTTPS","Port":"443","StatusCode":"HTTP_301"}}]'
```

---

## ✅ Checklist Final

- [ ] CNAME para www criado no Hostgator
- [ ] Redirect ou A record para domínio raiz configurado
- [ ] DNS propagado (testado em dnschecker.org)
- [ ] Site carrega em http://www.seudominio.com.br
- [ ] Certificado SSL solicitado (ACM)
- [ ] Registros CNAME de validação adicionados
- [ ] Certificado validado (status: ISSUED)
- [ ] Listener HTTPS criado no ALB (porta 443)
- [ ] Redirect HTTP → HTTPS configurado
- [ ] Site carrega em https://www.seudominio.com.br
- [ ] Cadeado verde aparece no navegador 🔒

---

## 🧪 Testar Configuração

### Verificar DNS
```powershell
nslookup www.seudominio.com.br
# Deve retornar: clinicapsi-alb-1064760770.us-east-1.elb.amazonaws.com
```

### Verificar HTTPS
```powershell
curl -I https://www.seudominio.com.br
# Deve retornar: HTTP/2 200
```

### Verificar Redirect
```powershell
curl -I http://www.seudominio.com.br
# Deve retornar: HTTP/1.1 301 Moved Permanently
# Location: https://www.seudominio.com.br
```

---

## 🚨 Problemas Comuns

### "Site não carrega"
```powershell
# Verificar DNS
nslookup www.seudominio.com.br

# Verificar ALB
aws elbv2 describe-load-balancers --names clinicapsi-alb

# Verificar ECS tasks
aws ecs list-tasks --cluster clinicapsi-cluster --service clinicapsi-service
```

### "DNS não propaga"
- ⏱️ Aguarde mais (até 48h)
- 🔄 Limpe cache: `ipconfig /flushdns`
- 🌐 Teste em: https://dnschecker.org

### "Certificado SSL demora"
- ⏱️ Validação leva 5-30 minutos
- ✅ Verifique registros CNAME corretos
- 🔍 Veja status: `aws acm describe-certificate --certificate-arn ARN`

### "HTTPS não funciona"
- ✅ Certificado está ISSUED?
- ✅ Listener HTTPS criado (porta 443)?
- ✅ Security Group permite 443?
- 🔄 Limpe cache do browser (Ctrl+Shift+Del)

---

## 📞 Precisa de Ajuda?

### Suporte Hostgator
- 🌐 https://suporte.hostgator.com.br
- 💬 Chat ao vivo disponível

### Documentação Completa
- 📖 `HOSTGATOR-DOMAIN-SETUP.md` - Guia detalhado completo
- 🔧 `configure-ssl.ps1` - Script automático SSL

---

## 💡 Dica Pro

Depois de tudo funcionando, considere migrar DNS para **Route 53** (AWS):
- ✅ Mais rápido
- ✅ Mais confiável
- ✅ Health checks automáticos
- 💰 Apenas ~R$ 3-5/mês

Ver guia completo em: `HOSTGATOR-DOMAIN-SETUP.md` > Opção 2

---

**⏱️ Tempo total estimado:** 
- DNS: 10 min + 1-6h propagação
- SSL: 15 min + 5-30 min validação

**🎉 Boa sorte!**
