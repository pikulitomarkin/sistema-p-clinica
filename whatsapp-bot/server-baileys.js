const { default: makeWASocket, DisconnectReason, useMultiFileAuthState, fetchLatestBaileysVersion } = require('@whiskeysockets/baileys');
const express = require('express');
const cors = require('cors');
const { Pool } = require('pg');
const P = require('pino');
const fs = require('fs');
const path = require('path');
require('dotenv').config();

const app = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());

// Logger configurado
const logger = P({ level: 'info' });

// Handler global para erros
process.on('unhandledRejection', (reason, promise) => {
  console.error('⚠️ Unhandled Rejection:', reason);
});

process.on('uncaughtException', (error) => {
  console.error('⚠️ Uncaught Exception:', error.message);
});

// Conexão PostgreSQL
const pool = new Pool({
  connectionString: process.env.DATABASE_URL,
  ssl: process.env.NODE_ENV === 'production' ? { rejectUnauthorized: false } : false
});

// Testar conexão com banco
pool.query('SELECT NOW()', (err, res) => {
  if (err) {
    console.error('❌ Erro ao conectar no PostgreSQL:', err);
  } else {
    console.log('✅ Conectado ao PostgreSQL:', res.rows[0].now);
  }
});

// Armazenar sockets ativos
const activeSockets = new Map();
const qrCodes = new Map();

// Diretório para auth states
const AUTH_DIR = path.join(__dirname, 'auth_info_baileys');
if (!fs.existsSync(AUTH_DIR)) {
  fs.mkdirSync(AUTH_DIR, { recursive: true });
}

// Salvar QR Code no banco
async function saveQRCode(sessionName, qrCode) {
  try {
    await pool.query(
      `INSERT INTO "WhatsAppSessions" ("SessionName", "Status", "QRCode", "QRCodeExpiry", "UpdatedAt")
       VALUES ($1, $2, $3, NOW() + INTERVAL '2 minutes', NOW())
       ON CONFLICT ("SessionName") 
       DO UPDATE SET 
         "Status" = $2,
         "QRCode" = $3,
         "QRCodeExpiry" = NOW() + INTERVAL '2 minutes',
         "UpdatedAt" = NOW()`,
      [sessionName, 'qrcode', qrCode]
    );
    console.log(`[${sessionName}] QR Code salvo no banco`);
  } catch (error) {
    console.error(`[${sessionName}] Erro ao salvar QR Code:`, error);
  }
}

// Atualizar status da sessão
async function updateSessionStatus(sessionName, status, phoneNumber = null) {
  try {
    await pool.query(
      `INSERT INTO "WhatsAppSessions" ("SessionName", "Status", "PhoneNumber", "LastConnection", "UpdatedAt")
       VALUES ($1, $2, $3, NOW(), NOW())
       ON CONFLICT ("SessionName") 
       DO UPDATE SET 
         "Status" = $2,
         "PhoneNumber" = COALESCE($3, "WhatsAppSessions"."PhoneNumber"),
         "LastConnection" = NOW(),
         "UpdatedAt" = NOW()`,
      [sessionName, status, phoneNumber]
    );
    console.log(`[${sessionName}] Status atualizado: ${status}`);
  } catch (error) {
    console.error(`[${sessionName}] Erro ao atualizar status:`, error);
  }
}

