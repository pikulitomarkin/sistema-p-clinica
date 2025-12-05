# 🚀 WhatsApp Bot com Baileys - ClinicaPsi

## 📱 O que mudou?

Migração de **whatsapp-web.js** para **@whiskeysockets/baileys** para melhor estabilidade e performance.

## ✨ Vantagens do Baileys

- ✅ **Mais leve**: Não usa Puppeteer (menos memória e CPU)
- ✅ **Multi-device**: Suporte nativo ao WhatsApp multi-dispositivos
- ✅ **Mais estável**: Menos problemas de desconexão
- ✅ **Comunidade ativa**: Atualizado constantemente
- ✅ **Melhor performance**: Ideal para Railway/VPS

## 🔧 Instalação

### 1. Instalar dependências

```bash
cd whatsapp-bot
npm install
```

### 2. Configurar variáveis de ambiente

Crie/edite o arquivo `.env`:

```env
DATABASE_URL=postgresql://usuario:senha@host:porta/database
PORT=3000
NODE_ENV=production
```

### 3. Rodar localmente

```bash
npm start
```

Ou em desenvolvimento:

```bash
npm run dev
```

## 📡 API Endpoints

### 1. Health Check
```http
GET http://localhost:3000/
```

**Resposta:**
```json
{
  "service": "ClinicaPsi WhatsApp Bot (Baileys)",
  "status": "running",
  "activeSessions": 1,
  "timestamp": "2025-12-05T18:00:00.000Z"
}
```

### 2. Obter QR Code
```http
GET http://localhost:3000/qrcode
GET http://localhost:3000/qrcode/default
```

**Resposta (QR Code disponível):**
```json
{
  "status": "qrcode",
  "qrCode": "data:image/png;base64,iVBORw0KGgoAAAANS..."
}
```

**Resposta (já conectado):**
```json
{
  "status": "connected",
  "phoneNumber": "5511999999999",
  "message": "Sessão já está conectada"
}
```

### 3. Status da Conexão
```http
GET http://localhost:3000/status
GET http://localhost:3000/status/default
```

**Resposta:**
```json
{
  "status": "connected",
  "phoneNumber": "5511999999999",
  "name": "Clínica Psicologia"
}
```

### 4. Enviar Mensagem
```http
POST http://localhost:3000/send
Content-Type: application/json

{
  "to": "5511999999999",
  "message": "Olá! Sua consulta está agendada para amanhã às 10h.",
  "sessionName": "default"
}
```

**Resposta:**
```json
{
  "success": true,
  "message": "Mensagem enviada com sucesso"
}
```

### 5. Logout (Desconectar)
```http
POST http://localhost:3000/logout
POST http://localhost:3000/logout/default
```

**Resposta:**
```json
{
  "success": true,
  "message": "Logout realizado com sucesso"
}
```

### 6. Webhook (ASP.NET → WhatsApp)
```http
POST http://localhost:3000/webhook/notify
Content-Type: application/json

{
  "to": "5511999999999",
  "message": "Lembrete: Sua consulta é amanhã às 10h.",
  "sessionName": "default"
}
```

## 🗄️ Tabelas PostgreSQL

O bot cria automaticamente 2 tabelas:

### WhatsAppSessions
Armazena informações das sessões conectadas.

