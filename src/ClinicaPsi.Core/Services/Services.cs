using ClinicaPsi.Shared.Models;
using ClinicaPsi.Shared.DTOs;

namespace ClinicaPsi.Core.Services;

// Interfaces dos repositórios (serão implementadas na camada Data)
public interface IBaseRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

public interface IPacienteRepository : IBaseRepository<Paciente>
{
    Task<Paciente?> GetByEmailAsync(string email);
    Task<Paciente?> GetByCPFAsync(string cpf);
    Task<IEnumerable<Paciente>> GetAtivosAsync();
    Task<IEnumerable<Paciente>> SearchAsync(string searchTerm);
    Task<Paciente?> GetWithConsultasAsync(int id);
}

public interface IConsultaRepository : IBaseRepository<Consulta>
{
    Task<IEnumerable<Consulta>> GetByPacienteAsync(int pacienteId);
    Task<IEnumerable<Consulta>> GetByPsicologoAsync(int psicologoId);
    Task<IEnumerable<Consulta>> GetByDataAsync(DateTime data);
    Task<IEnumerable<Consulta>> GetByPeriodoAsync(DateTime dataInicio, DateTime dataFim);
    Task<bool> ExisteConflitorHorarioAsync(int psicologoId, DateTime dataHorario, int? consultaId = null);
    Task<IEnumerable<Consulta>> GetProximasConsultasAsync(int dias = 7);
    Task<IEnumerable<Consulta>> GetConsultasComNotificacaoPendenteAsync();
}

public interface IPsicologoRepository : IBaseRepository<Psicologo>
{
    Task<Psicologo?> GetByEmailAsync(string email);
    Task<Psicologo?> GetByCRPAsync(string crp);
    Task<IEnumerable<Psicologo>> GetAtivosAsync();
}

public interface IHistoricoPontosRepository : IBaseRepository<HistoricoPontos>
{
    Task<IEnumerable<HistoricoPontos>> GetByPacienteAsync(int pacienteId);
    Task<int> GetTotalPontosPacienteAsync(int pacienteId);
}

public interface IPacienteService
{
    Task<PacienteDto?> GetByIdAsync(int id);
    Task<IEnumerable<PacienteDto>> GetAllAsync();
    Task<IEnumerable<PacienteDto>> GetAtivosAsync();
    Task<IEnumerable<PacienteDto>> SearchAsync(string searchTerm);
    Task<PacienteDto> CreateAsync(PacienteDto pacienteDto);
    Task<PacienteDto> UpdateAsync(PacienteDto pacienteDto);
    Task DeleteAsync(int id);
    Task<bool> ExistsEmailAsync(string email);
    Task<bool> ExistsCPFAsync(string cpf);
}

public class PacienteService : IPacienteService
{
    private readonly IPacienteRepository _pacienteRepository;

    public PacienteService(IPacienteRepository pacienteRepository)
    {
        _pacienteRepository = pacienteRepository;
    }

    public async Task<PacienteDto?> GetByIdAsync(int id)
    {
        var paciente = await _pacienteRepository.GetByIdAsync(id);
        return paciente != null ? MapToDto(paciente) : null;
    }

    public async Task<IEnumerable<PacienteDto>> GetAllAsync()
    {
        var pacientes = await _pacienteRepository.GetAllAsync();
        return pacientes.Select(MapToDto);
    }

    public async Task<IEnumerable<PacienteDto>> GetAtivosAsync()
    {
        var pacientes = await _pacienteRepository.GetAtivosAsync();
        return pacientes.Select(MapToDto);
    }

    public async Task<IEnumerable<PacienteDto>> SearchAsync(string searchTerm)
    {
        var pacientes = await _pacienteRepository.SearchAsync(searchTerm);
        return pacientes.Select(MapToDto);
    }

    public async Task<PacienteDto> CreateAsync(PacienteDto pacienteDto)
    {
        var paciente = MapToEntity(pacienteDto);
        paciente.DataCadastro = DateTime.Now;
        
        var createdPaciente = await _pacienteRepository.AddAsync(paciente);
        return MapToDto(createdPaciente);
    }

    public async Task<PacienteDto> UpdateAsync(PacienteDto pacienteDto)
    {
        var paciente = MapToEntity(pacienteDto);
        var updatedPaciente = await _pacienteRepository.UpdateAsync(paciente);
        return MapToDto(updatedPaciente);
    }

    public async Task DeleteAsync(int id)
    {
        await _pacienteRepository.DeleteAsync(id);
    }

    public async Task<bool> ExistsEmailAsync(string email)
    {
        var paciente = await _pacienteRepository.GetByEmailAsync(email);
        return paciente != null;
    }

    public async Task<bool> ExistsCPFAsync(string cpf)
    {
        var paciente = await _pacienteRepository.GetByCPFAsync(cpf);
        return paciente != null;
    }

