const venom = require('venom-bot');
const express = require('express');
const cors = require('cors');
const { Pool } = require('pg');
require('dotenv').config();

const app = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());

// Conexão PostgreSQL
const pool = new Pool({
  connectionString: process.env.DATABASE_URL,
  ssl: process.env.NODE_ENV === 'production' ? { rejectUnauthorized: false } : false
});

// Armazenar clientes Venom-Bot
const clients = new Map();
const initializingClients = new Map();

// Inicializar sessão Venom-Bot
async function initSession(sessionName = 'default') {
  // Se já existe um cliente conectado, retornar
  if (clients.has(sessionName)) {
    console.log(`Sessão ${sessionName} já existe, reutilizando...`);
    return clients.get(sessionName);
  }

  // Se já está inicializando, aguardar
  if (initializingClients.has(sessionName)) {
    console.log(`Sessão ${sessionName} já está sendo inicializada, aguardando...`);
    return initializingClients.get(sessionName);
  }

  console.log(`Iniciando nova sessão: ${sessionName}`);

  // Marcar como inicializando
  const initPromise = venom.create({
    session: sessionName,
    multidevice: true,
    disableWelcome: true,
    updatesLog: false,
    logQR: false,
    catchQR: (base64Qr, asciiQR, attempts, urlCode) => {
      console.log(`========== QR CODE CAPTURADO ==========`);
      console.log(`Sessão: ${sessionName}`);
      console.log(`Tentativa: ${attempts}`);
      console.log(`QR Code (primeiros 50 chars): ${base64Qr.substring(0, 50)}...`);
      console.log(`========================================`);
      // Salvar QR Code no banco
      saveQRCode(sessionName, base64Qr);
    },
    statusFind: (statusSession, session) => {
      console.log(`Status: ${statusSession} - Sessão: ${session}`);
      updateSessionStatus(sessionName, statusSession);
      
      // Se conectou com sucesso, remover QR Code do banco
      if (statusSession === 'isLogged' || statusSession === 'qrReadSuccess') {
        pool.query(`
          UPDATE "WhatsAppSessions" 
          SET "QRCode" = NULL, "Status" = 'Conectado', "UpdatedAt" = NOW()
          WHERE "SessionName" = $1
        `, [sessionName]).catch(err => console.error('Erro ao limpar QR Code:', err));
      }
    }
  }).then(client => {
    clients.set(sessionName, client);
    initializingClients.delete(sessionName);
    return client;
  }).catch(err => {
    console.error(`Erro ao inicializar sessão ${sessionName}:`, err);
    initializingClients.delete(sessionName);
    throw err;
  });

  initializingClients.set(sessionName, initPromise);
  return initPromise;
}

// Salvar QR Code no banco
async function saveQRCode(sessionName, qrCode) {
  try {
    console.log(`Salvando QR Code no banco para sessão ${sessionName}...`);
    await pool.query(`
      INSERT INTO "WhatsAppSessions" ("SessionName", "Status", "QRCode", "QRCodeExpiry", "CreatedAt", "UpdatedAt")
      VALUES ($1, 'QRCode', $2, NOW() + INTERVAL '2 minutes', NOW(), NOW())
      ON CONFLICT ("SessionName") 
      DO UPDATE SET "QRCode" = $2, "Status" = 'QRCode', "QRCodeExpiry" = NOW() + INTERVAL '2 minutes', "UpdatedAt" = NOW()
    `, [sessionName, qrCode]);
    console.log(`QR Code salvo com sucesso no banco!`);
  } catch (error) {
    console.error('Erro ao salvar QR Code no banco:', error);
  }
}

