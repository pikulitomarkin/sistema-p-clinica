const { Client, LocalAuth } = require('whatsapp-web.js');
const express = require('express');
const cors = require('cors');
const { Pool } = require('pg');
const QRCode = require('qrcode');
require('dotenv').config();

const app = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());

// Handler global para erros não tratados (evita crash do processo)
process.on('unhandledRejection', (reason, promise) => {
  console.error('⚠️ Unhandled Rejection:', reason);
});

process.on('uncaughtException', (error) => {
  console.error('⚠️ Uncaught Exception:', error.message);
  // Não fechar o processo, apenas logar
});

// Conexão PostgreSQL
const pool = new Pool({
  connectionString: process.env.DATABASE_URL,
  ssl: process.env.NODE_ENV === 'production' ? { rejectUnauthorized: false } : false
});

// Armazenar clientes WhatsApp Web
const clients = new Map();
const qrCodes = new Map();

// Criar cliente WhatsApp Web
function createClient(sessionName = 'default') {
  console.log(`[${sessionName}] Criando novo cliente WhatsApp Web...`);

  const client = new Client({
    authStrategy: new LocalAuth({
      clientId: sessionName,
      dataPath: './sessions'
    }),
    puppeteer: {
      headless: true,
      args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-accelerated-2d-canvas',
        '--no-first-run',
        '--no-zygote',
        '--disable-gpu',
        '--disable-web-security'
      ],
      executablePath: process.env.PUPPETEER_EXECUTABLE_PATH || '/usr/bin/chromium'
    }
  });

  // Evento: QR Code gerado
  client.on('qr', async (qr) => {
    console.log(`========== QR CODE GERADO ==========`);
    console.log(`Sessão: ${sessionName}`);
    console.log(`QR Code raw length: ${qr.length} caracteres`);
    
    // Verificar se já está conectado (evitar loop)
    const client = clients.get(sessionName);
    if (client) {
      try {
        const state = await client.getState().catch(() => 'UNKNOWN');
        console.log(`[${sessionName}] Estado atual do cliente: ${state}`);
        if (state === 'CONNECTED') {
          console.log(`[${sessionName}] ⚠️  Já está conectado, IGNORANDO QR Code`);
          return;
        }
      } catch (e) {
        console.log(`[${sessionName}] Erro ao verificar estado: ${e.message}`);
      }
    }
    
    try {
      // Converter para base64
      const qrCodeBase64 = await QRCode.toDataURL(qr);
      console.log(`QR Code convertido para base64 (${qrCodeBase64.length} chars)`);
      
      // Armazenar temporariamente
      qrCodes.set(sessionName, qrCodeBase64);
      
      // Salvar no banco
      await saveQRCode(sessionName, qrCodeBase64);
      
      console.log(`========================================`);
    } catch (error) {
      console.error(`[${sessionName}] Erro ao processar QR Code:`, error);
    }
  });

  // Evento: Cliente pronto
  client.on('ready', async () => {
    console.log(`[${sessionName}] ✅ Cliente conectado e pronto!`);
    
    try {
      const info = client.info;
      await updateSessionStatus(sessionName, 'Conectado', info.wid.user);
      
      // Limpar QR Code do banco e da memória
      qrCodes.delete(sessionName);
      await pool.query(
        'UPDATE "WhatsAppSessions" SET "QRCode" = NULL, "UpdatedAt" = NOW() WHERE "SessionName" = $1',
        [sessionName]
      );
      
      console.log(`[${sessionName}] ✅ QR Code limpo - Sessão autenticada`);
    } catch (error) {
      console.error(`[${sessionName}] Erro ao atualizar status:`, error);
    }
  });

  // Evento: Autenticação
  client.on('authenticated', () => {
    console.log(`[${sessionName}] Autenticado com sucesso!`);
  });

  // Evento: Falha de autenticação
  client.on('auth_failure', async (msg) => {
    console.error(`[${sessionName}] Falha na autenticação:`, msg);
    await updateSessionStatus(sessionName, 'Erro de Autenticação');
  });

  // Evento: Desconectado
  client.on('disconnected', async (reason) => {
    console.log(`[${sessionName}] Desconectado:`, reason);
    await updateSessionStatus(sessionName, 'Desconectado');
    clients.delete(sessionName);
  });

  return client;
}

