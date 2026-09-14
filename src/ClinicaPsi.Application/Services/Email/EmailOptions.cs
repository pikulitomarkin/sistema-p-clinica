namespace ClinicaPsi.Application.Services.Email;

/// <summary>
/// Opções de e-mail (Resend). Preferir variáveis de ambiente / secrets do container.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Chave da API Resend (RESEND_API_KEY ou Email:ApiKey). Nunca versionar.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Endereço From, ex.: noreply@psiianasantos.com.br</summary>
    public string From { get; set; } = "onboarding@resend.dev";

    /// <summary>Nome exibido no From</summary>
    public string FromName { get; set; } = "Psicóloga Ana Santos";

    /// <summary>URL pública do app (links nos e-mails)</summary>
    public string? PublicAppUrl { get; set; }
}