    private static PacienteDto MapToDto(Paciente paciente)
    {
        return new PacienteDto
        {
            Id = paciente.Id,
            Nome = paciente.Nome,
            Email = paciente.Email,
            Telefone = paciente.Telefone,
            CPF = paciente.CPF,
            DataNascimento = paciente.DataNascimento,
            Endereco = paciente.Endereco,
            ContatoEmergencia = paciente.ContatoEmergencia,
            TelefoneEmergencia = paciente.TelefoneEmergencia,
            HistoricoMedico = paciente.HistoricoMedico,
            MedicamentosUso = paciente.MedicamentosUso,
            Observacoes = paciente.Observacoes,
            PsicoPontos = paciente.PsicoPontos,
            ConsultasRealizadas = paciente.ConsultasRealizadas,
            Ativo = paciente.Ativo
        };
    }

    private static Paciente MapToEntity(PacienteDto dto)
    {
        return new Paciente
        {
            Id = dto.Id,
            Nome = dto.Nome,
            Email = dto.Email,
            Telefone = dto.Telefone,
            CPF = dto.CPF,
            DataNascimento = dto.DataNascimento,
            Endereco = dto.Endereco,
            ContatoEmergencia = dto.ContatoEmergencia,
            TelefoneEmergencia = dto.TelefoneEmergencia,
            HistoricoMedico = dto.HistoricoMedico,
            MedicamentosUso = dto.MedicamentosUso,
            Observacoes = dto.Observacoes,
            PsicoPontos = dto.PsicoPontos,
            ConsultasRealizadas = dto.ConsultasRealizadas,
            Ativo = dto.Ativo
        };
    }
}

public interface IConsultaService
{
    Task<ConsultaDto?> GetByIdAsync(int id);
    Task<IEnumerable<ConsultaDto>> GetByPacienteAsync(int pacienteId);
    Task<IEnumerable<ConsultaDto>> GetByPsicologoAsync(int psicologoId);
    Task<IEnumerable<ConsultaDto>> GetByDataAsync(DateTime data);
    Task<IEnumerable<CalendarioEventoDto>> GetEventosCalendarioAsync(DateTime inicio, DateTime fim);
    Task<ConsultaDto> AgendarAsync(AgendarConsultaDto agendarDto);
    Task<ConsultaDto> ReagendarAsync(ReagendarConsultaDto reagendarDto);
    Task CancelarAsync(CancelarConsultaDto cancelarDto);
    Task<bool> PodeAlterarConsultaAsync(int consultaId);
    Task<IEnumerable<ConsultaDto>> GetProximasConsultasAsync(int dias = 7);
}

public class ConsultaService : IConsultaService
{
    private readonly IConsultaRepository _consultaRepository;
    private readonly IPacienteRepository _pacienteRepository;
    private readonly IPsicologoRepository _psicologoRepository;
    private readonly IPsicoPontosService _psicoPontosService;

    public ConsultaService(
        IConsultaRepository consultaRepository,
        IPacienteRepository pacienteRepository,
        IPsicologoRepository psicologoRepository,
        IPsicoPontosService psicoPontosService)
    {
        _consultaRepository = consultaRepository;
        _pacienteRepository = pacienteRepository;
        _psicologoRepository = psicologoRepository;
        _psicoPontosService = psicoPontosService;
    }

    public async Task<ConsultaDto?> GetByIdAsync(int id)
    {
        var consulta = await _consultaRepository.GetByIdAsync(id);
        return consulta != null ? MapToDto(consulta) : null;
    }

    public async Task<IEnumerable<ConsultaDto>> GetByPacienteAsync(int pacienteId)
    {
        var consultas = await _consultaRepository.GetByPacienteAsync(pacienteId);
        return consultas.Select(MapToDto);
    }

    public async Task<IEnumerable<ConsultaDto>> GetByPsicologoAsync(int psicologoId)
    {
        var consultas = await _consultaRepository.GetByPsicologoAsync(psicologoId);
        return consultas.Select(MapToDto);
    }

    public async Task<IEnumerable<ConsultaDto>> GetByDataAsync(DateTime data)
    {
        var consultas = await _consultaRepository.GetByDataAsync(data);
        return consultas.Select(MapToDto);
    }

    public async Task<IEnumerable<CalendarioEventoDto>> GetEventosCalendarioAsync(DateTime inicio, DateTime fim)
    {
        var consultas = await _consultaRepository.GetByPeriodoAsync(inicio, fim);
        return consultas.Select(MapToCalendarioEvento);
    }