// Salvar QR Code no banco
async function saveQRCode(sessionName, qrCode) {
  try {
    console.log(`[${sessionName}] Salvando QR Code no banco...`);
    await pool.query(`
      INSERT INTO "WhatsAppSessions" ("SessionName", "Status", "QRCode", "QRCodeExpiry", "CreatedAt", "UpdatedAt")
      VALUES ($1, 'QRCode', $2, NOW() + INTERVAL '2 minutes', NOW(), NOW())
      ON CONFLICT ("SessionName") 
      DO UPDATE SET "QRCode" = $2, "Status" = 'QRCode', "QRCodeExpiry" = NOW() + INTERVAL '2 minutes', "UpdatedAt" = NOW()
    `, [sessionName, qrCode]);
    console.log(`[${sessionName}] QR Code salvo com sucesso!`);
  } catch (error) {
    console.error(`[${sessionName}] Erro ao salvar QR Code:`, error);
  }
}

// Atualizar status da sessão
async function updateSessionStatus(sessionName, status, phoneNumber = null) {
  try {
    await pool.query(`
      UPDATE "WhatsAppSessions" 
      SET "Status" = $1, "PhoneNumber" = COALESCE($2, "PhoneNumber"), "UpdatedAt" = NOW(), 
          "LastConnection" = CASE WHEN $1 = 'Conectado' THEN NOW() ELSE "LastConnection" END
      WHERE "SessionName" = $3
    `, [status, phoneNumber, sessionName]);
  } catch (error) {
    console.error(`[${sessionName}] Erro ao atualizar status:`, error);
  }
}

// === ROTAS API ===

// GET /status - Verificar status da conexão
app.get('/status', async (req, res) => {
  const session = req.query.session || 'default';
  
  try {
    const client = clients.get(session);
    
    if (!client) {
      return res.json({ connected: false, phoneNumber: null });
    }

    const state = await client.getState();
    const info = client.info;
    
    res.json({
      connected: state === 'CONNECTED',
      phoneNumber: info?.wid?.user || null,
      status: state
    });
  } catch (error) {
    console.error(`[${session}] Erro ao obter status:`, error);
    res.status(500).json({ error: 'Erro ao verificar status' });
  }
});

