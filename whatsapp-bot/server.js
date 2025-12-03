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

// Inicializar sessão Venom-Bot
async function initSession(sessionName = 'default') {
  if (clients.has(sessionName)) {
    return clients.get(sessionName);
  }

  console.log(`Iniciando sessão: ${sessionName}`);

  const client = await venom.create({
    session: sessionName,
    multidevice: true,
    disableWelcome: true,
    updatesLog: false,
    logQR: false,
    catchQR: (base64Qr, asciiQR, attempts, urlCode) => {
      console.log(`QR Code gerado para ${sessionName}`);
      // Salvar QR Code no banco
      saveQRCode(sessionName, base64Qr);
    },
    statusFind: (statusSession, session) => {
      console.log(`Status: ${statusSession} - Sessão: ${session}`);
      updateSessionStatus(sessionName, statusSession);
    }
  });

  clients.set(sessionName, client);
  
  // Configurar evento de mensagem recebida
  client.onMessage(async (message) => {
    console.log('Mensagem recebida:', message.body);
    // Aqui você pode adicionar lógica para processar mensagens recebidas
  });

  return client;
}

// Salvar QR Code no banco
async function saveQRCode(sessionName, qrCode) {
  try {
    await pool.query(`
      INSERT INTO "WhatsAppSessions" ("SessionName", "Status", "QRCode", "QRCodeExpiry", "CreatedAt", "UpdatedAt")
      VALUES ($1, 'QRCode', $2, NOW() + INTERVAL '2 minutes', NOW(), NOW())
      ON CONFLICT ("SessionName") 
      DO UPDATE SET "QRCode" = $2, "Status" = 'QRCode', "QRCodeExpiry" = NOW() + INTERVAL '2 minutes', "UpdatedAt" = NOW()
    `, [sessionName, qrCode]);
  } catch (error) {
    console.error('Erro ao salvar QR Code:', error);
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
  const session = req.query.session || 'default';
  
  try {
    // Buscar QR Code do banco
    const result = await pool.query(
      'SELECT "QRCode", "QRCodeExpiry" FROM "WhatsAppSessions" WHERE "SessionName" = $1',
      [session]
    );

    if (result.rows.length > 0 && result.rows[0].QRCode) {
      const qrCodeExpiry = new Date(result.rows[0].QRCodeExpiry);
      if (qrCodeExpiry > new Date()) {
        return res.json({ qrCode: result.rows[0].QRCode, expired: false });
      }
    }

    // Iniciar nova sessão para gerar QR Code
    await initSession(session);
    
    // Aguardar QR Code ser salvo (timeout 10s)
    for (let i = 0; i < 20; i++) {
      await new Promise(resolve => setTimeout(resolve, 500));
      const qrResult = await pool.query(
        'SELECT "QRCode" FROM "WhatsAppSessions" WHERE "SessionName" = $1 AND "QRCode" IS NOT NULL',
        [session]
      );
      
      if (qrResult.rows.length > 0) {
        return res.json({ qrCode: qrResult.rows[0].QRCode, expired: false });
      }
    }

    res.status(408).json({ error: 'Timeout ao gerar QR Code' });
  } catch (error) {
    console.error('Erro ao gerar QR Code:', error);
    res.status(500).json({ error: 'Erro ao gerar QR Code' });
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

// Iniciar servidor
app.listen(PORT, () => {
  console.log(`WhatsApp Bot API rodando na porta ${PORT}`);
  console.log(`Ambiente: ${process.env.NODE_ENV || 'development'}`);
});

// Inicializar sessão padrão ao iniciar
setTimeout(() => {
  console.log('Inicializando sessão padrão...');
  initSession('default').catch(console.error);
}, 2000);