// Criar conexão Baileys
async function connectToWhatsApp(sessionName = 'default') {
  console.log(`\n========================================`);
  console.log(`[${sessionName}] 🚀 Iniciando conexão Baileys...`);
  console.log(`========================================\n`);

  const sessionDir = path.join(AUTH_DIR, sessionName);
  if (!fs.existsSync(sessionDir)) {
    fs.mkdirSync(sessionDir, { recursive: true });
  }

  try {
    // Buscar última versão do Baileys
    const { version, isLatest } = await fetchLatestBaileysVersion();
    console.log(`[${sessionName}] Baileys version: ${version.join('.')} (${isLatest ? 'latest' : 'outdated'})`);

    // Carregar estado de autenticação
    const { state, saveCreds } = await useMultiFileAuthState(sessionDir);

    // Criar socket
    const sock = makeWASocket({
      version,
      logger,
      printQRInTerminal: true,
      auth: state,
      browser: ['ClinicaPsi', 'Chrome', '10.0'],
      syncFullHistory: false,
      markOnlineOnConnect: true,
      getMessage: async (key) => {
        return { conversation: 'Mensagem não disponível' };
      }
    });

    // Salvar socket ativo
    activeSockets.set(sessionName, sock);

    // ========== EVENTOS ==========

    // QR Code gerado
    sock.ev.on('connection.update', async (update) => {
      const { connection, lastDisconnect, qr } = update;

      // QR Code
      if (qr) {
        console.log(`\n========== QR CODE GERADO ==========`);
        console.log(`Sessão: ${sessionName}`);
        
        // Converter para base64
        const QRCode = require('qrcode');
        const qrCodeBase64 = await QRCode.toDataURL(qr);
        
        // Armazenar temporariamente
        qrCodes.set(sessionName, qrCodeBase64);
        
        // Salvar no banco
        await saveQRCode(sessionName, qrCodeBase64);
        
        console.log(`QR Code disponível em: GET /qrcode/${sessionName}`);
        console.log(`========================================\n`);
      }

      // Conexão fechada
      if (connection === 'close') {
        const shouldReconnect = lastDisconnect?.error?.output?.statusCode !== DisconnectReason.loggedOut;
        const reason = lastDisconnect?.error?.output?.statusCode;
        
        console.log(`\n[${sessionName}] ❌ Conexão fechada. Razão:`, reason);
        console.log(`[${sessionName}] Reconectar?`, shouldReconnect);

        await updateSessionStatus(sessionName, 'disconnected');
        activeSockets.delete(sessionName);

        if (shouldReconnect) {
          console.log(`[${sessionName}] 🔄 Reconectando em 5 segundos...`);
          setTimeout(() => connectToWhatsApp(sessionName), 5000);
        } else {
          console.log(`[${sessionName}] ⚠️ Logout realizado. Remover sessão para reconectar.`);
          // Limpar diretório de autenticação
          if (fs.existsSync(sessionDir)) {
            fs.rmSync(sessionDir, { recursive: true, force: true });
          }
        }
      }

      // Conexão aberta
      if (connection === 'open') {
        console.log(`\n[${sessionName}] ✅ CONECTADO COM SUCESSO!`);
        
        const phoneNumber = sock.user?.id?.split(':')[0];
        console.log(`[${sessionName}] Número: ${phoneNumber}`);
        
        await updateSessionStatus(sessionName, 'connected', phoneNumber);
        qrCodes.delete(sessionName); // Remover QR Code
        
        console.log(`========================================\n`);
      }
    });

    // Salvar credenciais quando atualizadas
    sock.ev.on('creds.update', saveCreds);

    // Mensagens recebidas
    sock.ev.on('messages.upsert', async ({ messages, type }) => {
      if (type !== 'notify') return;

      for (const msg of messages) {
        // Ignorar mensagens próprias
        if (msg.key.fromMe) continue;

        const from = msg.key.remoteJid;
        const text = msg.message?.conversation || 
                    msg.message?.extendedTextMessage?.text || 
                    '';

        if (!text) continue;

        console.log(`\n[${sessionName}] 📨 Mensagem recebida:`);
        console.log(`De: ${from}`);
        console.log(`Texto: ${text}`);

        // Processar comandos ou salvar no banco
        await processIncomingMessage(sessionName, from, text, msg);
      }
    });

    return sock;

  } catch (error) {
    console.error(`[${sessionName}] ❌ Erro ao conectar:`, error);
    await updateSessionStatus(sessionName, 'error');
    throw error;
  }
}

