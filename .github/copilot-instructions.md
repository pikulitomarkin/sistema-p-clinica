# Sistema de Gestão - Clínica de Psicologia

Este workspace contém um sistema completo de gestão para clínicas de psicologia desenvolvido em ASP.NET Core 9.0 com Blazor Server.

## 🎯 Visão Geral do Projeto

### Objetivo
Sistema web completo para gestão de clínicas de psicologia com foco em:
- Eficiência no agendamento
- Gamificação para engajamento dos pacientes
- Automação de notificações
- Controle financeiro e administrativo

### Tecnologias Core
- **Framework**: ASP.NET Core 9.0
- **UI**: Blazor Server (renderização server-side)
- **ORM**: Entity Framework Core 9.0
- **Database**: SQLite (desenvolvimento) / SQL Server (produção)
- **CSS**: Bootstrap 5.3
- **Real-time**: SignalR (built-in Blazor)

## 📁 Estrutura do Projeto

```
ClinicaPsi/
├── src/
│   ├── ClinicaPsi.Shared/              # Camada de domínio
│   │   └── Models/
│   │       └── Entities.cs             # Entidades principais
│   │
│   ├── ClinicaPsi.Infrastructure/      # Camada de dados
│   │   └── Data/
│   │       └── AppDbContext.cs         # Configuração EF Core
│   │
│   ├── ClinicaPsi.Application/         # Camada de negócio
│   │   └── Services/
│   │       ├── PacienteService.cs
│   │       ├── PsicologoService.cs
│   │       ├── ConsultaService.cs
│   │       └── NotificacaoService.cs
│   │
│   └── ClinicaPsi.Web/                 # Camada de apresentação
│       ├── Pages/                      # Páginas Blazor
│       ├── Shared/                     # Componentes compartilhados
│       ├── wwwroot/                    # Arquivos estáticos
│       └── Program.cs                  # Configuração da aplicação
```

## 🏗️ Arquitetura

### Clean Architecture
O projeto segue Clean Architecture com 4 camadas:

1. **Shared (Domain)**: Entidades, enums, interfaces
2. **Infrastructure**: Implementação de acesso a dados
3. **Application**: Regras de negócio e casos de uso
4. **Web (Presentation)**: Interface do usuário

### Padrões Utilizados
- Repository Pattern (via EF Core DbContext)
- Service Layer Pattern
- Dependency Injection
- CQRS (futuro)
- Unit of Work (via EF Core)

## 📊 Modelos de Dados

### Entidades Principais

#### Paciente
```csharp
- Id, Nome, Email, Telefone, CPF
- DataNascimento, Endereco
- ContatoEmergencia, TelefoneEmergencia
- HistoricoMedico, MedicamentosUso
- PsicoPontos, ConsultasRealizadas, ConsultasGratuitas
```

#### Psicologo
```csharp
- Id, Nome, Email, CRP, Telefone
- Especialidades, ValorConsulta
- Horários de atendimento (manhã/tarde)
- Dias de atendimento (seg-dom)
```

#### Consulta
```csharp
- Id, PacienteId, PsicologoId
- DataHorario, DuracaoMinutos, Valor
- Status, Tipo
- Observacoes, RelatorioSessao
```

### Enums

```csharp
StatusConsulta: Agendada, Confirmada, Realizada, Cancelada, NoShow, Reagendada
TipoConsulta: Normal, Gratuita, Retorno, Avaliacao
TipoNotificacao: Email, WhatsApp, SMS, Push
```

## 🎯 Funcionalidades Principais

### 1. Sistema PsicoPontos
- **Regra**: 1 ponto por consulta realizada
- **Benefício**: 10 pontos = 1 consulta gratuita
- **Tracking**: Histórico completo de movimentações
- **Gamificação**: Incentivo à continuidade do tratamento

### 2. Agendamento Inteligente
- Verificação de disponibilidade em tempo real
- Bloqueio automático de horários ocupados
- Respeito aos dias/horários de cada psicólogo
- Duração configurável por consulta

### 3. Notificações Automáticas
- **24h antes**: Lembrete da consulta
- **Confirmação**: Solicitação de confirmação
- **Múltiplos canais**: WhatsApp, Email, SMS
- **Queue system**: Fila de envio assíncrono

### 4. Dashboard
- Estatísticas em tempo real
- Consultas do dia
- Próximas consultas (7 dias)
- Indicadores de performance

## 🔧 Convenções de Código

### Naming
- **Classes**: PascalCase (ex: `PacienteService`)
- **Métodos**: PascalCase (ex: `GetAllAsync`)
- **Variáveis**: camelCase (ex: `totalPacientes`)
- **Propriedades**: PascalCase (ex: `DataNascimento`)
- **Constantes**: UPPER_SNAKE_CASE (ex: `MAX_PONTOS`)

