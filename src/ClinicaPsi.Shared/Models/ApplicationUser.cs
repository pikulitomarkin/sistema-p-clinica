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
    
    // Relacionamentos
    public int? PacienteId { get; set; }
    public virtual Paciente? Paciente { get; set; }
    
    public int? PsicologoId { get; set; }
    public virtual Psicologo? Psicologo { get; set; }
}