// Processar mensagens recebidas
async function processIncomingMessage(sessionName, from, text, fullMessage) {
  try {
    // Salvar mensagem no banco
    await pool.query(
      `INSERT INTO "WhatsAppMessages" ("SessionName", "From", "Message", "MessageData", "ReceivedAt")
       VALUES ($1, $2, $3, $4, NOW())`,
      [sessionName, from, text, JSON.stringify(fullMessage)]
    );

    console.log(`[${sessionName}] 💾 Mensagem salva no banco`);

    // Chamar webhook do ASP.NET para processar mensagem
    const aspnetWebhookUrl = process.env.ASPNET_WEBHOOK_URL;
    
    if (aspnetWebhookUrl) {
      try {
        console.log(`[${sessionName}] 📡 Enviando para ASP.NET: ${aspnetWebhookUrl}`);
        
        const response = await fetch(`${aspnetWebhookUrl}/webhook/whatsapp`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            sessionName: sessionName,
            from: from,
            message: text,
            timestamp: new Date().toISOString()
          })
        });

        if (response.ok) {
          console.log(`[${sessionName}] ✅ Mensagem processada pelo ASP.NET`);
        } else {
          console.warn(`[${sessionName}] ⚠️ ASP.NET retornou erro: ${response.status}`);
        }
      } catch (webhookError) {
        console.warn(`[${sessionName}] ⚠️ Erro ao chamar webhook ASP.NET:`, webhookError.message);
      }
    } else {
      console.log(`[${sessionName}] ℹ️ ASPNET_WEBHOOK_URL não configurada - webhook não será chamado`);
    }

  } catch (error) {
    console.error(`[${sessionName}] Erro ao processar mensagem:`, error);
  }
}

// Enviar mensagem
async function sendMessage(sessionName, to, message) {
  const sock = activeSockets.get(sessionName);
  
  if (!sock) {
    throw new Error(`Sessão ${sessionName} não está conectada`);
  }

  // Formatar número (adicionar @s.whatsapp.net se necessário)
  let formattedTo = to.includes('@') ? to : `${to}@s.whatsapp.net`;
  
  console.log(`[${sessionName}] 📤 Tentando enviar mensagem:`);
  console.log(`  - De: ${sock.user?.id}`);
  console.log(`  - Para: ${formattedTo}`);
  console.log(`  - Mensagem: ${message.substring(0, 50)}...`);
  
  try {
    const result = await sock.sendMessage(formattedTo, { text: message });
    console.log(`[${sessionName}] ✅ Mensagem enviada com sucesso!`);
    console.log(`  - Result:`, JSON.stringify(result, null, 2));
    return { success: true, result };
  } catch (error) {
    console.error(`[${sessionName}] ❌ ERRO ao enviar mensagem:`, error.message);
    console.error(`  - Stack:`, error.stack);
    console.error(`  - Data:`, error.data);
    throw error;
  }
}

// ========== API REST ==========

// Health check
app.get('/', (req, res) => {
  res.json({
    service: 'ClinicaPsi WhatsApp Bot (Baileys)',
    status: 'running',
    activeSessions: activeSockets.size,
    timestamp: new Date().toISOString()
  });
});

// Obter QR Code
app.get('/qrcode/:sessionName?', async (req, res) => {
  const sessionName = req.params.sessionName || 'default';
  
  try {
    // Verificar se já está conectado
    const sock = activeSockets.get(sessionName);
    if (sock && sock.user) {
      return res.json({
        status: 'connected',
        phoneNumber: sock.user.id.split(':')[0],
        message: 'Sessão já está conectada'
      });
    }

    // Buscar QR Code do cache
    const qrCodeCache = qrCodes.get(sessionName);
    if (qrCodeCache) {
      return res.json({
        status: 'qrcode',
        qrCode: qrCodeCache
      });
    }

    // Buscar QR Code do banco
    const result = await pool.query(
      `SELECT "QRCode", "Status", "QRCodeExpiry" 
       FROM "WhatsAppSessions" 
       WHERE "SessionName" = $1`,
      [sessionName]
    );

    if (result.rows.length > 0 && result.rows[0].QRCode) {
      const session = result.rows[0];
      
      // Verificar se QR Code ainda é válido
      if (session.QRCodeExpiry && new Date(session.QRCodeExpiry) > new Date()) {
        return res.json({
          status: session.Status,
          qrCode: session.QRCode
        });
      }
    }

    // Iniciar nova conexão se não existir
    if (!activeSockets.has(sessionName)) {
      connectToWhatsApp(sessionName).catch(console.error);
      
      return res.json({
        status: 'connecting',
        message: 'Iniciando conexão... QR Code será gerado em breve.'
      });
    }

    res.json({
      status: 'waiting',
      message: 'Aguardando geração do QR Code...'
    });

  } catch (error) {
    console.error('Erro ao buscar QR Code:', error);
    res.status(500).json({ error: error.message });
  }
});

