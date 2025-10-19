# 🏥 ClinicaPsi - Sistema de Gestão para Clínicas de Psicologia

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=flat-square&logo=blazor)
![EF Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

[![Build Status](https://github.com/pikulitomarkin/sistema-p-clinica/workflows/.NET%20Build%20and%20Test/badge.svg)](https://github.com/pikulitomarkin/sistema-p-clinica/actions)

Sistema completo de gestão desenvolvido em ASP.NET Core 9.0 com Blazor Server para clínicas de psicologia, oferecendo funcionalidades modernas de agendamento, gamificação e notificações inteligentes.

## 🔗 Links Importantes

- **Repositório**: https://github.com/pikulitomarkin/sistema-p-clinica
- **Issues**: https://github.com/pikulitomarkin/sistema-p-clinica/issues
- **Wiki**: https://github.com/pikulitomarkin/sistema-p-clinica/wiki

## ✨ Funcionalidades Principais

### 👥 Gestão de Pacientes
- Cadastro completo com validações
- Histórico médico e medicamentos
- Contatos de emergência
- Sistema de busca avançada

### 📅 Sistema de Agendamento Inteligente
- Agendamento online com verificação de disponibilidade
- Calendário interativo
- Bloqueio automático de horários
- Reagendamento facilitado
- Notificações automáticas

### 🎯 PsicoPontos - Sistema de Gamificação
- Acumule 1 ponto por consulta realizada
- Troque 10 pontos por 1 consulta gratuita
- Histórico completo de pontuação
- Incentivo à continuidade do tratamento

### 👨‍⚕️ Gestão de Psicólogos
- Cadastro com CRP e especialidades
- Configuração de horários personalizados
- Controle de dias de atendimento
- Valores diferenciados por profissional

### 🔔 Sistema de Notificações
- **WhatsApp**: Lembretes e confirmações
- **Email**: Comunicações formais
- **SMS**: Alertas urgentes
- Notificações 24h antes da consulta
- Confirmação automática de presença

### 📊 Dashboard e Relatórios
- Visão geral da clínica
- Estatísticas de atendimento
- Consultas do dia e próximas
- Indicadores de performance

## 🏗️ Arquitetura

O projeto segue os princípios da **Clean Architecture** com separação clara de responsabilidades:

```
ClinicaPsi/
├── src/
│   ├── ClinicaPsi.Shared/           # Modelos e DTOs compartilhados
│   │   └── Models/
│   │       └── Entities.cs          # Entidades do domínio
│   │
│   ├── ClinicaPsi.Infrastructure/   # Acesso a dados e infraestrutura
│   │   └── Data/
│   │       └── AppDbContext.cs      # Contexto do EF Core
│   │
│   ├── ClinicaPsi.Application/      # Lógica de negócio
│   │   └── Services/
│   │       ├── PacienteService.cs
│   │       ├── PsicologoService.cs
│   │       └── ConsultaService.cs
│   │
│   └── ClinicaPsi.Web/              # Interface Blazor Server
│       ├── Pages/                   # Páginas Razor
│       ├── Shared/                  # Componentes compartilhados
│       └── wwwroot/                 # Arquivos estáticos
│
├── .gitignore
├── README.md
└── ClinicaPsi.sln
```

## 🚀 Tecnologias Utilizadas

- **ASP.NET Core 9.0** - Framework principal
- **Blazor Server** - Interface interativa e reativa
- **Entity Framework Core 9.0** - ORM para acesso a dados
- **SQLite** - Banco de dados leve e portátil
- **Bootstrap 5.3** - Framework CSS responsivo
- **SignalR** - Comunicação em tempo real
- **Data Annotations** - Validações robustas

## 📋 Pré-requisitos

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Editor de código (Visual Studio, VS Code, Rider)
- Git (opcional)

## 🔧 Instalação e Configuração

### 1. Clone o repositório

```bash
git clone https://github.com/pikulitomarkin/sistema-p-clinica.git
cd sistema-p-clinica
```

### 2. Restaure as dependências

```bash
cd src/ClinicaPsi.Web
dotnet restore
```

### 3. Execute o projeto

```bash
dotnet run
```

### 4. Acesse o sistema

Abra seu navegador em:
- **HTTP**: http://localhost:5000
- **HTTPS**: https://localhost:5001

## 🗄️ Banco de Dados

O sistema utiliza **SQLite** e cria automaticamente o banco de dados na primeira execução.

**Localização**: `src/ClinicaPsi.Web/clinicapsi.db`

### Dados iniciais (Seed)

O sistema já vem com 2 psicólogos cadastrados para teste:
- Dr. João Silva - CRP 06/123456
- Dra. Maria Santos - CRP 06/654321

## 📱 Funcionalidades Detalhadas

### Sistema PsicoPontos

O sistema de pontos incentiva a continuidade do tratamento:

1. **Ganhe pontos**: A cada consulta realizada, o paciente ganha 1 PsicoPonto
2. **Troque por benefícios**: 10 pontos = 1 consulta gratuita
3. **Histórico transparente**: Todas as movimentações ficam registradas

### Agendamento Inteligente

- Verificação automática de disponibilidade
- Bloqueio de horários já ocupados
- Respeito aos dias e horários de cada psicólogo
- Duração padrão de 50 minutos por consulta

### Status de Consultas

- **Agendada**: Consulta marcada, aguardando data
- **Confirmada**: Paciente confirmou presença
- **Realizada**: Consulta concluída
- **Cancelada**: Consulta cancelada com motivo
- **No-Show**: Paciente não compareceu
- **Reagendada**: Consulta remarcada

## 🔐 Segurança

- Validações em múltiplas camadas
- Proteção contra injeção SQL (EF Core)
- Validação de CPF e CRP
- Sanitização de inputs
- HTTPS obrigatório em produção

## 📊 Modelos de Dados

### Paciente
- Dados pessoais (nome, CPF, email, telefone)
- Data de nascimento
- Endereço completo
- Contatos de emergência
- Histórico médico e medicamentos
- Sistema de pontos

### Psicólogo
- Dados profissionais (nome, CRP, email)
- Especialidades
- Valor da consulta
- Configuração de horários (manhã/tarde)
- Dias de atendimento

### Consulta
- Paciente e psicólogo
- Data e horário
- Duração e valor
- Status e tipo
- Observações e relatório de sessão

## 🎨 Interface

- Design moderno e responsivo
- Mobile-first
- Cores consistentes com tema de saúde
- Feedback visual imediato
- Navegação intuitiva

## 🚦 Roadmap

### Fase 1 - MVP ✅ (Concluído)
- [x] Estrutura do projeto
- [x] Modelos de dados
- [x] Configuração do banco
- [x] Dashboard básico

### Fase 2 - Funcionalidades Core (Em Desenvolvimento)
- [ ] CRUD completo de pacientes
- [ ] CRUD completo de psicólogos
- [ ] Sistema de agendamento
- [ ] Gestão de consultas

### Fase 3 - Recursos Avançados
- [ ] Sistema de notificações
- [ ] Integração WhatsApp
- [ ] Relatórios e gráficos
- [ ] Exportação de dados

### Fase 4 - Melhorias
- [ ] Sistema de autenticação
- [ ] Níveis de acesso (Admin, Recepcionista, Psicólogo)
- [ ] Backup automático
- [ ] Tema escuro

## 🤝 Contribuindo

Contribuições são bem-vindas! Para contribuir:

1. Fork o projeto (https://github.com/pikulitomarkin/sistema-p-clinica/fork)
2. Crie uma branch para sua feature (`git checkout -b feature/MinhaFeature`)
3. Commit suas mudanças (`git commit -m 'feat: Adiciona MinhaFeature'`)
4. Push para a branch (`git push origin feature/MinhaFeature`)
5. Abra um Pull Request

## 📝 Convenções de Código

- Use PascalCase para classes e métodos
- Use camelCase para variáveis locais
- Sempre adicione comentários em código complexo
- Siga os princípios SOLID
- Mantenha métodos pequenos e focados

## 🐛 Reportando Bugs

Encontrou um bug? Por favor, abra uma [issue](https://github.com/pikulitomarkin/sistema-p-clinica/issues) com:

- Descrição detalhada do problema
- Passos para reproduzir
- Comportamento esperado vs atual
- Screenshots (se aplicável)
- Ambiente (SO, versão do .NET, navegador)

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

## 👨‍💻 Autor

Desenvolvido com ❤️ para facilitar a gestão de clínicas de psicologia.

## 📞 Suporte

- 📧 Email: suporte@clinicapsi.com
- 🐛 Issues: https://github.com/pikulitomarkin/sistema-p-clinica/issues
- 💬 Discussions: https://github.com/pikulitomarkin/sistema-p-clinica/discussions

## 🙏 Agradecimentos

- Comunidade .NET
- Equipe Blazor
- Todos os contribuidores

---

⭐ Se este projeto foi útil para você, considere dar uma estrela no [GitHub](https://github.com/pikulitomarkin/sistema-p-clinica)!

**Feito com .NET 9.0 e Blazor** 🚀
