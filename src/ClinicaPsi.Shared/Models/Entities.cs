
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

    /// <summary>Caminho relativo da foto de perfil (ex.: /uploads/perfil/abc.jpg).</summary>
    [StringLength(300)]
    public string? FotoUrl { get; set; }

    // Sistema de pontos
    [Range(0, int.MaxValue, ErrorMessage = "PsicoPontos não pode ser negativo")]
    public int PsicoPontos { get; set; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "Consultas realizadas não pode ser negativo")]
    public int ConsultasRealizadas { get; set; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "Consultas gratuitas não pode ser negativo")]
    public int ConsultasGratuitas { get; set; } = 0;

    // Controle de sistema
    public DateTime DataCadastro { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Aceite dos Termos de Uso (UTC).</summary>
    public DateTime? AceiteTermosEm { get; set; }

    /// <summary>Aceite da Política de Privacidade (UTC).</summary>
    public DateTime? AceitePrivacidadeEm { get; set; }

    /// <summary>Consentimento para tratamento de dados de saúde / sensíveis (UTC).</summary>
    public DateTime? ConsentimentoDadosSaudeEm { get; set; }

    // Relacionamentos
    public virtual ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
    public virtual ICollection<HistoricoPontos> HistoricoPontos { get; set; } = new List<HistoricoPontos>();
}

public class Psicologo
{
    public int Id { get; set; }

    // Relacionamento com AspNetUsers
    public string? UserId { get; set; }

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

    // Períodos de atendimento
    public bool AtendeManha { get; set; } = true;
    public bool AtendeTarde { get; set; } = true;

    public DateTime DataCadastro { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Caminho relativo da foto de perfil (ex.: /uploads/perfil/abc.jpg).</summary>
    [StringLength(300)]
    public string? FotoUrl { get; set; }

    /// <summary>Soft-delete: quando preenchido, o psicólogo não reaparece na listagem nem no seed/sync.</summary>
    public DateTime? ExcluidoEm { get; set; }

    /// <summary>Validação do cadastro pelo admin (documentos CNH/CRP + contrato).</summary>
    public StatusValidacaoPsicologo StatusValidacao { get; set; } = StatusValidacaoPsicologo.Pendente;

    public DateTime? AceiteTermosEm { get; set; }
    public DateTime? AceiteContratoEm { get; set; }

    /// <summary>Aceite da Política de Privacidade (UTC).</summary>
    public DateTime? AceitePrivacidadeEm { get; set; }

    /// <summary>Valor da consulta previsto no contrato digital (padrão R$ 50).</summary>
    [Range(0, double.MaxValue)]
    public decimal ValorContratoConsulta { get; set; } = 50m;

    [StringLength(400)]
    public string? DocumentoCnhUrl { get; set; }

    [StringLength(400)]
    public string? DocumentoCrpUrl { get; set; }

    public DateTime? ValidadoEm { get; set; }

    [StringLength(450)]
    public string? ValidadoPorUserId { get; set; }

    [StringLength(1000)]
    public string? MotivoRecusa { get; set; }

    public bool PodeAtender =>
        Ativo && ExcluidoEm == null && StatusValidacao == StatusValidacaoPsicologo.Aprovado;

    // Relacionamentos
    public virtual ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
}

public enum StatusValidacaoPsicologo
{
    Pendente = 1,
    Aprovado = 2,
    Recusado = 3
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
    public FormatoConsulta Formato { get; set; } = FormatoConsulta.Presencial;

    [StringLength(100)]
    public string? VideoRoomName { get; set; }

    [StringLength(500)]
    public string? VideoRoomUrl { get; set; }

    /// <summary>Quando o psicólogo inicia "Chamar paciente"; usado para notificação in-app / polling.</summary>
    public DateTime? VideoChamadaAtivaEm { get; set; }

    [StringLength(1000, ErrorMessage = "Observações deve ter no máximo 1000 caracteres")]
    public string? Observacoes { get; set; }

    public string? RelatorioSessao { get; set; }