    public async Task<ConsultaDto> AgendarAsync(AgendarConsultaDto agendarDto)
    {
        // Validações
        if (await _consultaRepository.ExisteConflitorHorarioAsync(agendarDto.PsicologoId, agendarDto.DataHorario))
        {
            throw new InvalidOperationException("Já existe uma consulta agendada para este horário.");
        }

        var paciente = await _pacienteRepository.GetByIdAsync(agendarDto.PacienteId);
        var psicologo = await _psicologoRepository.GetByIdAsync(agendarDto.PsicologoId);

        if (paciente == null) throw new ArgumentException("Paciente não encontrado.");
        if (psicologo == null) throw new ArgumentException("Psicólogo não encontrado.");

        // Criar consulta
        var consulta = new Consulta
        {
            PacienteId = agendarDto.PacienteId,
            PsicologoId = agendarDto.PsicologoId,
            DataHorario = agendarDto.DataHorario,
            DuracaoMinutos = agendarDto.DuracaoMinutos,
            Valor = agendarDto.ConsultaGratuita ? 0 : psicologo.ValorConsulta,
            Tipo = agendarDto.ConsultaGratuita ? TipoConsulta.Gratuita : TipoConsulta.Normal,
            Status = StatusConsulta.Agendada,
            Observacoes = agendarDto.Observacoes,
            DataAgendamento = DateTime.Now
        };

        // Se for consulta gratuita, descontar pontos
        if (agendarDto.ConsultaGratuita)
        {
            if (paciente.PsicoPontos < 10)
            {
                throw new InvalidOperationException("Paciente não possui pontos suficientes para consulta gratuita.");
            }
            
            await _psicoPontosService.DescontarPontosAsync(agendarDto.PacienteId, 10, "Resgate de consulta gratuita");
        }

        var consultaCriada = await _consultaRepository.AddAsync(consulta);
        return MapToDto(consultaCriada);
    }

    public async Task<ConsultaDto> ReagendarAsync(ReagendarConsultaDto reagendarDto)
    {
        var consulta = await _consultaRepository.GetByIdAsync(reagendarDto.ConsultaId);
        if (consulta == null)
        {
            throw new ArgumentException("Consulta não encontrada.");
        }

        if (!await PodeAlterarConsultaAsync(reagendarDto.ConsultaId))
        {
            throw new InvalidOperationException("Consulta não pode ser reagendada (menos de 24h de antecedência).");
        }

        if (await _consultaRepository.ExisteConflitorHorarioAsync(consulta.PsicologoId, reagendarDto.NovaDataHorario, consulta.Id))
        {
            throw new InvalidOperationException("Já existe uma consulta agendada para este horário.");
        }

        consulta.DataHorario = reagendarDto.NovaDataHorario;
        consulta.Status = StatusConsulta.Reagendada;
        if (!string.IsNullOrWhiteSpace(reagendarDto.Motivo))
        {
            consulta.Observacoes = $"{consulta.Observacoes}\nReagendamento: {reagendarDto.Motivo}";
        }

        var consultaAtualizada = await _consultaRepository.UpdateAsync(consulta);
        return MapToDto(consultaAtualizada);
    }

    public async Task CancelarAsync(CancelarConsultaDto cancelarDto)
    {
        var consulta = await _consultaRepository.GetByIdAsync(cancelarDto.ConsultaId);
        if (consulta == null)
        {
            throw new ArgumentException("Consulta não encontrada.");
        }

        if (!await PodeAlterarConsultaAsync(cancelarDto.ConsultaId))
        {
            throw new InvalidOperationException("Consulta não pode ser cancelada (menos de 24h de antecedência).");
        }

        consulta.Status = StatusConsulta.Cancelada;
        consulta.DataCancelamento = DateTime.Now;
        consulta.MotivoCancelamento = cancelarDto.Motivo;

        await _consultaRepository.UpdateAsync(consulta);
    }

    public async Task<bool> PodeAlterarConsultaAsync(int consultaId)
    {
        var consulta = await _consultaRepository.GetByIdAsync(consultaId);
        if (consulta == null) return false;

        return DateTime.Now <= consulta.DataHorario.AddHours(-24);
    }

    public async Task<IEnumerable<ConsultaDto>> GetProximasConsultasAsync(int dias = 7)
    {
        var consultas = await _consultaRepository.GetProximasConsultasAsync(dias);
        return consultas.Select(MapToDto);
    }

    private static ConsultaDto MapToDto(Consulta consulta)
    {
        return new ConsultaDto
        {
            Id = consulta.Id,
            PacienteId = consulta.PacienteId,
            PacienteNome = consulta.Paciente?.Nome ?? "",
            PsicologoId = consulta.PsicologoId,
            PsicologoNome = consulta.Psicologo?.Nome ?? "",
            DataHorario = consulta.DataHorario,
            DuracaoMinutos = consulta.DuracaoMinutos,
            Valor = consulta.Valor,
            Status = consulta.Status.ToString(),
            Tipo = consulta.Tipo.ToString(),
            Observacoes = consulta.Observacoes,
            DataAgendamento = consulta.DataAgendamento
        };
    }