// Atualizar status da sessão
async function updateSessionStatus(sessionName, status) {
  try {
    const dbStatus = status === 'isLogged' ? 'Conectado' : 
                     status === 'qrReadSuccess' ? 'Conectado' :
                     status === 'qrReadFail' ? 'Erro' : 'Desconectado';
    
    await pool.query(`
      UPDATE "WhatsAppSessions" 
      SET "Status" = $1, "UpdatedAt" = NOW(), "LastConnection" = CASE WHEN $1 = 'Conectado' THEN NOW() ELSE "LastConnection" END
      WHERE "SessionName" = $2
    `, [dbStatus, sessionName]);
  } catch (error) {
    console.error('Erro ao atualizar status:', error);
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

    const status = await client.getConnectionState();
    const hostDevice = await client.getHostDevice();
    
    res.json({
      connected: status === 'CONNECTED',
      phoneNumber: hostDevice?.id?.user || null,
      status: status
    });
  } catch (error) {
    console.error('Erro ao obter status:', error);
    res.status(500).json({ error: 'Erro ao verificar status' });
  }
});

// GET /qrcode - Gerar/Obter QR Code
app.get('/qrcode', async (req, res) => {
  const session = req.query.sessionName || req.query.session || 'default';
  
  try {
    console.log(`[/qrcode] ========== NOVA REQUISIÇÃO ==========`);
    console.log(`[/qrcode] Sessão solicitada: ${session}`);
    console.log(`[/qrcode] Clientes ativos: ${clients.size}`);
    console.log(`[/qrcode] Clientes inicializando: ${initializingClients.size}`);
    
    // Verificar se já está conectado
    if (clients.has(session)) {
      const client = clients.get(session);
      try {
        const state = await client.getConnectionState();
        console.log(`[/qrcode] Estado da conexão: ${state}`);
        if (state === 'CONNECTED') {
          console.log(`[/qrcode] Sessão ${session} já está conectada`);
          return res.json({ 
            error: 'Já conectado',
            message: 'A sessão já está conectada ao WhatsApp'
          });
        }
      } catch (err) {
        console.log(`[/qrcode] Cliente existe mas erro ao verificar estado:`, err.message);
        console.log(`[/qrcode] Removendo cliente e reiniciando...`);
        try {
          await client.close();
        } catch (e) {}
        clients.delete(session);
        initializingClients.delete(session);
      }
    }
    
    // Se já está inicializando, aguardar
    if (initializingClients.has(session)) {
      console.log(`[/qrcode] Sessão já está sendo inicializada, aguardando...`);
      // Aguardar QR Code ser salvo (timeout 60s)
      for (let i = 0; i < 120; i++) {
        await new Promise(resolve => setTimeout(resolve, 500));
        const qrResult = await pool.query(
          'SELECT "QRCode" FROM "WhatsAppSessions" WHERE "SessionName" = $1 AND "QRCode" IS NOT NULL',
          [session]
        );
        
        if (qrResult.rows.length > 0 && qrResult.rows[0].QRCode) {
          console.log(`[/qrcode] QR Code encontrado no banco!`);
          return res.json({ qrCode: qrResult.rows[0].QRCode, expired: false });
        }
        
        // Log a cada 10 segundos
        if (i % 20 === 0 && i > 0) {
          console.log(`[/qrcode] Aguardando... ${i/2} segundos`);
        }
      }
      console.error(`[/qrcode] Timeout: QR Code não foi gerado em 60 segundos`);
      return res.status(408).json({ error: 'Timeout ao gerar QR Code' });
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
      } else {
        console.log(`[/qrcode] QR Code expirado, gerando novo...`);
      }
    }

    // Limpar QR Code expirado do banco
    await pool.query(
      'UPDATE "WhatsAppSessions" SET "QRCode" = NULL, "Status" = $1, "UpdatedAt" = NOW() WHERE "SessionName" = $2',
      ['Gerando QR Code', session]
    );

    console.log(`[/qrcode] Iniciando nova sessão...`);
    
    // Iniciar nova sessão para gerar QR Code (sem await para não bloquear)
    initSession(session).catch(err => {
      console.error(`[/qrcode] Erro ao iniciar sessão:`, err);
      initializingClients.delete(session);
    });
    
    // Aguardar QR Code ser salvo (timeout 60s)
    console.log(`[/qrcode] Aguardando QR Code ser gerado...`);
    for (let i = 0; i < 120; i++) {
      await new Promise(resolve => setTimeout(resolve, 500));
      const qrResult = await pool.query(
        'SELECT "QRCode" FROM "WhatsAppSessions" WHERE "SessionName" = $1 AND "QRCode" IS NOT NULL',
        [session]
      );
      
      if (qrResult.rows.length > 0 && qrResult.rows[0].QRCode) {
        console.log(`[/qrcode] QR Code gerado com sucesso após ${i/2} segundos!`);
        return res.json({ qrCode: qrResult.rows[0].QRCode, expired: false });
      }
      
      // Log a cada 10 segundos
      if (i % 20 === 0 && i > 0) {
        console.log(`[/qrcode] Aguardando... ${i/2} segundos`);
      }
    }

    console.error(`[/qrcode] Timeout: QR Code não foi gerado em 60 segundos`);
    // Limpar inicialização falhada
    initializingClients.delete(session);
    if (clients.has(session)) {
      try {
        const client = clients.get(session);
        await client.close();
      } catch (e) {}
      clients.delete(session);
    }
    res.status(408).json({ error: 'Timeout ao gerar QR Code' });
  } catch (error) {
    console.error('[/qrcode] Erro ao gerar QR Code:', error);
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
    let client = clients.get(session);
    
    if (!client) {
      client = await initSession(session);
    }

    // Formatar número (adicionar @c.us se necessário)
    const formattedNumber = number.includes('@c.us') ? number : `${number}@c.us`;
    
    await client.sendText(formattedNumber, message);
    
    res.json({ success: true, message: 'Mensagem enviada' });
  } catch (error) {
    console.error('Erro ao enviar mensagem:', error);
    res.status(500).json({ error: 'Erro ao enviar mensagem', details: error.message });
  }
});