// GET /qrcode - Gerar/Obter QR Code
app.get('/qrcode', async (req, res) => {
  const session = req.query.sessionName || req.query.session || 'default';
  
  try {
    console.log(`[/qrcode] ========== NOVA REQUISIÇÃO ==========`);
    console.log(`[/qrcode] Sessão: ${session}`);
    
    // Verificar se já tem QR Code gerado recentemente
    if (qrCodes.has(session)) {
      console.log(`[/qrcode] QR Code em memória encontrado`);
      return res.json({ qrCode: qrCodes.get(session), expired: false });
    }

    // Buscar QR Code válido do banco
    const result = await pool.query(
      'SELECT "QRCode", "QRCodeExpiry" FROM "WhatsAppSessions" WHERE "SessionName" = $1',
      [session]
    );

    if (result.rows.length > 0 && result.rows[0].QRCode) {
      const qrCodeExpiry = new Date(result.rows[0].QRCodeExpiry);
      if (qrCodeExpiry > new Date()) {
        console.log(`[/qrcode] QR Code válido encontrado no banco`);
        return res.json({ qrCode: result.rows[0].QRCode, expired: false });
      }
    }

    // Se já existe cliente, verificar estado
    if (clients.has(session)) {
      const client = clients.get(session);
      let state = null;
      
      try {
        state = await client.getState();
      } catch (e) {
        console.log(`[/qrcode] ⚠️ Erro ao obter estado do cliente: ${e.message}`);
        // Cliente fantasma (Puppeteer fechado), limpar
        clients.delete(session);
        state = 'DEAD';
      }
      
      console.log(`[/qrcode] Cliente existente - Estado: ${state}`);
      
      if (state === 'CONNECTED') {
        console.log(`[/qrcode] ✅ Cliente já está conectado - não gerar novo QR Code`);
        return res.status(400).json({ 
          error: 'Já conectado',
          message: 'A sessão já está conectada ao WhatsApp'
        });
      }
      
      // Se está em processo de conexão, não destruir
      if (state === 'OPENING' || state === 'INITIALIZING') {
        console.log(`[/qrcode] ⏳ Cliente está conectando... aguarde`);
        return res.status(400).json({ 
          error: 'Conectando',
          message: 'Aguarde, a conexão está em andamento'
        });
      }
      
      // Cliente não está conectado ou está morto, destruir
      if (state !== 'DEAD') {
        console.log(`[/qrcode] Cliente existe mas não está conectado (${state}), destruindo...`);
        try {
          await client.destroy();
        } catch (e) {
          console.log(`[/qrcode] ⚠️ Erro ao destruir cliente: ${e.message}`);
        }
        clients.delete(session);
      }
    }

    // Limpar QR Code expirado
    console.log(`[/qrcode] Limpando QR Code expirado da memória e banco...`);
    qrCodes.delete(session);
    await pool.query(
      'UPDATE "WhatsAppSessions" SET "QRCode" = NULL, "Status" = $1, "UpdatedAt" = NOW() WHERE "SessionName" = $2',
      ['Gerando QR Code', session]
    );
    console.log(`[/qrcode] ✅ QR Code limpo`);

    console.log(`[/qrcode] Criando novo cliente whatsapp-web.js...`);
    const client = createClient(session);
    clients.set(session, client);
    console.log(`[/qrcode] ✅ Cliente criado e adicionado ao Map`);

    // Inicializar cliente
    console.log(`[/qrcode] Inicializando cliente (aguardar evento 'qr')...`);
    client.initialize();
    console.log(`[/qrcode] ✅ Inicialização disparada`);

    // Aguardar QR Code ou conexão (timeout 90s)
    console.log(`[/qrcode] Aguardando QR Code ou conexão...`);
    for (let i = 0; i < 180; i++) {
      await new Promise(resolve => setTimeout(resolve, 500));
      
      // Verificar se conectou (QR Code foi escaneado)
      const client = clients.get(session);
      if (client) {
        try {
          const state = await client.getState();
          if (state === 'CONNECTED') {
            console.log(`[/qrcode] ✅ Cliente conectou durante aguardo do QR Code!`);
            return res.json({ 
              connected: true,
              message: 'WhatsApp conectado com sucesso!'
            });
          }
        } catch (e) {}
      }
      
      // Verificar memória
      if (qrCodes.has(session)) {
        console.log(`[/qrcode] QR Code gerado com sucesso!`);
        return res.json({ qrCode: qrCodes.get(session), expired: false });
      }
      
      // Verificar banco
      const qrResult = await pool.query(
        'SELECT "QRCode" FROM "WhatsAppSessions" WHERE "SessionName" = $1 AND "QRCode" IS NOT NULL',
        [session]
      );
      
      if (qrResult.rows.length > 0 && qrResult.rows[0].QRCode) {
        console.log(`[/qrcode] QR Code encontrado no banco!`);
        qrCodes.set(session, qrResult.rows[0].QRCode);
        return res.json({ qrCode: qrResult.rows[0].QRCode, expired: false });
      }
      
      if (i % 20 === 0 && i > 0) {
        console.log(`[/qrcode] Aguardando... ${i/2}s`);
      }
    }

    console.error(`[/qrcode] Timeout após 90 segundos`);
    res.status(408).json({ error: 'Timeout ao gerar QR Code' });
  } catch (error) {
    console.error('[/qrcode] Erro:', error);
    res.status(500).json({ error: 'Erro ao gerar QR Code', details: error.message });
  }
});