    private static CalendarioEventoDto MapToCalendarioEvento(Consulta consulta)
    {
        var cor = consulta.Status switch
        {
            StatusConsulta.Agendada => "#28a745",
            StatusConsulta.Confirmada => "#007bff",
            StatusConsulta.Realizada => "#6c757d",
            StatusConsulta.Cancelada => "#dc3545",
            StatusConsulta.NoShow => "#ffc107",
            StatusConsulta.Reagendada => "#17a2b8",
            _ => "#6c757d"
        };

        return new CalendarioEventoDto
        {
            Id = consulta.Id,
            ConsultaId = consulta.Id,
            Title = $"{consulta.Paciente?.Nome} - {consulta.Psicologo?.Nome}",
            Start = consulta.DataHorario,
            End = consulta.DataHorario.AddMinutes(consulta.DuracaoMinutos),
            Color = cor,
            Description = consulta.Observacoes,
            PacienteNome = consulta.Paciente?.Nome ?? "",
            PsicologoNome = consulta.Psicologo?.Nome ?? "",
            Status = consulta.Status.ToString()
        };
    }
}

public interface IPsicoPontosService
{
    Task<PsicoPontosDto> GetPontosPacienteAsync(int pacienteId);
    Task AdicionarPontosAsync(int pacienteId, int pontos, string motivo, int? consultaId = null);
    Task DescontarPontosAsync(int pacienteId, int pontos, string motivo, int? consultaId = null);
    Task<bool> PodeResgatarConsultaGratuitaAsync(int pacienteId);
    Task<IEnumerable<HistoricoPontos>> GetHistoricoAsync(int pacienteId);
}

public class PsicoPontosService : IPsicoPontosService
{
    private readonly IPacienteRepository _pacienteRepository;
    private readonly IHistoricoPontosRepository _historicoPontosRepository;

    public PsicoPontosService(
        IPacienteRepository pacienteRepository,
        IHistoricoPontosRepository historicoPontosRepository)
    {
        _pacienteRepository = pacienteRepository;
        _historicoPontosRepository = historicoPontosRepository;
    }

    public async Task<PsicoPontosDto> GetPontosPacienteAsync(int pacienteId)
    {
        var paciente = await _pacienteRepository.GetByIdAsync(pacienteId);
        if (paciente == null)
        {
            throw new ArgumentException("Paciente não encontrado.");
        }

        return new PsicoPontosDto
        {
            PacienteId = paciente.Id,
            PacienteNome = paciente.Nome,
            PontosAtuais = paciente.PsicoPontos,
            ConsultasRealizadas = paciente.ConsultasRealizadas,
            ConsultasGratuitas = paciente.ConsultasGratuitas
        };
    }

    public async Task AdicionarPontosAsync(int pacienteId, int pontos, string motivo, int? consultaId = null)
    {
        var paciente = await _pacienteRepository.GetByIdAsync(pacienteId);
        if (paciente == null)
        {
            throw new ArgumentException("Paciente não encontrado.");
        }

        paciente.PsicoPontos += pontos;
        await _pacienteRepository.UpdateAsync(paciente);

        var historico = new HistoricoPontos
        {
            PacienteId = pacienteId,
            PontosAlterados = pontos,
            Motivo = motivo,
            ConsultaId = consultaId,
            DataMovimentacao = DateTime.Now
        };

        await _historicoPontosRepository.AddAsync(historico);
    }

    public async Task DescontarPontosAsync(int pacienteId, int pontos, string motivo, int? consultaId = null)
    {
        var paciente = await _pacienteRepository.GetByIdAsync(pacienteId);
        if (paciente == null)
        {
            throw new ArgumentException("Paciente não encontrado.");
        }

        if (paciente.PsicoPontos < pontos)
        {
            throw new InvalidOperationException("Paciente não possui pontos suficientes.");
        }

        paciente.PsicoPontos -= pontos;
        await _pacienteRepository.UpdateAsync(paciente);

        var historico = new HistoricoPontos
        {
            PacienteId = pacienteId,
            PontosAlterados = -pontos,
            Motivo = motivo,
            ConsultaId = consultaId,
            DataMovimentacao = DateTime.Now
        };

        await _historicoPontosRepository.AddAsync(historico);
    }

    public async Task<bool> PodeResgatarConsultaGratuitaAsync(int pacienteId)
    {
        var paciente = await _pacienteRepository.GetByIdAsync(pacienteId);
        return paciente != null && paciente.PsicoPontos >= 10;
    }

    public async Task<IEnumerable<HistoricoPontos>> GetHistoricoAsync(int pacienteId)
    {
        return await _historicoPontosRepository.GetByPacienteAsync(pacienteId);
    }
}