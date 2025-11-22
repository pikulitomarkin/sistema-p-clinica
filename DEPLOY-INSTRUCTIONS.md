# 🚀 Instruções de Deploy - PsiiAnaSantos

## ⚠️ IMPORTANTE: Docker Desktop deve estar rodando!

### 1️⃣ Iniciar Docker Desktop

**Windows:**
1. Procure por "Docker Desktop" no menu Iniciar
2. Clique para abrir
3. Aguarde o ícone da baleia 🐋 ficar verde na barra de tarefas
4. Pode levar 1-2 minutos para inicializar completamente

**Verificar se está rodando:**
```powershell
docker version
```

Se retornar informações sobre **Client** E **Server**, está tudo OK! ✅

---

## 2️⃣ Executar Deploy Automatizado

Depois que o Docker estiver rodando:

```powershell
.\deploy.ps1
```

O script fará automaticamente:
- ✅ Build da imagem Docker
- ✅ Tag com timestamp
- ✅ Login no AWS ECR
- ✅ Push da imagem
- ✅ Registro de nova task definition
- ✅ Atualização do serviço ECS
- ✅ Monitoramento do deployment
- ✅ Verificação de health dos targets

---

## 3️⃣ O que foi mudado nesta versão (v3.6.1)

### Mudanças Visuais:
- ❌ Removidos 4 cards de estatísticas da homepage
- ✅ Email atualizado para `psiianasantos@psiianasantos.com.br` em todo o sistema

### Nova Funcionalidade:
- ✨ Página de configuração WhatsApp no Admin (`/admin/whatsapp`)
  - Formulário para Phone Number ID
  - Access Token e Verify Token
  - App Secret (validação HMAC)
  - OpenAI API Key (bot inteligente)
  - Exibição da URL do webhook
  - Botão de teste de conexão
  - Instruções passo-a-passo

### Arquivos Modificados:
```
src/ClinicaPsi.Web/Pages/
├── _Host.cshtml (removido cards, atualizado email)
├── Shared/
│   ├── _Layout.cshtml (email no footer)
│   └── _AdminLayout.cshtml (menu WhatsApp)
└── Admin/
    ├── WhatsApp.cshtml (NOVO)
    └── WhatsApp.cshtml.cs (NOVO)

src/ClinicaPsi.Application/Services/
├── ConfiguracaoService.cs (método RemoverAsync, email padrão)
├── NotificacaoService.cs (emails nos templates)
└── Tests/
    └── PdfExemploGenerator.cs (email no PDF)
```

---

## 4️⃣ Após o Deploy

### Verificar no AWS Console:
1. **ECS**: Verifique se as 2 tasks estão RUNNING
2. **Target Group**: Verifique se ambos targets estão "healthy"
3. **CloudWatch Logs**: Verifique logs em `/ecs/clinicapsi`

### Testar o Site:
1. Acesse via ALB (Load Balancer URL)
2. Verifique que os cards de estatísticas NÃO aparecem na homepage
3. Verifique o email no rodapé: `psiianasantos@psiianasantos.com.br`

### Configurar WhatsApp:
1. Faça login como Admin
2. Acesse `/admin/whatsapp`
3. Preencha os campos:
   - **Phone Number ID**: Do Facebook Business Manager
   - **Access Token**: Token permanente do WhatsApp API
   - **Verify Token**: Token customizado para validação
   - **App Secret**: (Opcional) Para validação HMAC
   - **OpenAI API Key**: (Opcional) Para respostas inteligentes
4. Clique em "Salvar Configurações"
5. Copie a URL do webhook
6. Configure no Facebook Business Manager

---

## 5️⃣ Troubleshooting

### Docker não inicia:
- Reinicie o Windows
- Verifique se WSL2 está habilitado
- Verifique requisitos de virtualização no BIOS

### Build falha:
```powershell
# Limpar cache e tentar novamente
docker system prune -a
.\deploy.ps1
```

### ECS deployment trava:
```powershell
# Verificar logs
aws logs tail /ecs/clinicapsi --follow

# Verificar serviço
aws ecs describe-services --cluster clinicapsi-cluster --services clinicapsi-service
```

### Targets não ficam healthy:
- Verifique security groups (porta 8080)
- Verifique logs do container
- Verifique connection string do PostgreSQL
- Endpoint `/health` deve retornar 200 OK

---

## 📞 URLs Importantes

- **Homepage**: http://[ALB-DNS]/
- **Admin**: http://[ALB-DNS]/admin
- **WhatsApp Config**: http://[ALB-DNS]/admin/whatsapp
- **Health Check**: http://[ALB-DNS]/health
- **Webhook**: http://[ALB-DNS]/api/whatsapp/webhook

---

## 🎯 Próximos Passos Após Deploy

1. ✅ Configurar WhatsApp no admin
2. ✅ Testar envio de mensagem via WhatsApp
3. ✅ Configurar webhook no Facebook Business Manager
4. ✅ Testar bot com comandos:
   - "agendar" / "marcar" → Agendar consulta
   - "remarcar" → Remarcar consulta existente
   - "cancelar" → Cancelar consulta
   - "confirmar" → Confirmar presença
   - "pontos" / "psicopontos" → Verificar pontos
5. ✅ Monitorar logs para garantir estabilidade

---

**✨ Tudo pronto para deploy! Basta iniciar o Docker Desktop e executar `.\deploy.ps1`**
