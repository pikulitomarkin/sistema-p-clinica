# 📱 Tarefa: Implementar WhatsApp Web com QR Code no Railway

## 🎯 Objetivo
Implementar sistema de WhatsApp Web com leitura de QR Code que mantém a conexão persistente no Railway com PostgreSQL.

## 📋 Status Atual

### ❌ Implementação Existente (Não Funcional)
- **WhatsAppService.cs**: Usa WhatsApp Business API (Meta/Facebook)
- **Problemas**:
  - Requer aprovação da Meta
  - Custo por mensagem
  - Sem QR Code
  - Limitações de templates
  - Precisa de webhooks públicos

### ✅ Solução Proposta: WhatsApp Web + Baileys/Venom

## 🛠️ Implementação Planejada

### 1. Backend - Nova Tabela no Banco
```sql
CREATE TABLE WhatsAppSessions (
    Id SERIAL PRIMARY KEY,
    SessionName VARCHAR(100) NOT NULL,
    Status VARCHAR(50) NOT NULL, -- Conectado, Desconectado, QRCode, Erro
    QRCode TEXT NULL,
    QRCodeExpiry TIMESTAMP NULL,
    AuthToken TEXT NULL,
    PhoneNumber VARCHAR(50) NULL,
    LastConnection TIMESTAMP NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### 2. Serviço WhatsApp Web
**Arquivo**: `src/ClinicaPsi.Application/Services/WhatsAppWebService.cs`

**Funcionalidades**:
- ✅ Gerar QR Code para conexão
- ✅ Salvar sessão no PostgreSQL
- ✅ Reconexão automática
- ✅ Enviar mensagens sem limitações
- ✅ Receber mensagens em tempo real
- ✅ Status da conexão (Conectado/Desconectado)

### 3. Página de Administração
**Arquivo**: `src/ClinicaPsi.Web/Pages/Admin/WhatsApp.cshtml`

**Interface**:
```
┌─────────────────────────────────────┐
│  WhatsApp Bot - Gerenciamento       │
├─────────────────────────────────────┤
│                                     │
│  Status: 🟢 Conectado               │
│  Número: +55 42 99936-9724          │
│  Última conexão: 02/12/2025 10:30   │
│                                     │
│  [🔌 Desconectar] [🔄 Reconectar]  │
│                                     │
├─────────────────────────────────────┤
│  QR Code (se desconectado):         │
│  ┌─────────────────┐                │
│  │  [QR CODE IMG]  │                │
│  └─────────────────┘                │
│  Escaneie com WhatsApp              │
│                                     │
├─────────────────────────────────────┤
│  Mensagens Recentes:                │
│  📩 (42) 99999-9999: "Olá!"        │
│  📤 Você: "Olá, como posso..."     │
│                                     │
└─────────────────────────────────────┘
```

### 4. Integração com Bot Existente
**Manter**: `WhatsAppBotService.cs` com intents (agendar, cancelar, etc.)

**Trocar**: `WhatsAppService` por `WhatsAppWebService`

## 🔧 Opções de Implementação

### Opção A: Baileys (Node.js) via API
**Vantagens**:
- Biblioteca mais estável
- Multi-device oficial do WhatsApp
- Comunidade grande

**Implementação**:
1. Criar container Node.js no Railway
2. API REST para comunicação C# ↔ Node.js
3. Persistência no PostgreSQL compartilhado

### Opção B: Venom-Bot (Node.js) via API
**Vantagens**:
- Mais simples de usar
- QR Code fácil de gerar
- Boa documentação

**Implementação**:
1. Container Node.js separado
2. Endpoints REST para C#
3. WebSocket para mensagens em tempo real

### Opção C: WhatsApp-Web.js + Puppeteer
**Vantagens**:
- Controle total do navegador
- Funciona como WhatsApp Web real

**Desvantagens**:
- Mais pesado (precisa Chromium)
- Maior uso de memória no Railway

## 📦 Arquitetura Recomendada (Opção B - Venom-Bot)

```
┌──────────────────────────────────────────┐
│          Railway Production              │
├──────────────────────────────────────────┤
│                                          │
│  ┌────────────────┐  ┌───────────────┐  │
│  │  ASP.NET Core  │◄─┤  PostgreSQL   │  │
│  │  (Main App)    │  │   Database    │  │
│  └────────┬───────┘  └───────────────┘  │
│           │                              │
│           │ HTTP/WebSocket               │
│           ▼                              │
│  ┌────────────────┐  ┌───────────────┐  │
│  │  Venom-Bot     │◄─┤  PostgreSQL   │  │
│  │  (Node.js)     │  │  (Sessions)   │  │
│  │  WhatsApp API  │  └───────────────┘  │
│  └────────────────┘                      │
│           │                              │
│           ▼                              │
│     WhatsApp Web                         │
│                                          │
└──────────────────────────────────────────┘
```

## 📝 Passos de Implementação

### Fase 1: Preparação do Banco (1h)
- [ ] Criar migration para tabela `WhatsAppSessions`
- [ ] Adicionar entidade `WhatsAppSession` em `Entities.cs`
- [ ] Atualizar `AppDbContext`
- [ ] Rodar migration no Railway

### Fase 2: Container Venom-Bot (2h)
- [ ] Criar projeto Node.js separado
- [ ] Instalar Venom-Bot
- [ ] Criar API REST endpoints:
  - `GET /status` - Status da conexão
  - `GET /qrcode` - Gerar QR Code
  - `POST /send` - Enviar mensagem
  - `POST /disconnect` - Desconectar
  - `POST /reconnect` - Reconectar
- [ ] Configurar persistência no PostgreSQL
- [ ] Criar Dockerfile
- [ ] Deploy no Railway como serviço separado

### Fase 3: Integração C# (2h)
- [ ] Criar `WhatsAppWebService.cs`
- [ ] Implementar HttpClient para comunicar com Venom-Bot
- [ ] Criar métodos:
  - `ObterStatusAsync()`
  - `GerarQRCodeAsync()`
  - `EnviarMensagemAsync()`
  - `DesconectarAsync()`
  - `ReconectarAsync()`
- [ ] Atualizar `WhatsAppBotService` para usar novo serviço

### Fase 4: Página Admin (1h)
- [ ] Criar `Pages/Admin/WhatsApp.cshtml`
- [ ] Criar `Pages/Admin/WhatsApp.cshtml.cs`
- [ ] Implementar UI com:
  - Status da conexão
  - QR Code (auto-refresh a cada 5s)
  - Botões Desconectar/Reconectar
  - Histórico de mensagens
- [ ] Adicionar SignalR para updates em tempo real

### Fase 5: Testes e Deploy (1h)
- [ ] Testar conexão via QR Code
- [ ] Testar envio de mensagens
- [ ] Testar reconexão automática
- [ ] Testar persistência da sessão
- [ ] Deploy final no Railway
- [ ] Documentar processo no README

## 🔐 Segurança

- [ ] Tokens de sessão criptografados no banco
- [ ] Apenas usuários Admin podem acessar página WhatsApp
- [ ] Logs de todas as mensagens enviadas/recebidas
- [ ] Rate limiting para evitar spam
- [ ] Validação de números de telefone

## 📚 Referências

- [Venom-Bot Documentation](https://github.com/orkestral/venom)
- [Baileys Documentation](https://github.com/WhiskeySockets/Baileys)
- [WhatsApp-Web.js](https://github.com/pedroslopez/whatsapp-web.js)
- [Railway Docs - Multi-Service Apps](https://docs.railway.app/)

## 💡 Melhorias Futuras

- [ ] Múltiplas sessões (diferentes números)
- [ ] Agendamento de mensagens
- [ ] Respostas automáticas personalizáveis
- [ ] Analytics de mensagens
- [ ] Integração com OpenAI para respostas inteligentes
- [ ] Suporte a mídias (imagens, vídeos, documentos)
- [ ] Grupos de WhatsApp
- [ ] Listas de transmissão

## ⏱️ Estimativa Total: 7 horas

## 🎯 Prioridade: **ALTA**

Este recurso permitirá:
- ✅ Agendamento automático via WhatsApp
- ✅ Lembretes sem custo
- ✅ Atendimento automatizado 24/7
- ✅ Redução de no-shows
- ✅ Melhor experiência do paciente

---

**Data de Criação**: 02/12/2025
**Responsável**: Sistema Clínica Psicológica
**Status**: 🟡 Planejado - Aguardando Aprovação