    // Controle de alterações
    public DateTime DataAgendamento { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public DateTime? DataCancelamento { get; set; }

    [StringLength(500, ErrorMessage = "Motivo do cancelamento deve ter no máximo 500 caracteres")]
    public string? MotivoCancelamento { get; set; }

    public bool NotificacaoEnviada { get; set; } = false;
    public bool ConfirmacaoRecebida { get; set; } = false;

    /// <summary>Status do pagamento da consulta (Mercado Pago).</summary>
    public StatusPagamento StatusPagamento { get; set; } = StatusPagamento.Pendente;

    /// <summary>Quando o pagamento foi confirmado (UTC/local conforme app).</summary>
    public DateTime? PaidAt { get; set; }

    [StringLength(100)]
    public string? MercadoPagoPreferenceId { get; set; }

    [StringLength(100)]
    public string? MercadoPagoPaymentId { get; set; }

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

    // Propriedade para compatibilidade
    public int Pontos { get; set; }

    [Required(ErrorMessage = "Motivo é obrigatório")]
    [StringLength(200, ErrorMessage = "Motivo deve ter no máximo 200 caracteres")]
    public string Motivo { get; set; } = string.Empty;

    // Propriedade para compatibilidade
    [StringLength(200, ErrorMessage = "Descrição deve ter no máximo 200 caracteres")]
    public string Descricao { get; set; } = string.Empty;

    public TipoMovimentacaoPontos TipoMovimentacao { get; set; } = TipoMovimentacaoPontos.Ganho;

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

public enum StatusPagamento
{
    Pendente = 0,
    Aguardando = 1,
    Pago = 2,
    Falhou = 3,
    Reembolsado = 4,
    Cancelado = 5
}

public enum TipoConsulta
{
    Normal = 1,
    Gratuita = 2,
    Retorno = 3,
    Avaliacao = 4
}

public enum FormatoConsulta
{
    Presencial = 1,
    Online = 2
}

public enum TipoNotificacao
{
    Email = 1,
    WhatsApp = 2,
    SMS = 3,
    Push = 4
}

public enum TipoMovimentacaoPontos
{
    Ganho = 1,      // Ganho de pontos por consulta realizada
    Uso = 2,        // Uso de pontos para consulta gratuita
    Bonus = 3,      // Bônus promocional
    Expiracao = 4   // Expiração de pontos (futuro)
}

public enum TipoUsuario
{
    Admin = 1,      // Dra. Ana Santos
    Psicologo = 2,  // Outros psicólogos
    Cliente = 3     // Pacientes/Clientes
}

public enum TipoAcaoAuditoria
{
    CriacaoUsuario = 1,
    DesativacaoUsuario = 2,
    ReativacaoUsuario = 3,
    EdicaoUsuario = 4,
    AlteracaoSenha = 5,
    AlteracaoRole = 6,
    ExclusaoUsuario = 7
}

/// <summary>
/// Registro de auditoria para rastrear todas as ações administrativas sobre usuários
/// </summary>
public class AuditoriaUsuario
{
    public int Id { get; set; }

    /// <summary>
    /// ID do administrador que realizou a ação
    /// </summary>
    [Required]
    public string AdminId { get; set; } = string.Empty;

    /// <summary>
    /// Nome do administrador
    /// </summary>
    [Required]
    [StringLength(200)]
    public string AdminNome { get; set; } = string.Empty;

    /// <summary>
    /// ID do usuário afetado pela ação
    /// </summary>
    [Required]
    public string UsuarioAfetadoId { get; set; } = string.Empty;

    /// <summary>
    /// Nome do usuário afetado
    /// </summary>
    [Required]
    [StringLength(200)]
    public string UsuarioAfetadoNome { get; set; } = string.Empty;

    /// <summary>
    /// Email do usuário afetado
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string UsuarioAfetadoEmail { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de ação realizada
    /// </summary>
    [Required]
    public TipoAcaoAuditoria Acao { get; set; }

    /// <summary>
    /// Data e hora da ação
    /// </summary>
    [Required]
    public DateTime DataHora { get; set; } = DateTime.Now;

    /// <summary>
    /// Detalhes adicionais da ação (JSON ou texto)
    /// </summary>
    public string? Detalhes { get; set; }

    /// <summary>
    /// IP do administrador que realizou a ação
    /// </summary>
    [StringLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Dados anteriores (para edições)
    /// </summary>
    public string? DadosAnteriores { get; set; }

    /// <summary>
    /// Dados novos (para edições)
    /// </summary>
    public string? DadosNovos { get; set; }
}

/// <summary>
/// Configurações do sistema
/// </summary>
public class ConfiguracaoSistema
{
    public int Id { get; set; }

    /// <summary>
    /// Chave da configuração (identificador único)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Chave { get; set; } = string.Empty;

    /// <summary>
    /// Valor da configuração
    /// </summary>
    public string? Valor { get; set; }

    /// <summary>
    /// Descrição da configuração
    /// </summary>
    [StringLength(500)]
    public string? Descricao { get; set; }

    /// <summary>
    /// Categoria da configuração (ex: Notificacoes, Backup, Sistema)
    /// </summary>
    [StringLength(50)]
    public string? Categoria { get; set; }

    /// <summary>
    /// Tipo do valor (string, boolean, number, json)
    /// </summary>
    [StringLength(20)]
    public string TipoValor { get; set; } = "string";

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime DataCriacao { get; set; } = DateTime.Now;

    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;

    /// <summary>
    /// Usuário que fez a última atualização
    /// </summary>
    [StringLength(100)]
    public string? UsuarioAtualizacao { get; set; }
}

/// <summary>
/// Prontuário Eletrônico - Registro de sessões clínicas
/// </summary>
public class ProntuarioEletronico
{
    public int Id { get; set; }

    /// <summary>
    /// ID do paciente
    /// </summary>
    [Required]
    public int PacienteId { get; set; }

    /// <summary>
    /// Paciente relacionado
    /// </summary>
    public Paciente? Paciente { get; set; }

    /// <summary>
    /// ID da consulta relacionada
    /// </summary>
    public int? ConsultaId { get; set; }

    /// <summary>
    /// Consulta relacionada
    /// </summary>
    public Consulta? Consulta { get; set; }

    /// <summary>
    /// ID do psicólogo que registrou
    /// </summary>
    [Required]
    public int PsicologoId { get; set; }

    /// <summary>
    /// Psicólogo que registrou
    /// </summary>
    public Psicologo? Psicologo { get; set; }

    /// <summary>
    /// Data e hora da sessão
    /// </summary>
    [Required]
    public DateTime DataSessao { get; set; }

    /// <summary>
    /// Tipo de atendimento
    /// </summary>
    [StringLength(50)]
    public string TipoAtendimento { get; set; } = "Individual";

    /// <summary>
    /// Queixa principal relatada pelo paciente
    /// </summary>
    [Required]
    public string QueixaPrincipal { get; set; } = string.Empty;

    /// <summary>
    /// Observações da sessão
    /// </summary>
    [Required]
    public string Observacoes { get; set; } = string.Empty;

    /// <summary>
    /// Evolução do paciente
    /// </summary>
    public string? Evolucao { get; set; }

    /// <summary>
    /// Intervenções realizadas
    /// </summary>
    public string? Intervencoes { get; set; }

    /// <summary>
    /// Plano terapêutico
    /// </summary>
    public string? PlanoTerapeutico { get; set; }

    /// <summary>
    /// Objetivos da próxima sessão
    /// </summary>
    public string? ProximaSessao { get; set; }

    /// <summary>
    /// Estado emocional observado
    /// </summary>
    [StringLength(100)]
    public string? EstadoEmocional { get; set; }

    /// <summary>
    /// Medicamentos em uso (se relatado)
    /// </summary>
    public string? MedicamentosAtuais { get; set; }

    /// <summary>
    /// Anexos (URLs ou caminhos de arquivos)
    /// </summary>
    public string? Anexos { get; set; }

    /// <summary>
    /// Sessão está finalizada
    /// </summary>
    public bool Finalizado { get; set; } = false;

    /// <summary>
    /// Data de criação do registro
    /// </summary>
    public DateTime DataCriacao { get; set; } = DateTime.Now;

    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;

    /// <summary>
    /// Confidencial - acesso restrito
    /// </summary>
    public bool Confidencial { get; set; } = true;
}

/// <summary>
/// Avaliação mútua pós-consulta: paciente avalia psicólogo e psicólogo avalia paciente.
/// </summary>
public class Avaliacao
{
    public int Id { get; set; }

    [Required]
    public int ConsultaId { get; set; }
    public virtual Consulta Consulta { get; set; } = null!;

    public int? PsicologoId { get; set; }
    public virtual Psicologo? Psicologo { get; set; }

    public int? PacienteId { get; set; }
    public virtual Paciente? Paciente { get; set; }

    /// <summary>Quem está sendo avaliado.</summary>
    public TipoAlvoAvaliacao Alvo { get; set; }

    /// <summary>UserId (AspNetUsers) de quem enviou a avaliação.</summary>
    [Required]
    [StringLength(450)]
    public string AvaliadorUserId { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Nota { get; set; }

    [StringLength(1000)]
    public string? Comentario { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public bool Publica { get; set; } = true;
}

public enum TipoAlvoAvaliacao
{
    Psicologo = 1,
    Paciente = 2
}

/// <summary>Tipo de solicitação de direitos do titular (LGPD arts. 18 e ss.).</summary>
public enum TipoSolicitacaoPrivacidade
{
    Acesso = 1,
    Exportacao = 2,
    Correcao = 3,
    Eliminacao = 4,
    Outro = 5
}

public enum StatusSolicitacaoPrivacidade
{
    Pendente = 1,
    EmAnalise = 2,
    Concluida = 3,
    Recusada = 4
}

/// <summary>Solicitação de exercício de direitos do titular de dados pessoais.</summary>
public class SolicitacaoPrivacidade
{
    public int Id { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string NomeTitular { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string EmailTitular { get; set; } = string.Empty;

    public TipoSolicitacaoPrivacidade Tipo { get; set; }

    public StatusSolicitacaoPrivacidade Status { get; set; } = StatusSolicitacaoPrivacidade.Pendente;

    [StringLength(2000)]
    public string? Detalhes { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public DateTime? DataAtualizacao { get; set; }

    [StringLength(2000)]
    public string? ObservacaoAdmin { get; set; }

    [StringLength(450)]
    public string? RespondidoPorUserId { get; set; }
}
