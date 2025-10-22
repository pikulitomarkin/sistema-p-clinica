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

        public UsuariosModel(
            UserManager<ApplicationUser> userManager,
            AuditoriaService auditoriaService)
        {
            _userManager = userManager;
            _auditoriaService = auditoriaService;
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

                // Verificar se não é admin
                if (usuario.TipoUsuario == TipoUsuario.Admin)
                {
                    TempData["Erro"] = "Não é possível desativar um administrador.";
                    return RedirectToPage();
                }

                // Desativar usuário
                usuario.Ativo = false;
                var result = await _userManager.UpdateAsync(usuario);

                if (result.Succeeded)
                {
                    // Registrar auditoria
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
                else
                {
                    TempData["Erro"] = "Erro ao desativar usuário: " + string.Join(", ", result.Errors.Select(e => e.Description));
                }
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

            // Aplicar filtro de busca
            if (!string.IsNullOrWhiteSpace(Busca))
            {
                query = query.Where(u => u.NomeCompleto.Contains(Busca) || u.Email.Contains(Busca));
            }

            // Aplicar filtro de tipo
            if (!string.IsNullOrWhiteSpace(TipoFiltro))
            {
                if (Enum.TryParse<TipoUsuario>(TipoFiltro, out var tipo))
                {
                    query = query.Where(u => u.TipoUsuario == tipo);
                }
            }

            // Aplicar filtro de status
            if (StatusFiltro.HasValue)
            {
                query = query.Where(u => u.Ativo == StatusFiltro.Value);
            }

            Usuarios = await query
                .OrderBy(u => u.TipoUsuario)
                .ThenBy(u => u.NomeCompleto)
                .ToListAsync();
        }

        private async Task CarregarEstatisticasAsync()
        {
            var todosUsuarios = await _userManager.Users.ToListAsync();
            
            TotalUsuarios = todosUsuarios.Count;
            TotalAdmins = todosUsuarios.Count(u => u.TipoUsuario == TipoUsuario.Admin);
            TotalPsicologos = todosUsuarios.Count(u => u.TipoUsuario == TipoUsuario.Psicologo);
            TotalClientes = todosUsuarios.Count(u => u.TipoUsuario == TipoUsuario.Cliente);
        }
    }
}