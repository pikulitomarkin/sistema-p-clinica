// Script de teste para enviar mensagem via bot Baileys
// Execute: node test-send-message.js

async function testarEnvioMensagem() {
  const botUrl = 'https://whatsapp-bot-production-0624.up.railway.app';
  
  // Teste 1: Número com código do país completo
  console.log('\n========== TESTE 1: Número com 55 ==========');
  const teste1 = {
    to: "5542988593775", // Número com 55
    message: "🧪 Teste 1: Número com código do país (55)",
    sessionName: "default"
  };
  
  console.log('Payload:', JSON.stringify(teste1, null, 2));
  
  try {
    const response1 = await fetch(`${botUrl}/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(teste1)
    });
    
    const result1 = await response1.json();
    console.log('Resposta:', result1);
    console.log('Status:', response1.status);
  } catch (error) {
    console.error('Erro:', error.message);
  }
  
  // Aguardar 3 segundos
  await new Promise(resolve => setTimeout(resolve, 3000));
  
  // Teste 2: Número sem código do país
  console.log('\n========== TESTE 2: Número sem 55 ==========');
  const teste2 = {
    to: "42988593775", // Número sem 55
    message: "🧪 Teste 2: Número sem código do país",
    sessionName: "default"
  };
  
  console.log('Payload:', JSON.stringify(teste2, null, 2));
  
  try {
    const response2 = await fetch(`${botUrl}/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(teste2)
    });
    
    const result2 = await response2.json();
    console.log('Resposta:', result2);
    console.log('Status:', response2.status);
  } catch (error) {
    console.error('Erro:', error.message);
  }
  
  // Aguardar 3 segundos
  await new Promise(resolve => setTimeout(resolve, 3000));
  
  // Teste 3: Número com @s.whatsapp.net
  console.log('\n========== TESTE 3: Número com @s.whatsapp.net ==========');
  const teste3 = {
    to: "5542988593775@s.whatsapp.net",
    message: "🧪 Teste 3: Número formatado com @s.whatsapp.net",
    sessionName: "default"
  };
  
  console.log('Payload:', JSON.stringify(teste3, null, 2));
  
  try {
    const response3 = await fetch(`${botUrl}/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(teste3)
    });
    
    const result3 = await response3.json();
    console.log('Resposta:', result3);
    console.log('Status:', response3.status);
  } catch (error) {
    console.error('Erro:', error.message);
  }
  
  console.log('\n========== TESTES CONCLUÍDOS ==========');
  console.log('Verifique seu WhatsApp para ver qual teste funcionou!');
}

testarEnvioMensagem();
