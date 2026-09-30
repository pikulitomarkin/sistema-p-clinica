namespace ClinicaPsi.Application.Services.MercadoPago;

/// <summary>
/// Credenciais Mercado Pago — apenas via env/secret (nunca no git).
/// Para produção: troque AccessToken/PublicKey pelas credenciais de produção e UseSandbox=false.
/// </summary>
public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    /// <summary>Chave pública do Checkout / Brick (frontend).</summary>
    public string? PublicKey { get; set; }

    /// <summary>Access Token da aplicação MP (API server-side).</summary>
    public string? AccessToken { get; set; }

    /// <summary>Segredo da assinatura de webhooks (x-signature). Opcional em teste.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>URL pública do site (notification_url / back_urls).</summary>
    public string? PublicAppUrl { get; set; }

    /// <summary>true = credenciais de teste / sandbox_init_point.</summary>
    public bool UseSandbox { get; set; } = true;
}
