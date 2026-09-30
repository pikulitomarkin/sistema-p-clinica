using System.ComponentModel.DataAnnotations;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Avaliacoes;

[Authorize]
public class AvaliarModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AvaliarModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultaId { get; set; }

    public Consulta? Consulta { get; private set; }
    public string AlvoNome { get; private set; } = "";
    public TipoAlvoAvaliacao Alvo { get; private set; }
    public bool JaAvaliou { get; private set; }

    [BindProperty]
    [Range(1, 5, ErrorMessage = "Selecione uma nota de 1 a 5")]
    public int Nota { get; set; } = 5;

    [BindProperty]
    [StringLength(1000)]
    public string? Comentario { get; set; }

    public string? Erro { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var ok = await CarregarContextoAsync();
        if (!ok) return RedirectToPage("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CarregarContextoAsync())
            return RedirectToPage("/Account/Login");

        if (JaAvaliou)
        {
            TempData["Info"] = "Esta consulta já foi avaliada.";
            return RedirectToDestino();
        }

        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Account/Login");

        var avaliacao = new Avaliacao
        {
            ConsultaId = Consulta!.Id,
            PsicologoId = Consulta.PsicologoId,
            PacienteId = Consulta.PacienteId,
            Alvo = Alvo,
            AvaliadorUserId = user.Id,
            Nota = Nota,
            Comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim(),
            DataCriacao = DateTime.UtcNow,
            // Avaliações de psicólogo são públicas no ranking; de paciente ficam internas.
            Publica = Alvo == TipoAlvoAvaliacao.Psicologo
        };

        _db.Avaliacoes.Add(avaliacao);
        await _db.SaveChangesAsync();
        TempData["Sucesso"] = "Avaliação registrada. Obrigado!";
        return RedirectToDestino();
    }

    private IActionResult RedirectToDestino()
    {
        if (User.IsInRole("Psicologo") || User.IsInRole("Admin"))
            return Redirect("/psicologo/consultas");
        return Redirect("/Cliente/Historico");
    }

    private async Task<bool> CarregarContextoAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return false;

        Consulta = await _db.Consultas.AsNoTracking()
            .Include(c => c.Psicologo)
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == ConsultaId);

        if (Consulta == null)
        {
            Erro = "Consulta não encontrada.";
            return false;
        }

        if (Consulta.Status != StatusConsulta.Realizada)
        {
            Erro = "Só é possível avaliar consultas com status Realizada.";
            return false;
        }

        var isPsicologo = User.IsInRole("Psicologo") || User.IsInRole("Admin");
        var isCliente = User.IsInRole("Cliente");

        if (isCliente)
        {
            var paciente = await _db.Pacientes.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Email == user.Email);
            if (paciente == null || paciente.Id != Consulta.PacienteId)
            {
                Erro = "Você não pode avaliar esta consulta.";
                return false;
            }

            Alvo = TipoAlvoAvaliacao.Psicologo;
            AlvoNome = Consulta.Psicologo.Nome;
        }
        else if (isPsicologo)
        {
            var psicologo = await _db.Psicologos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id || p.Email == user.Email);
            if (psicologo == null || psicologo.Id != Consulta.PsicologoId)
            {
                Erro = "Você não pode avaliar esta consulta.";
                return false;
            }

            Alvo = TipoAlvoAvaliacao.Paciente;
            AlvoNome = Consulta.Paciente.Nome;
        }
        else
        {
            Erro = "Perfil sem permissão para avaliar.";
            return false;
        }

        JaAvaliou = await _db.Avaliacoes.AsNoTracking()
            .AnyAsync(a => a.ConsultaId == ConsultaId && a.Alvo == Alvo);

        return string.IsNullOrEmpty(Erro);
    }
}
