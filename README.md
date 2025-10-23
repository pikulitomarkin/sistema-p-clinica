# 🏥 ClinicaPsi - Sistema de Gestão para Clínicas de Psicologia

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=flat-square&logo=blazor)
![EF Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4?style=flat-square)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15.0-336791?style=flat-square&logo=postgresql)
![Docker](https://img.shields.io/badge/Docker-Production-2496ED?style=flat-square&logo=docker)
![AWS](https://img.shields.io/badge/AWS-Cloud-FF9900?style=flat-square&logo=amazonaws)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

[![Build Status](https://github.com/pikulitomarkin/sistema-p-clinica/workflows/.NET%20Build%20and%20Test/badge.svg)](https://github.com/pikulitomarkin/sistema-p-clinica/actions)

> **Projeto concluído e em produção!**
> Acesse: [www.psiianasantos.com.br](https://www.psiianasantos.com.br)

Sistema completo de gestão desenvolvido em ASP.NET Core 9.0 com Blazor Server para clínicas de psicologia, oferecendo funcionalidades modernas de agendamento, gamificação e notificações inteligentes.

## 🔗 Links Importantes

- **Site em produção**: [www.psiianasantos.com.br](https://www.psiianasantos.com.br)
- **Repositório**: https://github.com/pikulitomarkin/sistema-p-clinica
- **Issues**: https://github.com/pikulitomarkin/sistema-p-clinica/issues
- **Wiki**: https://github.com/pikulitomarkin/sistema-p-clinica/wiki

## 🚀 Status do Projeto

- ✅ **Concluído**
- 🚢 **Deploy em produção via Docker e AWS**
- 🗄️ **Banco de dados: PostgreSQL**
- 🌐 **Acesso público:** [www.psiianasantos.com.br](https://www.psiianasantos.com.br)

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
- WhatsApp, Email, SMS
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
│   ├── ClinicaPsi.Infrastructure/   # Acesso a dados e infraestrutura
│   ├── ClinicaPsi.Application/      # Lógica de negócio
│   └── ClinicaPsi.Web/              # Interface Blazor Server
├── .github
├── README.md
└── ClinicaPsi.sln
```

## 🚀 Tecnologias Utilizadas

- **ASP.NET Core 9.0**
- **Blazor Server**
- **Entity Framework Core 9.0**
- **PostgreSQL** (produção via Docker/AWS)
- **Bootstrap 5.3**
- **SignalR**
- **Docker**
- **AWS EC2 / RDS**

## 📋 Pré-requisitos para Desenvolvimento

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Docker
- PostgreSQL (local ou cloud)
- Editor de código (Visual Studio, VS Code, Rider)
- Git

## 🔧 Instalação e Execução Local

```bash
git clone https://github.com/pikulitomarkin/sistema-p-clinica.git
cd sistema-p-clinica
```

### Usando Docker

```bash
docker-compose up --build
```

### Manualmente

```bash
cd src/ClinicaPsi.Web
dotnet restore
dotnet run
```

Acesse em:
- **HTTP**: http://localhost:5000
- **HTTPS**: https://localhost:5001

## 🗄️ Banco de Dados

- **Produção:** PostgreSQL (AWS RDS)
- **Desenvolvimento:** SQLite ou PostgreSQL local

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

## 🎨 Interface

- Design moderno e responsivo
- Mobile-first
- Cores consistentes com tema de saúde
- Feedback visual imediato
- Navegação intuitiva

## 🤝 Contribuindo

Contribuições são bem-vindas! Para contribuir:

1. Fork o projeto
2. Crie uma branch para sua feature
3. Commit suas mudanças
4. Push para a branch
5. Abra um Pull Request

## 📝 Convenções de Código

- Use PascalCase para classes e métodos
- Use camelCase para variáveis locais
- Sempre adicione comentários em código complexo
- Siga os princípios SOLID
- Mantenha métodos pequenos e focados

## 🐛 Reportando Bugs

Abra uma [issue](https://github.com/pikulitomarkin/sistema-p-clinica/issues) com detalhes do problema.

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

**Feito com .NET 9.0, Blazor, Docker, AWS e PostgreSQL** 🚀
