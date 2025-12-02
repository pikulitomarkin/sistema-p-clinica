using ClinicaPsi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ClinicaPsi.Web.Pages.Admin;

// TEMPORÁRIO: Removido [Authorize] para permitir acesso direto
// [Authorize(Roles = "Admin")]
public class UpdateUserIdModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<UpdateUserIdModel> _logger;

    public UpdateUserIdModel(AppDbContext context, ILogger<UpdateUserIdModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string? ResultMessage { get; set; }
    public bool Success { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var resultado = new StringBuilder();
            resultado.AppendLine("=== ATUALIZAÇÃO DE USERID DO PSICÓLOGO ===\n");

            // 1. Verificar psicólogos existentes
            var psicologos = await _context.Psicologos.ToListAsync();
            resultado.AppendLine($"📋 Psicólogos encontrados: {psicologos.Count}");
            foreach (var psi in psicologos)
            {
                resultado.AppendLine($"  - ID: {psi.Id}, Nome: {psi.Nome}, Email: {psi.Email}, UserId: {psi.UserId ?? "NULL"}");
            }
            resultado.AppendLine();

            // 2. Verificar usuários AspNet
            var usuarios = await _context.Users.ToListAsync();
            resultado.AppendLine($"👤 Usuários AspNetUsers encontrados: {usuarios.Count}");
            foreach (var user in usuarios)
            {
                resultado.AppendLine($"  - Id: {user.Id}, Email: {user.Email}, UserName: {user.UserName}");
            }
            resultado.AppendLine();

            // 3. Atualizar UserId do psicólogo com base no email
            int atualizados = 0;
            foreach (var psi in psicologos)
            {
                if (string.IsNullOrEmpty(psi.UserId))
                {
                    var usuario = usuarios.FirstOrDefault(u => u.Email == psi.Email);
                    if (usuario != null)
                    {
                        psi.UserId = usuario.Id;
                        atualizados++;
                        resultado.AppendLine($"✅ Psicólogo '{psi.Nome}' vinculado ao usuário '{usuario.Email}'");
                        resultado.AppendLine($"   UserId atribuído: {usuario.Id}");
                    }
                    else
                    {
                        resultado.AppendLine($"⚠️ Psicólogo '{psi.Nome}' ({psi.Email}) não tem usuário correspondente");
                    }
                }
                else
                {
                    resultado.AppendLine($"ℹ️ Psicólogo '{psi.Nome}' já possui UserId: {psi.UserId}");
                }
            }

            if (atualizados > 0)
            {
                await _context.SaveChangesAsync();
                resultado.AppendLine($"\n💾 {atualizados} psicólogo(s) atualizado(s) no banco de dados!");
            }
            else
            {
                resultado.AppendLine("\nℹ️ Nenhuma atualização necessária.");
            }

            // 4. Verificar resultado final
            resultado.AppendLine("\n=== RESULTADO FINAL ===");
            var psicologosAtualizados = await _context.Psicologos
                .Select(p => new { p.Id, p.Nome, p.Email, p.UserId })
                .ToListAsync();

            foreach (var psi in psicologosAtualizados)
            {
                var user = usuarios.FirstOrDefault(u => u.Id == psi.UserId);
                resultado.AppendLine($"✓ {psi.Nome} - UserId: {psi.UserId} - User: {user?.UserName ?? "N/A"}");
            }

            Success = true;
            ResultMessage = resultado.ToString();
            _logger.LogInformation("UserId atualizado com sucesso. {Count} registros atualizados.", atualizados);
        }
        catch (Exception ex)
        {
            Success = false;
            ResultMessage = $"❌ ERRO ao atualizar UserId:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            _logger.LogError(ex, "Erro ao atualizar UserId do psicólogo");
        }
    }
}