// POST /send - Enviar mensagem
app.post('/send', async (req, res) => {
  const { session = 'default', number, message } = req.body;

  if (!number || !message) {
    return res.status(400).json({ error: 'Número e mensagem são obrigatórios' });
  }

  try {
    const client = clients.get(session);
    
    if (!client) {
      return res.status(400).json({ error: 'Sessão não iniciada' });
    }

    const state = await client.getState();
    if (state !== 'CONNECTED') {
      return res.status(400).json({ error: 'Cliente não está conectado' });
    }

    // Formatar número
    const chatId = number.includes('@c.us') ? number : `${number}@c.us`;
    
    await client.sendMessage(chatId, message);
    
    res.json({ success: true, message: 'Mensagem enviada' });
  } catch (error) {
    console.error(`[${session}] Erro ao enviar mensagem:`, error);
    res.status(500).json({ error: 'Erro ao enviar mensagem', details: error.message });
  }
});

// POST /disconnect - Desconectar sessão
app.post('/disconnect', async (req, res) => {
  const session = req.query.session || 'default';

  try {
    const client = clients.get(session);
    
    if (client) {
      await client.destroy();
      clients.delete(session);
    }

    qrCodes.delete(session);

    await pool.query(
      'UPDATE "WhatsAppSessions" SET "Status" = $1, "QRCode" = NULL, "UpdatedAt" = NOW() WHERE "SessionName" = $2',
      ['Desconectado', session]
    );

    res.json({ success: true, message: 'Desconectado' });
  } catch (error) {
    console.error(`[${session}] Erro ao desconectar:`, error);
    res.status(500).json({ error: 'Erro ao desconectar' });
  }
});

// POST /reset - Resetar sessão
app.post('/reset', async (req, res) => {
  const session = req.query.session || 'default';

  try {
    console.log(`[/reset] Resetando sessão: ${session}`);
    
    // Destruir cliente
    if (clients.has(session)) {
      try {
        await clients.get(session).destroy();
      } catch (e) {}
      clients.delete(session);
    }

    qrCodes.delete(session);

    // Deletar sessão do disco
    const fs = require('fs');
    const path = require('path');
    const sessionPath = path.join(__dirname, 'sessions', `session-${session}`);
    console.log(`Deletando sessão em: ${sessionPath}`);
    try {
      if (fs.existsSync(sessionPath)) {
        fs.rmSync(sessionPath, { recursive: true, force: true });
        console.log(`Sessão deletada!`);
      }
    } catch (e) {
      console.log(`Erro ao deletar sessão:`, e.message);
    }

    // Limpar banco
    await pool.query(
      'UPDATE "WhatsAppSessions" SET "Status" = $1, "QRCode" = NULL, "AuthToken" = NULL, "PhoneNumber" = NULL, "UpdatedAt" = NOW() WHERE "SessionName" = $2',
      ['Reset', session]
    );

    console.log(`[/reset] Sessão ${session} resetada!`);
    res.json({ success: true, message: 'Sessão resetada' });
  } catch (error) {
    console.error('[/reset] Erro:', error);
    res.status(500).json({ error: 'Erro ao resetar sessão', details: error.message });
  }
});

// Health check
app.get('/health', (req, res) => {
  res.json({ 
    status: 'ok', 
    sessions: clients.size,
    library: 'whatsapp-web.js'
  });
});

// Debug: Testar conexão com banco
app.get('/debug/db', async (req, res) => {
  try {
    const result = await pool.query('SELECT NOW()');
    const tables = await pool.query(`
      SELECT table_name 
      FROM information_schema.tables 
      WHERE table_schema = 'public' 
      AND table_name = 'WhatsAppSessions'
    `);
    
    res.json({
      database: 'connected',
      time: result.rows[0].now,
      tableExists: tables.rows.length > 0,
      library: 'whatsapp-web.js v1.23.0'
    });
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

// Iniciar servidor
app.listen(PORT, () => {
  console.log(`WhatsApp Bot API (whatsapp-web.js) rodando na porta ${PORT}`);
  console.log(`Ambiente: ${process.env.NODE_ENV || 'development'}`);
  console.log('Aguardando requisições...');
});
