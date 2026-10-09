using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Profissionais;

public class PerfilModel : PageModel
{
    private readonly AppDbContext _db;

    public PerfilModel(AppDbContext db) => _db = db;

    public ClinicaPsi.Shared.Models.Psicologo Psicologo { get; private set; } = null!;
    public int AtendimentosRealizados { get; private set; }
    public double AvaliacaoMedia { get; private set; }
    public int TotalAvaliacoes { get; private set; }
    public List<string> Especialidades { get; private set; } = new();
    public List<string> DiasAtendimento { get; private set; } = new();
    public string? HorarioManha { get; private set; }
    public string? HorarioTarde { get; private set; }
    public List<AvaliacaoPublicaVm> Avaliacoes { get; private set; } = new();
    public string UrlAgendar { get; private set; } = "/agendamento";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var psicologo = await _db.Psicologos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.Ativo && p.ExcluidoEm == null
                && p.StatusValidacao == StatusValidacaoPsicologo.Aprovado);

        if (psicologo == null)
            return NotFound();

        Psicologo = psicologo;
        Especialidades = (psicologo.Especialidades ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();
        if (Especialidades.Count == 0)
            Especialidades.Add("Psicologia clínica");

        DiasAtendimento = Dias(psicologo);
        if (psicologo.AtendeManha && psicologo.HorarioInicioManha < psicologo.HorarioFimManha)
            HorarioManha = $"{Formatar(psicologo.HorarioInicioManha)} às {Formatar(psicologo.HorarioFimManha)}";
        if (psicologo.AtendeTarde && psicologo.HorarioInicioTarde < psicologo.HorarioFimTarde)
            HorarioTarde = $"{Formatar(psicologo.HorarioInicioTarde)} às {Formatar(psicologo.HorarioFimTarde)}";

        AtendimentosRealizados = await _db.Consultas.AsNoTracking()
            .CountAsync(c => c.PsicologoId == id && c.Status == StatusConsulta.Realizada);

        var avaliacoes = await _db.Avaliacoes.AsNoTracking()
            .Where(a => a.PsicologoId == id && a.Alvo == TipoAlvoAvaliacao.Psicologo && a.Publica)
            .OrderByDescending(a => a.DataCriacao)
            .Select(a => new
            {
                a.Nota,
                a.Comentario,
                a.DataCriacao,
                Nome = a.Paciente != null ? a.Paciente.Nome : null
            })
            .ToListAsync();

        TotalAvaliacoes = avaliacoes.Count;
        AvaliacaoMedia = TotalAvaliacoes == 0 ? 0 : avaliacoes.Average(a => a.Nota);
        Avaliacoes = avaliacoes.Select(a => new AvaliacaoPublicaVm
        {
            Nota = a.Nota,
            Comentario = string.IsNullOrWhiteSpace(a.Comentario) ? null : a.Comentario.Trim(),
            Data = a.DataCriacao,
            Autor = PrimeiroNome(a.Nome)
        }).ToList();

        var destino = $"/Cliente/AgendarConsulta?psicologoId={id}";
        UrlAgendar = User.Identity?.IsAuthenticated == true
            ? destino
            : "/Account/Login?returnUrl=" + Uri.EscapeDataString(destino);

        return Page();
    }

    private static List<string> Dias(ClinicaPsi.Shared.Models.Psicologo p)
    {
        var dias = new List<string>();
        if (p.AtendeSegunda) dias.Add("Segunda");
        if (p.AtendeTerca) dias.Add("Terça");
        if (p.AtendeQuarta) dias.Add("Quarta");
        if (p.AtendeQuinta) dias.Add("Quinta");
        if (p.AtendeSexta) dias.Add("Sexta");
        if (p.AtendeSabado) dias.Add("Sábado");
        if (p.AtendeDomingo) dias.Add("Domingo");
        return dias;
    }

    private static string Formatar(TimeSpan hora) => hora.ToString(@"hh\:mm");

    private static string PrimeiroNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "Paciente";
        var parte = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parte.Length == 0 ? "Paciente" : parte[0];
    }

    public class AvaliacaoPublicaVm
    {
        public int Nota { get; set; }
        public string? Comentario { get; set; }
        public DateTime Data { get; set; }
        public string Autor { get; set; } = "Paciente";
    }
}
