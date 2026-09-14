namespace ClinicaPsi.Application.Services.Email;

public class EmailSendResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ProviderId { get; init; }

    public static EmailSendResult Ok(string? providerId = null) =>
        new() { Success = true, ProviderId = providerId };

    public static EmailSendResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public interface IEmailService
{
    bool IsConfigured { get; }
    string EffectiveFrom { get; }
    Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
