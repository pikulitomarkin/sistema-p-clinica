# Receita Saúde — viabilidade no ClinicaPsi

## Interpretação correta

**Receita Saúde** (Receita Federal, IN RFB nº 2.240/2024) é o **Recibo Eletrônico de Prestação de Serviços de Saúde**, obrigatório desde 01/01/2025 para **pessoas físicas** das profissões listadas — incluindo **psicólogos** — quando prestam serviço como autônomos (Carnê-Leão).

Não é:
- receita médica digital (Memed / receita de medicamentos);
- NFC-e / nota fiscal de produto;
- API pública de “emitir recibo RFB” para softwares de clínica.

Objetivo do documento oficial: alimentar IRPF pré-preenchido do paciente e o Carnê-Leão do profissional, com autenticação **gov.br** (prata/ouro) + registro profissional ativo.

## API oficial?

| Canal | Status |
| --- | --- |
| App Receita Federal (mobile) | Oficial e obrigatório para PF |
| Carnê-Leão Web (`+ Receita Saúde`) | Oficial (computador) |
| Importação de escrituração (arquivo CSV `;`) | Oficial, em lote via e-CAC / Carnê-Leão |
| API REST pública para emissão | **Não documentada / não disponível** para terceiros |

Sistemas comerciais (ex.: Corpora) usam o fluxo de **gerar arquivo → importar no Carnê-Leão → baixar retorno**, não uma API RFB aberta.

## Requisitos do profissional (PF)

1. Conta **gov.br** nível prata ou ouro  
2. Registro **CRP** ativo  
3. Cadastro no **Carnê-Leão Web** (ocupação psicólogo = código **255**)  
4. Emissão na **data do pagamento** (ou retroativa até o limite RFB; ex.: AC 2025 até 28/02/2026)  

**CNPJ / certificado digital A1:** não são o caminho do Receita Saúde PF. Se a clínica emitir **NFS-e como PJ**, a própria RFB indica que **não** precisa do Receita Saúde para esses serviços (já há documento fiscal).

## O que o ClinicaPsi entrega agora (MVP)

Na aba **Psicólogo → Documentos**:

1. **PDF** “Recibo de Prestação de Serviços de Saúde” com dados da clínica (configs), psicólogo (CRP), paciente (CPF) e valor/data — útil para o paciente e para o consultório.  
   - **Não substitui** o recibo oficial do App/Carnê-Leão para fins de malha/IR. O rodapé do PDF deixa isso explícito.
2. **CSV** no formato de escrituração do Carnê-Leão (indicador de recibo `S`, ocupação `255`) para importação manual no e-CAC — caminho prático de integração sem API.

## Próximos passos (quando fizer sentido)

- Campo/config de CPF do profissional se ainda não estiver no usuário.  
- Exportação em lote a partir de consultas realizadas no período.  
- Se a Receita publicar API OAuth documentada: adapter isolado + credenciais só em env (nunca no git).

## Referências

- [Perguntas e respostas — Receita Saúde](https://www.gov.br/receitafederal/pt-br/assuntos/orientacao-tributaria/auditoria-fiscal/conformidade/perguntas-e-respostas-receita-saude)  
- [Formato do arquivo de Escrituração (Carnê-Leão)](https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/carne-leao/manual/formato-arquivo)  
- IN RFB nº 2.240/2024  
