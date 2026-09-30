namespace ClinicaPsi.Application.Services.MercadoPago;

public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    /// <summary>Chave pública do Checkout Pro / Brick (frontend).</summary>
    public string? PublicKey { get; set; }

    /// <summary>Access Token da aplicação MP (API server-side).</summary>
    public string? AccessToken { get; set; }

    /// <summary>Segredo da assinatura de webhooks (x-signature / HMAC-SHA256).</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>URL pública do site (notification_url / back_urls).</summary>
    public string? PublicAppUrl { get; set; }

    /// <summary>Usar sandbox_init_point no checkout.</summary>
    public bool UseSandbox { get; set; }
}