// Status da sessão
app.get('/status/:sessionName?', (req, res) => {
  const sessionName = req.params.sessionName || 'default';
  const sock = activeSockets.get(sessionName);

  if (sock && sock.user) {
    res.json({
      status: 'connected',
      phoneNumber: sock.user.id.split(':')[0],
      name: sock.user.name
    });
  } else {
    res.json({
      status: 'disconnected',
      message: 'Sessão não está conectada'
    });
  }
});

// Enviar mensagem via API
app.post('/send', async (req, res) => {
  try {
    const { sessionName = 'default', to, message } = req.body;

    if (!to || !message) {
      return res.status(400).json({ 
        error: 'Parâmetros obrigatórios: to, message' 
      });
    }

    await sendMessage(sessionName, to, message);
    
    res.json({ 
      success: true,
      message: 'Mensagem enviada com sucesso'
    });

  } catch (error) {
    console.error('Erro ao enviar mensagem:', error);
    res.status(500).json({ 
      success: false,
      error: error.message 
    });
  }
});

// Desconectar sessão
app.post('/logout/:sessionName?', async (req, res) => {
  const sessionName = req.params.sessionName || 'default';
  const sock = activeSockets.get(sessionName);

  if (sock) {
    await sock.logout();
    activeSockets.delete(sessionName);
    
    // Limpar diretório de autenticação
    const sessionDir = path.join(AUTH_DIR, sessionName);
    if (fs.existsSync(sessionDir)) {
      fs.rmSync(sessionDir, { recursive: true, force: true });
    }

    await updateSessionStatus(sessionName, 'disconnected');
    
    res.json({ 
      success: true,
      message: 'Logout realizado com sucesso'
    });
  } else {
    res.json({ 
      success: false,
      message: 'Sessão não está conectada'
    });
  }
});

// Webhook para receber notificações do ASP.NET
app.post('/webhook/notify', async (req, res) => {
  try {
    const { to, message, sessionName = 'default' } = req.body;

    console.log('📥 Webhook recebido:', { to, message: message?.substring(0, 50) });

    await sendMessage(sessionName, to, message);
    
    res.json({ success: true });
  } catch (error) {
    console.error('Erro no webhook:', error);
    res.status(500).json({ error: error.message });
  }
});

// Iniciar servidor
app.listen(PORT, async () => {
  console.log(`\n========================================`);
  console.log(`🚀 WhatsApp Bot Baileys rodando na porta ${PORT}`);
  console.log(`========================================\n`);

  // Criar tabelas se não existirem
  await createTables();

  // Iniciar conexão padrão
  connectToWhatsApp('default').catch(console.error);
});

// Criar tabelas no PostgreSQL
async function createTables() {
  try {
    await pool.query(`
      CREATE TABLE IF NOT EXISTS "WhatsAppSessions" (
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
    `);

    await pool.query(`
      CREATE TABLE IF NOT EXISTS "WhatsAppMessages" (
        "Id" SERIAL PRIMARY KEY,
        "SessionName" VARCHAR(100) NOT NULL,
        "From" VARCHAR(100) NOT NULL,
        "Message" TEXT NOT NULL,
        "MessageData" JSONB NULL,
        "ReceivedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
      );
    `);

    console.log('✅ Tabelas criadas/verificadas no PostgreSQL');
  } catch (error) {
    console.error('❌ Erro ao criar tabelas:', error);
  }
}