// POST /disconnect - Desconectar sessão
app.post('/disconnect', async (req, res) => {
  const session = req.query.session || 'default';

  try {
    const client = clients.get(session);
    
    if (client) {
      await client.close();
      clients.delete(session);
    }

    await pool.query(
      'UPDATE "WhatsAppSessions" SET "Status" = $1, "QRCode" = NULL, "UpdatedAt" = NOW() WHERE "SessionName" = $2',
      ['Desconectado', session]
    );

    res.json({ success: true, message: 'Desconectado' });
  } catch (error) {
    console.error('Erro ao desconectar:', error);
    res.status(500).json({ error: 'Erro ao desconectar' });
  }
});

// Health check
app.get('/health', (req, res) => {
  res.json({ status: 'ok', sessions: clients.size });
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
      tableExists: tables.rows.length > 0
    });
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

// Criar tabela WhatsAppSessions
app.post('/debug/create-table', async (req, res) => {
  try {
    await pool.query(`
      CREATE TABLE IF NOT EXISTS "WhatsAppSessions" (
        "Id" SERIAL PRIMARY KEY,
        "SessionName" TEXT NOT NULL UNIQUE,
        "Status" TEXT NOT NULL,
        "QRCode" TEXT,
        "QRCodeExpiry" TIMESTAMP,
        "AuthToken" TEXT,
        "PhoneNumber" TEXT,
        "LastConnection" TIMESTAMP,
        "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
        "UpdatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
      )
    `);
    
    res.json({ success: true, message: 'Tabela WhatsAppSessions criada com sucesso!' });
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

// Iniciar servidor
app.listen(PORT, () => {
  console.log(`WhatsApp Bot API rodando na porta ${PORT}`);
  console.log(`Ambiente: ${process.env.NODE_ENV || 'development'}`);
  console.log('Aguardando requisições para inicializar sessões...');
});