### Async/Await
- Sempre use sufixo `Async` em métodos assíncronos
- Prefira `Task<T>` sobre `Task`
- Use `ConfigureAwait(false)` em libraries

### Validações
- Use Data Annotations para validações básicas
- Valide no Service para regras de negócio
- Valide no Controller/Page para input do usuário

### Organização
- 1 classe por arquivo
- Agrupe using statements
- Ordem: System, Microsoft, Third-party, Project

## 🎨 Padrões de UI

### Blazor Components
```razor
@page "/rota"
@inject Service service

<PageTitle>Título</PageTitle>
<h1>Header</h1>

@code {
    protected override async Task OnInitializedAsync()
    {
        // Inicialização
    }
}
```

### Cores do Tema
- **Primary**: #0d6efd (azul)
- **Success**: #198754 (verde)
- **Info**: #0dcaf0 (ciano)
- **Warning**: #ffc107 (amarelo)
- **Danger**: #dc3545 (vermelho)

### Cards e Layouts
- Use Bootstrap grid system
- Mobile-first approach
- Cards para agrupamento visual
- Badges para status

## 🔐 Segurança

### Validações
- CPF: 11 dígitos, apenas números
- CRP: Formato XX/XXXXXX
- Email: Validação RFC compliant
- Telefone: Formato brasileiro

### Best Practices
- Sempre sanitize user input
- Use parameterized queries (EF Core)
- Implemente rate limiting
- Log security events

## 📝 Commits e Git

### Conventional Commits
```
feat: Nova funcionalidade
fix: Correção de bug
docs: Documentação
style: Formatação
refactor: Refatoração
test: Testes
chore: Manutenção
```

### Branches
- `main`: Produção
- `develop`: Desenvolvimento
- `feature/*`: Novas features
- `bugfix/*`: Correções
- `hotfix/*`: Correções urgentes

## 🧪 Testes

### Estrutura
```
tests/
├── ClinicaPsi.UnitTests/
├── ClinicaPsi.IntegrationTests/
└── ClinicaPsi.E2ETests/
```

### Convenções
- Nomenclatura: `MethodName_StateUnderTest_ExpectedBehavior`
- Arrange-Act-Assert pattern
- Mock external dependencies

## 📦 Dependências

### NuGet Packages
```xml
Microsoft.EntityFrameworkCore (9.0.0)
Microsoft.EntityFrameworkCore.Sqlite (9.0.0)
Bootstrap (5.3.2)
```

## 🚀 Deploy

### Desenvolvimento
```bash
dotnet run --project src/ClinicaPsi.Web
```

### Produção
```bash
dotnet publish -c Release -o ./publish
```

## 📚 Recursos Úteis

### Documentação
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core)
- [Blazor](https://docs.microsoft.com/aspnet/core/blazor)
- [EF Core](https://docs.microsoft.com/ef/core)
- [Bootstrap](https://getbootstrap.com)

### Comandos Úteis
```bash
# Criar migration
dotnet ef migrations add NomeDaMigration

# Aplicar migrations
dotnet ef database update

# Remover última migration
dotnet ef migrations remove

# Restaurar pacotes
dotnet restore

# Build
dotnet build

# Clean
dotnet clean
```

## 🎯 Próximos Passos

### Fase Atual: MVP ✅
- [x] Estrutura do projeto
- [x] Modelos de dados
- [x] DbContext configurado
- [x] Dashboard básico

### Próxima Fase: CRUD Completo
- [ ] Listagem de pacientes
- [ ] Formulário de cadastro
- [ ] Edição e exclusão
- [ ] Busca e filtros

### Futuro
- [ ] Sistema de autenticação (Identity)
- [ ] Relatórios em PDF
- [ ] Integração WhatsApp Business API
- [ ] App mobile (Blazor Hybrid / MAUI)
- [ ] Multi-tenancy

## 💡 Dicas para Copilot

### Ao criar novos componentes:
1. Sempre use o namespace correto
2. Injete serviços necessários
3. Implemente OnInitializedAsync se precisar carregar dados
4. Use try-catch para tratamento de erros
5. Adicione loading states

### Ao criar services:
1. Sempre use async/await
2. Injete apenas AppDbContext
3. Use Include() para navegação
4. Implemente tratamento de erros
5. Log operações importantes

### Ao criar páginas:
1. Defina @page no topo
2. Injete serviços necessários
3. Use componentes compartilhados
4. Implemente feedback visual
5. Mobile-first responsive

## ✅ Status Atual

**Fase**: MVP Concluído
**Versão**: 1.0.0
**Última Atualização**: 2024
**Próximo Milestone**: CRUD Completo de Pacientes

---

**Mantenha este arquivo atualizado conforme o projeto evolui!**