```sql
CREATE TABLE "WhatsAppSessions" (
  "Id" SERIAL PRIMARY KEY,
  "SessionName" VARCHAR(100) NOT NULL UNIQUE,
  "Status" VARCHAR(50) NOT NULL,
  "QRCode" TEXT NULL,
  "QRCodeExpiry" TIMESTAMP NULL,
  "PhoneNumber" VARCHAR(50) NULL,
  "LastConnection" TIMESTAMP NULL,
  "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### WhatsAppMessages
Armazena mensagens recebidas.

```sql
CREATE TABLE "WhatsAppMessages" (
  "Id" SERIAL PRIMARY KEY,
  "SessionName" VARCHAR(100) NOT NULL,
  "From" VARCHAR(100) NOT NULL,
  "Message" TEXT NOT NULL,
  "MessageData" JSONB NULL,
  "ReceivedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## 🚀 Deploy no Railway

### 1. Criar novo serviço

No Railway dashboard:
- Click em "New Service" → "GitHub Repo"
- Selecione o repositório
- Configure o diretório: `whatsapp-bot`

### 2. Variáveis de ambiente

Adicione no Railway:

```env
DATABASE_URL=${{Postgres.DATABASE_URL}}
PORT=3000
NODE_ENV=production
```

### 3. Build Command

```bash
npm install
```

### 4. Start Command

```bash
npm start
```

### 5. Configurar domínio público

Railway vai gerar um domínio tipo:
```
https://clinicapsi-whatsapp.up.railway.app
```

Use este domínio no ASP.NET para chamar a API.

## 🔗 Integração com ASP.NET

### 1. Atualizar appsettings.json

```json
{
  "WhatsApp": {
    "BotUrl": "https://clinicapsi-whatsapp.up.railway.app",
    "SessionName": "default"
  }
}
```

### 2. Atualizar WhatsAppService.cs

```csharp
public async Task<bool> EnviarMensagemAsync(string telefone, string mensagem)
{
    try
    {
        var botUrl = _configuration["WhatsApp:BotUrl"];
        var sessionName = _configuration["WhatsApp:SessionName"] ?? "default";

        var payload = new
        {
            to = telefone,
            message = mensagem,
            sessionName = sessionName
        };

        var response = await _httpClient.PostAsJsonAsync($"{botUrl}/send", payload);
        return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao enviar mensagem via Baileys");
        return false;
    }
}
```

## 📱 Como conectar pela primeira vez

1. **Iniciar o bot**:
```bash
npm start
```

2. **Acessar endpoint QR Code**:
```
http://localhost:3000/qrcode
```

3. **Escanear QR Code**:
- Copie o código base64 da resposta
- Cole em um visualizador online ou na página admin do ASP.NET
- Abra WhatsApp no celular → Aparelhos conectados → Conectar aparelho
- Escaneie o QR Code

4. **Verificar conexão**:
```
http://localhost:3000/status
```

## 🔄 Reconexão Automática

O bot reconecta automaticamente em caso de:
- Perda de conexão internet
- Timeout
- Erro temporário

**NÃO reconecta** em caso de:
- Logout manual (via `/logout`)
- Desconexão pelo celular

## 🐛 Troubleshooting

### QR Code não aparece
```bash
# Limpar sessão antiga
rm -rf auth_info_baileys/default

# Reiniciar bot
npm start
```

### Erro de conexão com banco
```bash
# Testar conexão PostgreSQL
psql $DATABASE_URL
```

### Bot desconecta constantemente
- Verificar se o celular está com internet estável
- Verificar se não há outro aparelho conectado com mesmo número
- Limpar sessões antigas e reconectar

## 📊 Logs

O bot mostra logs detalhados no console:

```
[default] 🚀 Iniciando conexão Baileys...
[default] Baileys version: 6.7.7 (latest)
[default] ✅ CONECTADO COM SUCESSO!
[default] Número: 5511999999999
[default] 📨 Mensagem recebida:
  De: 5511888888888@s.whatsapp.net
  Texto: Olá
```

## ⚡ Performance

- **Memória**: ~100-150 MB (vs 500+ MB do whatsapp-web.js)
- **CPU**: Baixo uso (~5-10% em idle)
- **Startup**: ~5-10 segundos para conectar

## 📝 Notas Importantes

1. ⚠️ **Não é oficial**: WhatsApp pode detectar e bloquear (risco baixo para uso pessoal/pequeno)
2. ✅ **Multi-device obrigatório**: Precisa estar ativado no WhatsApp
3. 🔒 **Sessão única**: Um número = uma sessão ativa por vez
4. 📱 **Celular precisa estar online**: Pelo menos ocasionalmente

## 🆚 Comparação

| Recurso | whatsapp-web.js | Baileys |
|---------|----------------|---------|
| Memória | 500+ MB | ~150 MB |
| Puppeteer | ✅ Sim | ❌ Não |
| Multi-device | ⚠️ Parcial | ✅ Completo |
| Estabilidade | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| Performance | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| Deploy Railway | ⚠️ Difícil | ✅ Fácil |

## 📚 Referências

- [Baileys GitHub](https://github.com/WhiskeySockets/Baileys)
- [Baileys Documentation](https://whiskeysockets.github.io/)
- [Railway Deployment](https://railway.app)

## 🎯 Próximos Passos

1. ✅ Deploy no Railway
2. ✅ Conectar ASP.NET ao bot
3. ✅ Criar página admin para QR Code
4. ⏳ Implementar respostas automáticas
5. ⏳ Sistema de templates de mensagens
