using System.ComponentModel.DataAnnotations;

namespace ClinicaPsi.Shared.DTOs;

public class PacienteDto
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(100, ErrorMessage = "Nome deve ter no máximo 100 caracteres")]
    public string Nome { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Telefone é obrigatório")]
    public string Telefone { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "CPF é obrigatório")]
    public string CPF { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Data de nascimento é obrigatória")]
    public DateTime DataNascimento { get; set; }
    
    public string? Endereco { get; set; }
    public string? ContatoEmergencia { get; set; }
    public string? TelefoneEmergencia { get; set; }
    public string? HistoricoMedico { get; set; }
    public string? MedicamentosUso { get; set; }
    public string? Observacoes { get; set; }
    
    public int PsicoPontos { get; set; }
    public int ConsultasRealizadas { get; set; }
    public bool Ativo { get; set; }
    
    public int Idade => DateTime.Now.Year - DataNascimento.Year;
}

public class ConsultaDto
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public string PacienteNome { get; set; } = string.Empty;
    public int PsicologoId { get; set; }
    public string PsicologoNome { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Data e horário são obrigatórios")]
    public DateTime DataHorario { get; set; }
    
    public int DuracaoMinutos { get; set; } = 50;
    public decimal Valor { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Observacoes { get; set; }
    
    public DateTime DataAgendamento { get; set; }
    public bool PodeAlterar => DateTime.Now <= DataHorario.AddHours(-24);
}

public class AgendarConsultaDto
{
    [Required(ErrorMessage = "Paciente é obrigatório")]
    public int PacienteId { get; set; }
    
    [Required(ErrorMessage = "Psicólogo é obrigatório")]
    public int PsicologoId { get; set; }
    
    [Required(ErrorMessage = "Data e horário são obrigatórios")]
    public DateTime DataHorario { get; set; }
    
    public int DuracaoMinutos { get; set; } = 50;
    public bool ConsultaGratuita { get; set; } = false;
    public string? Observacoes { get; set; }
}

public class ReagendarConsultaDto
{
    [Required]
    public int ConsultaId { get; set; }
    
    [Required(ErrorMessage = "Nova data e horário são obrigatórios")]
    public DateTime NovaDataHorario { get; set; }
    
    public string? Motivo { get; set; }
}

public class CancelarConsultaDto
{
    [Required]
    public int ConsultaId { get; set; }
    
    [Required(ErrorMessage = "Motivo do cancelamento é obrigatório")]
    [StringLength(500, ErrorMessage = "Motivo deve ter no máximo 500 caracteres")]
    public string Motivo { get; set; } = string.Empty;
}

public class PsicoPontosDto
{
    public int PacienteId { get; set; }
    public string PacienteNome { get; set; } = string.Empty;
    public int PontosAtuais { get; set; }
    public int ConsultasRealizadas { get; set; }
    public int ConsultasGratuitas { get; set; }
    public bool PodeResgatarConsulta => PontosAtuais >= 10;
    public int PontosParaProximaGratuita => Math.Max(0, 10 - PontosAtuais);
}

public class CalendarioEventoDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Color { get; set; } = "#007bff";
    public string? Description { get; set; }
    public bool AllDay { get; set; } = false;
    
    // Dados específicos da consulta
    public int ConsultaId { get; set; }
    public string PacienteNome { get; set; } = string.Empty;
    public string PsicologoNome { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DashboardDto
{
    public int TotalPacientes { get; set; }
    public int ConsultasHoje { get; set; }
    public int ConsultasSemana { get; set; }
    public int ConsultasMes { get; set; }
    public decimal FaturamentoMes { get; set; }
    public int PacientesAtivos { get; set; }
    
    public List<ConsultaDto> ProximasConsultas { get; set; } = new();
    public List<PacienteDto> UltimosPacientes { get; set; } = new();
    
    // Estatísticas para gráficos
    public Dictionary<string, int> ConsultasPorMes { get; set; } = new();
    public Dictionary<string, decimal> FaturamentoPorMes { get; set; } = new();
}

public class NotificacaoDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTime DataEnvio { get; set; }
    public bool Lida { get; set; } = false;
    public string? Link { get; set; }
}