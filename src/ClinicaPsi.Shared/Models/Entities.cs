using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ClinicaPsi.Shared.Models;

public class Paciente
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(100, ErrorMessage = "Nome deve ter no máximo 100 caracteres")]
    public string Nome { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Telefone é obrigatório")]
    [Phone(ErrorMessage = "Telefone inválido")]
    public string Telefone { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "CPF é obrigatório")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "CPF deve ter 11 dígitos")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "CPF deve conter apenas números")]
    public string CPF { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Data de nascimento é obrigatória")]
    public DateTime DataNascimento { get; set; }
    
    [StringLength(200, ErrorMessage = "Endereço deve ter no máximo 200 caracteres")]
    public string? Endereco { get; set; }
    
    [StringLength(100, ErrorMessage = "Contato de emergência deve ter no máximo 100 caracteres")]
    public string? ContatoEmergencia { get; set; }
    
    [Phone(ErrorMessage = "Telefone de emergência inválido")]
    public string? TelefoneEmergencia { get; set; }
    
    // Informações clínicas
    public string? HistoricoMedico { get; set; }
    public string? MedicamentosUso { get; set; }
    public string? Observacoes { get; set; }
    
    // Sistema de pontos
    [Range(0, int.MaxValue, ErrorMessage = "PsicoPontos não pode ser negativo")]
    public int PsicoPontos { get; set; } = 0;
    
    [Range(0, int.MaxValue, ErrorMessage = "Consultas realizadas não pode ser negativo")]
    public int ConsultasRealizadas { get; set; } = 0;
    
    [Range(0, int.MaxValue, ErrorMessage = "Consultas gratuitas não pode ser negativo")]
    public int ConsultasGratuitas { get; set; } = 0;
    
    // Controle de sistema
    public DateTime DataCadastro { get; set; }
    public bool Ativo { get; set; } = true;
    
    // Relacionamentos
    public virtual ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
    public virtual ICollection<HistoricoPontos> HistoricoPontos { get; set; } = new List<HistoricoPontos>();
}

public class Psicologo
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(100, ErrorMessage = "Nome deve ter no máximo 100 caracteres")]
    public string Nome { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "CRP é obrigatório")]
    [StringLength(20, ErrorMessage = "CRP deve ter no máximo 20 caracteres")]
    public string CRP { get; set; } = string.Empty;
    
    [Phone(ErrorMessage = "Telefone inválido")]
    public string? Telefone { get; set; }
    
    [StringLength(500, ErrorMessage = "Especialidades deve ter no máximo 500 caracteres")]
    public string? Especialidades { get; set; }
    
    [Range(0, double.MaxValue, ErrorMessage = "Valor da consulta deve ser positivo")]
    public decimal ValorConsulta { get; set; }
    
    // Configurações de horário
    public TimeSpan HorarioInicioManha { get; set; } = new TimeSpan(8, 0, 0);
    public TimeSpan HorarioFimManha { get; set; } = new TimeSpan(12, 0, 0);
    public TimeSpan HorarioInicioTarde { get; set; } = new TimeSpan(14, 0, 0);
    public TimeSpan HorarioFimTarde { get; set; } = new TimeSpan(18, 0, 0);
    
    public bool AtendeSegunda { get; set; } = true;
    public bool AtendeTerca { get; set; } = true;
    public bool AtendeQuarta { get; set; } = true;
    public bool AtendeQuinta { get; set; } = true;
    public bool AtendeSexta { get; set; } = true;
    public bool AtendeSabado { get; set; } = false;
    public bool AtendeDomingo { get; set; } = false;
    
    public DateTime DataCadastro { get; set; }
    public bool Ativo { get; set; } = true;
    
    // Relacionamentos
    public virtual ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
}

public class Consulta
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Paciente é obrigatório")]
    public int PacienteId { get; set; }
    public virtual Paciente Paciente { get; set; } = null!;
    
    [Required(ErrorMessage = "Psicólogo é obrigatório")]
    public int PsicologoId { get; set; }
    public virtual Psicologo Psicologo { get; set; } = null!;
    
    [Required(ErrorMessage = "Data e horário são obrigatórios")]
    public DateTime DataHorario { get; set; }
    
    [Range(15, 180, ErrorMessage = "Duração deve estar entre 15 e 180 minutos")]
    public int DuracaoMinutos { get; set; } = 50;
    
    [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser positivo")]
    public decimal Valor { get; set; }
    
    public StatusConsulta Status { get; set; } = StatusConsulta.Agendada;
    public TipoConsulta Tipo { get; set; } = TipoConsulta.Normal;
    
    [StringLength(1000, ErrorMessage = "Observações deve ter no máximo 1000 caracteres")]
    public string? Observacoes { get; set; }
    
    public string? RelatorioSessao { get; set; }
    
    // Controle de alterações
    public DateTime DataAgendamento { get; set; }
    public DateTime? DataCancelamento { get; set; }
    
    [StringLength(500, ErrorMessage = "Motivo do cancelamento deve ter no máximo 500 caracteres")]
    public string? MotivoCancelamento { get; set; }
    
    public bool NotificacaoEnviada { get; set; } = false;
    public bool ConfirmacaoRecebida { get; set; } = false;
    
    // Relacionamentos
    public virtual ICollection<NotificacaoConsulta> Notificacoes { get; set; } = new List<NotificacaoConsulta>();
}

public class HistoricoPontos
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Paciente é obrigatório")]
    public int PacienteId { get; set; }
    public virtual Paciente Paciente { get; set; } = null!;
    
    public int PontosAlterados { get; set; }
    
    [Required(ErrorMessage = "Motivo é obrigatório")]
    [StringLength(200, ErrorMessage = "Motivo deve ter no máximo 200 caracteres")]
    public string Motivo { get; set; } = string.Empty;
    
    public DateTime DataMovimentacao { get; set; }
    
    public int? ConsultaId { get; set; }
    public virtual Consulta? Consulta { get; set; }
}

public class NotificacaoConsulta
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Consulta é obrigatória")]
    public int ConsultaId { get; set; }
    public virtual Consulta Consulta { get; set; } = null!;
    
    public TipoNotificacao Tipo { get; set; }
    
    [Required(ErrorMessage = "Destinatário é obrigatório")]
    [StringLength(100, ErrorMessage = "Destinatário deve ter no máximo 100 caracteres")]
    public string Destinatario { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Assunto é obrigatório")]
    [StringLength(200, ErrorMessage = "Assunto deve ter no máximo 200 caracteres")]
    public string Assunto { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Conteúdo é obrigatório")]
    public string Conteudo { get; set; } = string.Empty;
    
    public DateTime DataEnvio { get; set; }
    public bool Enviada { get; set; } = false;
    
    [StringLength(500, ErrorMessage = "Erro de envio deve ter no máximo 500 caracteres")]
    public string? ErroEnvio { get; set; }
}

// Enums
public enum StatusConsulta
{
    Agendada = 1,
    Confirmada = 2,
    Realizada = 3,
    Cancelada = 4,
    NoShow = 5,
    Reagendada = 6
}

public enum TipoConsulta
{
    Normal = 1,
    Gratuita = 2,
    Retorno = 3,
    Avaliacao = 4
}

public enum TipoNotificacao
{
    Email = 1,
    WhatsApp = 2,
    SMS = 3,
    Push = 4
}