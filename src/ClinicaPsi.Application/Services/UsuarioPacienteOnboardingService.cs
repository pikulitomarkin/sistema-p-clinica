using System.Security.Cryptography;
using ClinicaPsi.Application.Services.Email;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicaPsi.Application.Services;

public class OnboardingEmailResult
{
    public bool UsuarioCriadoOuAtualizado { get; init; }
    public bool EmailEnviado { get; init; }
    public string? SenhaProvisoria { get; init; }
    public string? EmailErro { get; init; }
    public string? LoginEmail { get; init; }
    public ApplicationUser? Usuario { get; init; }
}

public class UsuarioPacienteOnboardingService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;
    private readonly ILogger<UsuarioPacienteOnboardingService> _logger;

    public UsuarioPacienteOnboardingService(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration configuration,
        IOptionsMonitor<EmailOptions> emailOptions,
        ILogger<UsuarioPacienteOnboardingService> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _configuration = configuration;
        _emailOptions = emailOptions;
        _logger = logger;
    }

    public async Task<OnboardingEmailResult> GarantirAcessoClienteEEnviarBoasVindasAsync(
        Paciente paciente, CancellationToken cancellationToken = default)
    {
        if (paciente.Id <= 0)
            throw new ArgumentException("Paciente precisa estar persistido.", nameof(paciente));
        if (string.IsNullOrWhiteSpace(paciente.Email))
            return new OnboardingEmailResult { EmailErro = "Paciente sem e-mail." };

        var emailLogin = paciente.Email.Trim().ToLowerInvariant();
        var senha = GerarSenhaTemporaria();
        var user = await _userManager.FindByEmailAsync(emailLogin);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = emailLogin,
                Email = emailLogin,
                NomeCompleto = paciente.Nome,
                TipoUsuario = TipoUsuario.Cliente,
                CPF = paciente.CPF,
                PacienteId = paciente.Id,
                EmailConfirmed = true,
                Ativo = paciente.Ativo,
                MustChangePassword = true,
                DataCadastro = DateTime.UtcNow,
                PhoneNumber = string.IsNullOrWhiteSpace(paciente.Telefone) ? null : paciente.Telefone
            };
            var create = await _userManager.CreateAsync(user, senha);
            if (!create.Succeeded)
            {
                var errs = string.Join("; ", create.Errors.Select(e => e.Description));
                _logger.LogError("Falha ao criar usuário Cliente para paciente {Id}: {Errors}", paciente.Id, errs);
                return new OnboardingEmailResult { EmailErro = errs, LoginEmail = emailLogin };
            }
            await GarantirRoleClienteAsync(user);
        }
        else
        {
            if (user.TipoUsuario != TipoUsuario.Cliente && user.TipoUsuario != TipoUsuario.Admin)
            {
                return new OnboardingEmailResult
                {
                    EmailErro = $"O e-mail já está em uso por um usuário do tipo {user.TipoUsuario}.",
                    LoginEmail = emailLogin, Usuario = user
                };
            }
            if (user.TipoUsuario != TipoUsuario.Admin)
            {
                user.TipoUsuario = TipoUsuario.Cliente;
                user.PacienteId = paciente.Id;
                if (string.IsNullOrWhiteSpace(user.NomeCompleto)) user.NomeCompleto = paciente.Nome;
                if (string.IsNullOrWhiteSpace(user.CPF) && !string.IsNullOrWhiteSpace(paciente.CPF)) user.CPF = paciente.CPF;
                user.MustChangePassword = true;
                user.Ativo = true;
                await _userManager.UpdateAsync(user);
                await GarantirRoleClienteAsync(user);
            }
            else if (!user.PacienteId.HasValue)
            {
                user.PacienteId = paciente.Id;
                await _userManager.UpdateAsync(user);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await _userManager.ResetPasswordAsync(user, token, senha);
            if (!reset.Succeeded)
            {
                var errs = string.Join("; ", reset.Errors.Select(e => e.Description));
                return new OnboardingEmailResult { UsuarioCriadoOuAtualizado = true, EmailErro = errs, LoginEmail = emailLogin, Usuario = user };
            }
            if (!user.MustChangePassword && user.TipoUsuario == TipoUsuario.Cliente)
            {
                user.MustChangePassword = true;
                await _userManager.UpdateAsync(user);
            }
        }

        var baseUrl = ResolvePublicBaseUrl();
        var html = EmailTemplates.BoasVindasPaciente(paciente.Nome, emailLogin, senha, $"{baseUrl}/Account/Login", $"{baseUrl}/Account/ChangePassword");
        var send = await _emailService.SendAsync(emailLogin, "Bem-vindo(a) — acesso à área do paciente | Psicóloga Ana Santos", html, cancellationToken);
        if (!send.Success)
            _logger.LogWarning("Paciente {Id} com acesso criado, mas e-mail falhou: {Erro}", paciente.Id, send.ErrorMessage);

        return new OnboardingEmailResult
        {
            UsuarioCriadoOuAtualizado = true,
            EmailEnviado = send.Success,
            SenhaProvisoria = senha,
            EmailErro = send.Success ? null : send.ErrorMessage,
            LoginEmail = emailLogin,
            Usuario = user
        };
    }

    public async Task<EmailSendResult> EnviarBoasVindasPsicologoAsync(ApplicationUser user, string? senhaProvisoria, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return EmailSendResult.Fail("Usuário sem e-mail.");
        var loginUrl = $"{ResolvePublicBaseUrl()}/Account/Login";
        var html = EmailTemplates.BoasVindasPsicologo(user.NomeCompleto, user.Email, senhaProvisoria, loginUrl);
        return await _emailService.SendAsync(user.Email, "Bem-vindo(a) à equipe | Psicóloga Ana Santos", html, cancellationToken);
    }

    /// <summary>
    /// Garante AspNetUser Cliente vinculado ao paciente (sem e-mail de boas-vindas).
    /// Usado no ForgotPassword quando há Paciente sem conta de login.
    /// </summary>
    public async Task<(ApplicationUser? User, string? Error)> GarantirUsuarioClienteSemEmailAsync(
        Paciente paciente, CancellationToken cancellationToken = default)
    {
        if (paciente.Id <= 0)
            return (null, "Paciente inválido.");
        if (string.IsNullOrWhiteSpace(paciente.Email))
            return (null, "Paciente sem e-mail.");

        var emailLogin = paciente.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(emailLogin);
        if (user != null)
        {
            if (user.TipoUsuario != TipoUsuario.Cliente && user.TipoUsuario != TipoUsuario.Admin)
                return (null, $"E-mail em uso por {user.TipoUsuario}.");
            if (!user.PacienteId.HasValue && user.TipoUsuario == TipoUsuario.Cliente)
            {
                user.PacienteId = paciente.Id;
                await _userManager.UpdateAsync(user);
            }
            return (user, null);
        }

        var senha = GerarSenhaTemporaria();
        user = new ApplicationUser
        {
            UserName = emailLogin,
            Email = emailLogin,
            NomeCompleto = paciente.Nome,
            TipoUsuario = TipoUsuario.Cliente,
            CPF = paciente.CPF,
            PacienteId = paciente.Id,
            EmailConfirmed = true,
            Ativo = paciente.Ativo,
            MustChangePassword = false,
            DataCadastro = DateTime.UtcNow,
            PhoneNumber = string.IsNullOrWhiteSpace(paciente.Telefone) ? null : paciente.Telefone
        };
        var create = await _userManager.CreateAsync(user, senha);
        if (!create.Succeeded)
        {
            var errs = string.Join("; ", create.Errors.Select(e => e.Description));
            _logger.LogError("ForgotPassword: falha ao criar Cliente para paciente {Id}: {Errors}", paciente.Id, errs);
            return (null, errs);
        }
        await GarantirRoleClienteAsync(user);
        _logger.LogInformation("ForgotPassword: conta Cliente criada para paciente {Id} ({Email})", paciente.Id, emailLogin);
        return (user, null);
    }

    public async Task<EmailSendResult> EnviarResetSenhaAsync(ApplicationUser user, string resetUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return EmailSendResult.Fail("Usuário sem e-mail.");
        return await _emailService.SendAsync(user.Email, "Redefinição de senha | Psicóloga Ana Santos", EmailTemplates.ResetSenha(user.NomeCompleto, resetUrl), cancellationToken);
    }

    public string ResolvePublicBaseUrl()
    {
        var url = FirstNonEmpty(_emailOptions.CurrentValue.PublicAppUrl, _configuration["PUBLIC_APP_URL"], _configuration["Email:PublicAppUrl"], _configuration["WhatsApp:SiteUrl"], "https://psiianasantos.com.br");
        return url!.TrimEnd('/');
    }

    private async Task GarantirRoleClienteAsync(ApplicationUser user)
    {
        if (!await _userManager.IsInRoleAsync(user, "Cliente"))
            await _userManager.AddToRoleAsync(user, "Cliente");
    }

    public static string GerarSenhaTemporaria()
    {
        const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var body = new char[12];
        for (var i = 0; i < 12; i++) body[i] = chars[bytes[i] % chars.Length];
        return $"Tmp{new string(body)}1!";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        return null;
    }
}
