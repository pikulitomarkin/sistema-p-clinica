using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ClinicaPsi.Shared.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string NomeCompleto { get; set; } = string.Empty;
    
    [Required]
    public TipoUsuario TipoUsuario { get; set; }
    
    [StringLength(20)]
    public string? CPF { get; set; }
    
    [StringLength(20)]
    public string? CRP { get; set; } // Para psicólogos
    
    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Quando true, o usuário deve trocar a senha no próximo login (senha provisória).
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Quando true, o tour guiado de primeiro acesso foi concluído ou dispensado.
    /// </summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>Caminho relativo da foto de perfil (ex.: /uploads/perfil/abc.jpg).</summary>
    [StringLength(300)]
    public string? FotoUrl { get; set; }

    /// <summary>Timestamp do aceite dos Termos de Uso (LGPD / prova de consentimento).</summary>
    public DateTime? AceiteTermosEm { get; set; }

    /// <summary>Timestamp do aceite da Política de Privacidade.</summary>
    public DateTime? AceitePrivacidadeEm { get; set; }

    /// <summary>Consentimento para tratamento de dados de saúde / sensíveis (art. 11 LGPD), quando aplicável.</summary>
    public DateTime? ConsentimentoDadosSaudeEm { get; set; }

    /// <summary>Preferência de cookies analíticos (opcional). Essenciais não dependem deste flag.</summary>
    public bool? CookieAnalyticsAceito { get; set; }

    public DateTime? CookieConsentimentoEm { get; set; }
    
    // Relacionamentos
    public int? PacienteId { get; set; }
    public virtual Paciente? Paciente { get; set; }
    
    public int? PsicologoId { get; set; }
    public virtual Psicologo? Psicologo { get; set; }
}