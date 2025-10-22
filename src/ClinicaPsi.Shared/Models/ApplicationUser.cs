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
    
    // Relacionamentos
    public int? PacienteId { get; set; }
    public virtual Paciente? Paciente { get; set; }
    
    public int? PsicologoId { get; set; }
    public virtual Psicologo? Psicologo { get; set; }
}