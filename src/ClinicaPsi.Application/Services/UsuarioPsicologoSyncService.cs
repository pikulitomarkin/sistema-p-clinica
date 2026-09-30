using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Mantém ApplicationUser (Identity) e entidade Psicologo sincronizados:
/// TipoUsuario=Psicologo, role Psicologo, PsicologoId ↔ UserId, status Ativo.
/// </summary>
public class UsuarioPsicologoSyncService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UsuarioPsicologoSyncService> _logger;

    public UsuarioPsicologoSyncService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<UsuarioPsicologoSyncService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public class SyncResult
    {
        public int VinculosAtualizados { get; set; }
        public int UsuariosCriados { get; set; }
        public int PsicologosCriados { get; set; }
        public int StatusSincronizados { get; set; }
        public List<string> SenhasTemporarias { get; set; } = new();

        public bool TeveAlteracoes =>
            VinculosAtualizados > 0 || UsuariosCriados > 0 || PsicologosCriados > 0 || StatusSincronizados > 0;
    }

    /// <summary>Emails de seed/demo que nunca devem ser recriados pelo sync.</summary>
    private static readonly HashSet<string> EmailsDemoBloqueados = new(StringComparer.OrdinalIgnoreCase)
    {
        "joao.silva@clinicapsi.com",
        "maria.santos@clinicapsi.com",
        "joao.silva@psii.com",
        "maria.santos@psii.com"
    };

    public async Task<SyncResult> SincronizarTodosAsync(CancellationToken ct = default)
    {
        var result = new SyncResult();
        var todosPsicologos = await _context.Psicologos.ToListAsync(ct);
        var psicologos = todosPsicologos.Where(p => p.ExcluidoEm == null).ToList();
        var emailsExcluidos = todosPsicologos
            .Where(p => p.ExcluidoEm != null && !string.IsNullOrWhiteSpace(p.Email))
            .Select(p => p.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usuarios = await _userManager.Users.ToListAsync(ct);

        foreach (var psicologo in psicologos)
        {
            var usuario = EncontrarUsuarioParaPsicologo(psicologo, usuarios);
            if (usuario == null)
            {
                var (novo, senha) = await CriarUsuarioParaPsicologoAsync(psicologo);
                if (novo != null)
                {
                    usuarios.Add(novo);
                    result.UsuariosCriados++;
                    if (!string.IsNullOrEmpty(senha))
                        result.SenhasTemporarias.Add($"{novo.Email}: {senha}");
                    usuario = novo;
                }
            }

            if (usuario == null)
                continue;

            if (await GarantirVinculoERoleAsync(usuario, psicologo))
                result.VinculosAtualizados++;
            if (await SincronizarStatusEntreAsync(usuario, psicologo))
                result.StatusSincronizados++;
        }

        foreach (var usuario in usuarios.Where(u => u.TipoUsuario == TipoUsuario.Psicologo).ToList())
        {
            Psicologo? psicologo = null;
            if (usuario.PsicologoId.HasValue)
                psicologo = todosPsicologos.FirstOrDefault(p => p.Id == usuario.PsicologoId.Value);

            psicologo ??= todosPsicologos.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.UserId) &&
                string.Equals(p.UserId, usuario.Id, StringComparison.OrdinalIgnoreCase));

            psicologo ??= todosPsicologos.FirstOrDefault(p =>
                !string.IsNullOrEmpty(usuario.Email) &&
                string.Equals(p.Email, usuario.Email, StringComparison.OrdinalIgnoreCase));

            if (psicologo == null && !string.IsNullOrEmpty(usuario.CRP))
            {
                psicologo = todosPsicologos.FirstOrDefault(p =>
                    string.Equals(p.CRP, usuario.CRP, StringComparison.OrdinalIgnoreCase));
            }

            // Soft-deleted ou email demo: não recriar registro "fantasma"
            if (psicologo != null && psicologo.ExcluidoEm != null)
            {
                if (usuario.Ativo)
                {
                    usuario.Ativo = false;
                    await _userManager.UpdateAsync(usuario);
                    result.StatusSincronizados++;
                }
                continue;
            }

            if (psicologo == null)
            {
                var emailCanonico = ExtrairEmailCanonico(usuario.Email);
                if (EmailsDemoBloqueados.Contains(emailCanonico) || emailsExcluidos.Contains(emailCanonico))
                {
                    _logger.LogInformation(
                        "Sync: não recria psicólogo para {Email} (demo ou já excluído)",
                        emailCanonico);
                    continue;
                }

                psicologo = await CriarPsicologoParaUsuarioAsync(usuario);
                if (psicologo != null)
                {
                    todosPsicologos.Add(psicologo);
                    psicologos.Add(psicologo);
                    result.PsicologosCriados++;
                }
            }

            if (psicologo == null)
                continue;

            if (await GarantirVinculoERoleAsync(usuario, psicologo))
                result.VinculosAtualizados++;
            if (await SincronizarStatusEntreAsync(usuario, psicologo))
                result.StatusSincronizados++;
        }

        if (result.TeveAlteracoes)
        {
            _logger.LogInformation(
                "Sync usuários↔psicólogos: vínculos={V}, users={U}, psicólogos={P}, status={S}",
                result.VinculosAtualizados, result.UsuariosCriados, result.PsicologosCriados, result.StatusSincronizados);
        }

        return result;
    }

    public async Task VincularAsync(ApplicationUser usuario, Psicologo psicologo)
    {
        await GarantirVinculoERoleAsync(usuario, psicologo);
        await SincronizarStatusEntreAsync(usuario, psicologo);
    }

    public async Task SincronizarStatusPorPsicologoIdAsync(int psicologoId, bool ativo)
    {
        var psicologo = await _context.Psicologos.FindAsync(psicologoId);
        if (psicologo == null) return;

        // Excluídos permanecem inativos e fora da listagem
        if (psicologo.ExcluidoEm != null)
            ativo = false;

        psicologo.Ativo = ativo;
        psicologo.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var usuario = await EncontrarUsuarioAsync(psicologo);
        if (usuario != null && usuario.Ativo != ativo)
        {
            usuario.Ativo = ativo;
            await _userManager.UpdateAsync(usuario);
        }
    }

    private static string ExtrairEmailCanonico(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        if (email.StartsWith("psicologo.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = email.Split('.', 3);
            if (parts.Length == 3)
                return parts[2];
        }

        return email;
    }

    public async Task SincronizarStatusPorUsuarioAsync(ApplicationUser usuario, bool ativo)
    {
        usuario.Ativo = ativo;
        await _userManager.UpdateAsync(usuario);

        if (usuario.TipoUsuario != TipoUsuario.Psicologo)
            return;

        Psicologo? psicologo = null;
        if (usuario.PsicologoId.HasValue)
            psicologo = await _context.Psicologos.FindAsync(usuario.PsicologoId.Value);

        psicologo ??= await _context.Psicologos.FirstOrDefaultAsync(p => p.UserId == usuario.Id);

        if (psicologo != null && psicologo.Ativo != ativo)
        {
            psicologo.Ativo = ativo;
            psicologo.DataAtualizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SincronizarDadosUsuarioApartirDePsicologoAsync(Psicologo psicologo)
    {
        var usuario = await EncontrarUsuarioAsync(psicologo);
        if (usuario == null) return;

        var alterou = false;

        if (!string.Equals(usuario.NomeCompleto, psicologo.Nome, StringComparison.Ordinal))
        {
            usuario.NomeCompleto = psicologo.Nome;
            alterou = true;
        }

        if (!string.IsNullOrWhiteSpace(psicologo.Email) &&
            usuario.TipoUsuario == TipoUsuario.Psicologo &&
            usuario.Email?.StartsWith("psicologo.", StringComparison.OrdinalIgnoreCase) != true &&
            !string.Equals(usuario.Email, psicologo.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailEmUso = await _userManager.FindByEmailAsync(psicologo.Email);
            if (emailEmUso == null || emailEmUso.Id == usuario.Id)
            {
                usuario.Email = psicologo.Email;
                usuario.UserName = psicologo.Email;
                usuario.NormalizedEmail = psicologo.Email.ToUpperInvariant();
                usuario.NormalizedUserName = psicologo.Email.ToUpperInvariant();
                alterou = true;
            }
        }

        if (!string.Equals(usuario.CRP, psicologo.CRP, StringComparison.Ordinal))
        {
            usuario.CRP = psicologo.CRP;
            alterou = true;
        }

        if (usuario.Ativo != psicologo.Ativo)
        {
            usuario.Ativo = psicologo.Ativo;
            alterou = true;
        }

        if (usuario.TipoUsuario != TipoUsuario.Psicologo && usuario.TipoUsuario != TipoUsuario.Admin)
        {
            usuario.TipoUsuario = TipoUsuario.Psicologo;
            alterou = true;
        }

        if (usuario.TipoUsuario == TipoUsuario.Psicologo && usuario.PsicologoId != psicologo.Id)
        {
            usuario.PsicologoId = psicologo.Id;
            alterou = true;
        }

        if (alterou)
            await _userManager.UpdateAsync(usuario);

        if (usuario.TipoUsuario == TipoUsuario.Psicologo && psicologo.UserId != usuario.Id)
        {
            psicologo.UserId = usuario.Id;
            await _context.SaveChangesAsync();
        }

        if (usuario.TipoUsuario == TipoUsuario.Psicologo &&
            !await _userManager.IsInRoleAsync(usuario, "Psicologo"))
        {
            await _userManager.AddToRoleAsync(usuario, "Psicologo");
        }
    }

    private ApplicationUser? EncontrarUsuarioParaPsicologo(Psicologo psicologo, List<ApplicationUser> usuarios)
    {
        if (!string.IsNullOrEmpty(psicologo.UserId))
        {
            var byUserId = usuarios.FirstOrDefault(u =>
                string.Equals(u.Id, psicologo.UserId, StringComparison.OrdinalIgnoreCase));
            if (byUserId != null) return byUserId;
        }

        var byPsicologoId = usuarios.FirstOrDefault(u =>
            u.TipoUsuario == TipoUsuario.Psicologo && u.PsicologoId == psicologo.Id);
        if (byPsicologoId != null) return byPsicologoId;

        if (!string.IsNullOrEmpty(psicologo.Email))
        {
            var byEmail = usuarios.FirstOrDefault(u =>
                u.TipoUsuario == TipoUsuario.Psicologo &&
                string.Equals(u.Email, psicologo.Email, StringComparison.OrdinalIgnoreCase));
            if (byEmail != null) return byEmail;

            var dedicated = usuarios.FirstOrDefault(u =>
                u.TipoUsuario == TipoUsuario.Psicologo &&
                string.Equals(u.Email, $"psicologo.{psicologo.Id}.{psicologo.Email}", StringComparison.OrdinalIgnoreCase));
            if (dedicated != null) return dedicated;
        }

        if (!string.IsNullOrEmpty(psicologo.CRP))
        {
            return usuarios.FirstOrDefault(u =>
                u.TipoUsuario == TipoUsuario.Psicologo &&
                !string.IsNullOrEmpty(u.CRP) &&
                string.Equals(u.CRP, psicologo.CRP, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private async Task<ApplicationUser?> EncontrarUsuarioAsync(Psicologo psicologo)
    {
        if (!string.IsNullOrEmpty(psicologo.UserId))
        {
            var byId = await _userManager.FindByIdAsync(psicologo.UserId);
            if (byId != null) return byId;
        }

        var byPsicologoId = await _userManager.Users
            .FirstOrDefaultAsync(u => u.TipoUsuario == TipoUsuario.Psicologo && u.PsicologoId == psicologo.Id);
        if (byPsicologoId != null) return byPsicologoId;

        if (!string.IsNullOrEmpty(psicologo.Email))
        {
            var byEmail = await _userManager.Users
                .FirstOrDefaultAsync(u => u.TipoUsuario == TipoUsuario.Psicologo && u.Email == psicologo.Email);
            if (byEmail != null) return byEmail;
        }

        return null;
    }

    private async Task<(ApplicationUser? user, string? senha)> CriarUsuarioParaPsicologoAsync(Psicologo psicologo)
    {
        if (string.IsNullOrWhiteSpace(psicologo.Email))
        {
            _logger.LogWarning("Psicólogo {Id} sem email — não é possível criar usuário", psicologo.Id);
            return (null, null);
        }

        var existente = await _userManager.FindByEmailAsync(psicologo.Email);
        if (existente != null)
        {
            if (existente.TipoUsuario == TipoUsuario.Psicologo)
                return (existente, null);

            var loginEmail = $"psicologo.{psicologo.Id}.{psicologo.Email}";
            var jaExisteLogin = await _userManager.FindByEmailAsync(loginEmail);
            if (jaExisteLogin != null)
                return (jaExisteLogin, null);

            return await CriarUsuarioComEmailAsync(psicologo, loginEmail);
        }

        return await CriarUsuarioComEmailAsync(psicologo, psicologo.Email);
    }

    private async Task<(ApplicationUser? user, string? senha)> CriarUsuarioComEmailAsync(Psicologo psicologo, string emailLogin)
    {
        var senha = GerarSenhaTemporaria();
        var user = new ApplicationUser
        {
            UserName = emailLogin,
            Email = emailLogin,
            NomeCompleto = psicologo.Nome,
            TipoUsuario = TipoUsuario.Psicologo,
            CRP = psicologo.CRP,
            PsicologoId = psicologo.Id,
            EmailConfirmed = true,
            Ativo = psicologo.Ativo,
            DataCadastro = DateTime.UtcNow
        };

        var create = await _userManager.CreateAsync(user, senha);
        if (!create.Succeeded)
        {
            _logger.LogError(
                "Falha ao criar usuário para psicólogo {Id} ({Email}): {Errors}",
                psicologo.Id, emailLogin,
                string.Join("; ", create.Errors.Select(e => e.Description)));
            return (null, null);
        }

        await _userManager.AddToRoleAsync(user, "Psicologo");
        psicologo.UserId = user.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuário Identity criado para psicólogo {Id}. Login={Email}", psicologo.Id, emailLogin);
        return (user, senha);
    }

    private async Task<Psicologo?> CriarPsicologoParaUsuarioAsync(ApplicationUser usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario.Email))
            return null;

        var emailPsicologo = usuario.Email;
        if (emailPsicologo.StartsWith("psicologo.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = emailPsicologo.Split('.', 3);
            if (parts.Length == 3)
                emailPsicologo = parts[2];
        }

        var psicologo = new Psicologo
        {
            Nome = string.IsNullOrWhiteSpace(usuario.NomeCompleto) ? emailPsicologo : usuario.NomeCompleto,
            Email = emailPsicologo,
            CRP = string.IsNullOrWhiteSpace(usuario.CRP)
                ? $"PENDENTE-{usuario.Id[..Math.Min(8, usuario.Id.Length)]}"
                : usuario.CRP,
            Telefone = usuario.PhoneNumber,
            ValorConsulta = 150m,
            Especialidades = string.Empty,
            Ativo = usuario.Ativo,
            UserId = usuario.Id,
            DataCadastro = DateTime.UtcNow,
            DataCriacao = DateTime.UtcNow,
            HorarioInicioManha = new TimeSpan(8, 0, 0),
            HorarioFimManha = new TimeSpan(12, 0, 0),
            HorarioInicioTarde = new TimeSpan(14, 0, 0),
            HorarioFimTarde = new TimeSpan(18, 0, 0),
            AtendeSegunda = true,
            AtendeTerca = true,
            AtendeQuarta = true,
            AtendeQuinta = true,
            AtendeSexta = true
        };

        _context.Psicologos.Add(psicologo);
        await _context.SaveChangesAsync();

        usuario.PsicologoId = psicologo.Id;
        usuario.TipoUsuario = TipoUsuario.Psicologo;
        await _userManager.UpdateAsync(usuario);

        if (!await _userManager.IsInRoleAsync(usuario, "Psicologo"))
            await _userManager.AddToRoleAsync(usuario, "Psicologo");

        return psicologo;
    }

    private async Task<bool> GarantirVinculoERoleAsync(ApplicationUser usuario, Psicologo psicologo)
    {
        var alterou = false;

        if (usuario.TipoUsuario == TipoUsuario.Admin)
            return false;

        if (usuario.TipoUsuario != TipoUsuario.Psicologo)
        {
            usuario.TipoUsuario = TipoUsuario.Psicologo;
            alterou = true;
        }

        if (usuario.PsicologoId != psicologo.Id)
        {
            usuario.PsicologoId = psicologo.Id;
            alterou = true;
        }

        if (string.IsNullOrEmpty(usuario.CRP) && !string.IsNullOrEmpty(psicologo.CRP))
        {
            usuario.CRP = psicologo.CRP;
            alterou = true;
        }

        if (alterou)
            await _userManager.UpdateAsync(usuario);

        if (!await _userManager.IsInRoleAsync(usuario, "Psicologo"))
        {
            await _userManager.AddToRoleAsync(usuario, "Psicologo");
            alterou = true;
        }

        if (psicologo.UserId != usuario.Id)
        {
            psicologo.UserId = usuario.Id;
            await _context.SaveChangesAsync();
            alterou = true;
        }

        return alterou;
    }

    private async Task<bool> SincronizarStatusEntreAsync(ApplicationUser usuario, Psicologo psicologo)
    {
        if (usuario.TipoUsuario == TipoUsuario.Admin)
            return false;
        if (usuario.Ativo == psicologo.Ativo)
            return false;

        usuario.Ativo = psicologo.Ativo;
        await _userManager.UpdateAsync(usuario);
        return true;
    }

    private static string GerarSenhaTemporaria()
    {
        const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var body = new char[12];
        for (var i = 0; i < 12; i++)
            body[i] = chars[bytes[i] % chars.Length];
        return $"Tmp{new string(body)}1!";
    }
}
