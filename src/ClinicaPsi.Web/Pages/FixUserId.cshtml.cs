using ClinicaPsi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ClinicaPsi.Web.Pages;

public class FixUserIdModel : PageModel
{
    private readonly AppDbContext _context;

    public FixUserIdModel(AppDbContext context)
    {
        _context = context;
    }

    public string? Result { get; set; }
    public bool Success { get; set; }

    public async Task OnGetAsync()
    {
        var sb = new StringBuilder();
        
        try
        {
            sb.AppendLine("=== FIX USERID DO PSICOLOGO ===\n");

            var psicologos = await _context.Psicologos.ToListAsync();
            var usuarios = await _context.Users.ToListAsync();

            sb.AppendLine($"Psicologos: {psicologos.Count}");
            sb.AppendLine($"Usuarios: {usuarios.Count}\n");

            int updated = 0;
            foreach (var psi in psicologos)
            {
                if (string.IsNullOrEmpty(psi.UserId))
                {
                    var user = usuarios.FirstOrDefault(u => u.Email == psi.Email);
                    if (user != null)
                    {
                        psi.UserId = user.Id;
                        updated++;
                        sb.AppendLine($"✓ {psi.Nome} → UserId: {user.Id}");
                    }
                }
            }

            if (updated > 0)
            {
                await _context.SaveChangesAsync();
                sb.AppendLine($"\n✓ {updated} registro(s) atualizado(s)!");
                Success = true;
            }
            else
            {
                sb.AppendLine("\n✓ Nenhuma atualização necessária (já configurado)");
                Success = true;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"\n✗ ERRO: {ex.Message}");
            Success = false;
        }

        Result = sb.ToString();
    }
}
