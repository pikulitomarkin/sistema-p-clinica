using System.ComponentModel.DataAnnotations;

namespace ClinicaPsi.Shared.Models;

public class WhatsAppSession
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string SessionName { get; set; } = "default";
    
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Desconectado"; // Conectado, Desconectado, QRCode, Erro
    
    public string? QRCode { get; set; }
    
    public DateTime? QRCodeExpiry { get; set; }
    
    public string? AuthToken { get; set; }
    
    [StringLength(50)]
    public string? PhoneNumber { get; set; }
    
    public DateTime? LastConnection { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum WhatsAppStatus
{
    Desconectado,
    Conectado,
    QRCode,
    Erro,
    Conectando
}
