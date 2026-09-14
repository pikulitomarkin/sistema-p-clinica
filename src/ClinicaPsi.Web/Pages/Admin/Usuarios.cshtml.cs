using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;
using System.Text.Json;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UsuariosModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditoriaService _auditoriaService;
        private readonly UsuarioPsicologoSyncService _syncService;
        private readonly ILogger<UsuariosModel> _logger;

        public UsuariosModel(
            UserManager<ApplicationUser> userManager,
            AuditoriaService auditoriaService,
            UsuarioPsicologoSyncService syncService,
            ILogger<UsuariosModel> logger)
        {
            _userManager = userManager;
            _auditoriaService = auditoriaService;
            _syncService = syncService;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Busca { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string TipoFiltro { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public bool? StatusFiltro { get; set; }

        public List<ApplicationUser> Usuarios { get; set; } = new();
        public int TotalUsuarios { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalPsicologos { get; set; }
        public int TotalClientes { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var sync = await _syncService.SincronizarTodosAsync();
                if (sync.UsuariosCriados > 0)
                {
                    TempData["Sucesso"] =
                        $"Sincronização automática: {sync.UsuariosCriados} usuário(s) de psicólogo criado(s). " +
                        "Altere as senhas temporárias se necessário.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no backfill ao abrir Gestão de Usuários");
            }

            await CarregarUsuarios();
            await CarregarEstatisticasAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDesativarAsync(string usuarioId)
        {
            try
            {
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                {
                    TempData["Erro"] = "Usuário não encontrado.";
                    return RedirectToPage();
                }

                if (usuario.TipoUsuario == TipoUsuario.Admin)
                {
                    TempData["Erro"] = "Não é possível desativar um administrador.";
                    return RedirectToPage();
                }

                await _syncService.SincronizarStatusPorUsuarioAsync(usuario, ativo: false);

                var adminAtual = await _userManager.GetUserAsync(User);
                if (adminAtual != null)
                {
                    var detalhes = JsonSerializer.Serialize(new
                    {
                        UsuarioDesativado = usuario.NomeCompleto,
                        TipoUsuario = usuario.TipoUsuario.ToString(),
                        DesativadoPor = adminAtual.NomeCompleto
                    });

                    await _auditoriaService.RegistrarAcaoAsync(
                        adminId: adminAtual.Id,
                        adminNome: adminAtual.NomeCompleto ?? adminAtual.Email ?? "Sistema",
                        usuarioAfetadoId: usuario.Id,
                        usuarioAfetadoNome: usuario.NomeCompleto,
                        usuarioAfetadoEmail: usuario.Email ?? string.Empty,
                        acao: TipoAcaoAuditoria.DesativacaoUsuario,
                        detalhes: detalhes,
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
                    );
                }

                TempData["Sucesso"] = $"Usuário {usuario.NomeCompleto} foi desativado com sucesso.";
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro interno: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAtivarAsync(string usuarioId)
        {
            try
            {
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                {
                    TempData["Erro"] = "Usuário não encontrado.";
                    return RedirectToPage();
                }

                await _syncService.SincronizarStatusPorUsuarioAsync(usuario, ativo: true);
                TempData["Sucesso"] = $"Usuário {usuario.NomeCompleto} foi ativado com sucesso.";
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro interno: " + ex.Message;
            }

            return RedirectToPage();
        }

        private async Task CarregarUsuarios()
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(Busca))
            {
                query = query.Where(u =>
                    (u.NomeCompleto != null && u.NomeCompleto.Contains(Busca)) ||
                    (u.Email != null && u.Email.Contains(Busca)));
            }

            if (!string.IsNullOrWhiteSpace(TipoFiltro) &&
                Enum.TryParse<TipoUsuario>(TipoFiltro, out var tipo))
            {
                query = query.Where(u => u.TipoUsuario == tipo);
            }

            if (StatusFiltro.HasValue)
                query = query.Where(u => u.Ativo == StatusFiltro.Value);

            Usuarios = await query
                .OrderBy(u => u.TipoUsuario)
                .ThenBy(u => u.NomeCompleto)
                .ToListAsync();
        }

        private async Task CarregarEstatisticasAsync()
        {
            TotalUsuarios = await _userManager.Users.CountAsync();
            TotalAdmins = await _userManager.Users.CountAsync(u => u.TipoUsuario == TipoUsuario.Admin);
            TotalPsicologos = await _userManager.Users.CountAsync(u => u.TipoUsuario == TipoUsuario.Psicologo);
            TotalClientes = await _userManager.Users.CountAsync(u => u.TipoUsuario == TipoUsuario.Cliente);
        }
    }
}
