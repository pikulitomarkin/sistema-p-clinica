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

            // 1. Adicionar coluna UserId se não existir
            try
            {
                sb.AppendLine("1. Verificando coluna UserId...");
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Psicologos\" ADD COLUMN IF NOT EXISTS \"UserId\" TEXT NULL");
                sb.AppendLine("✓ Coluna UserId verificada/criada\n");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ Erro ao criar coluna (pode já existir): {ex.Message}\n");
            }

            // 2. Buscar dados
            sb.AppendLine("2. Buscando dados...");
            var psicologos = await _context.Psicologos.ToListAsync();
            var usuarios = await _context.Users.ToListAsync();
            sb.AppendLine($"✓ Psicologos: {psicologos.Count}");
            sb.AppendLine($"✓ Usuarios: {usuarios.Count}\n");

            // 3. Atualizar UserId
            sb.AppendLine("3. Vinculando psicólogos com usuários...");
            int updated = 0;
            
            foreach (var psi in psicologos)
            {
                var user = usuarios.FirstOrDefault(u => u.Email == psi.Email);
                if (user != null)
                {
                    // Atualizar diretamente via SQL
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE \"Psicologos\" SET \"UserId\" = {0} WHERE \"Id\" = {1}",
                        user.Id, psi.Id);
                    
                    updated++;
                    sb.AppendLine($"✓ {psi.Nome} ({psi.Email}) → UserId: {user.Id}");
                }
                else
                {
                    sb.AppendLine($"⚠ {psi.Nome} ({psi.Email}) → Sem usuário correspondente");
                }
            }

            sb.AppendLine($"\n✓ CONCLUÍDO: {updated} registro(s) atualizado(s)!");
            Success = true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"\n✗ ERRO: {ex.Message}");
            sb.AppendLine($"Stack: {ex.StackTrace}");
            Success = false;
        }

        Result = sb.ToString();
    }
}
