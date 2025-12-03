# WhatsApp Bot - Venom-Bot API

Serviço Node.js que fornece API REST para integração com WhatsApp Web usando Venom-Bot.

## 🚀 Endpoints

### GET /status
Retorna status da conexão WhatsApp

**Query Params:**
- `session` (opcional): Nome da sessão (default: "default")

**Resposta:**
```json
{
  "connected": true,
  "phoneNumber": "5542999369724",
  "status": "CONNECTED"
}
```

### GET /qrcode
Gera ou retorna QR Code para autenticação

**Query Params:**
- `session` (opcional): Nome da sessão

**Resposta:**
```json
{
  "qrCode": "data:image/png;base64,iVBORw0KG...",
  "expired": false
}
```

### POST /send
Envia mensagem via WhatsApp

**Body:**
```json
{
  "session": "default",
  "number": "5542999369724",
  "message": "Olá! Sua consulta está agendada."
}
```

**Resposta:**
```json
{
  "success": true,
  "message": "Mensagem enviada"
}
```

### POST /disconnect
Desconecta sessão WhatsApp

**Query Params:**
- `session`: Nome da sessão

### GET /health
Health check do serviço

## 🔧 Variáveis de Ambiente

- `PORT`: Porta do servidor (default: 3000)
- `DATABASE_URL`: Connection string PostgreSQL
- `NODE_ENV`: Ambiente (production/development)

## 📦 Deploy no Railway

1. Criar novo serviço no Railway
2. Conectar ao repositório GitHub
3. Definir variáveis de ambiente
4. Railway detecta Dockerfile automaticamente
5. Deploy automático

## 🛠️ Desenvolvimento Local

```bash
npm install
npm run dev
```

## 📝 Notas

- As sessões são persistidas no PostgreSQL
- QR Code expira em 2 minutos
- Reconexão automática implementada
- Suporta múltiplas sessões simultâneas
