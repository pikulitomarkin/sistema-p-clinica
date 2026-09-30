using System.Globalization;
using System.Text;
using System.Text.Json;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Privacidade;

[Authorize]
public class MeusDadosModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public MeusDadosModel(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SolicitacaoPrivacidade> MinhasSolicitacoes { get; private set; } = new();
    public string? MensagemSucesso { get; private set; }

    public class InputModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Selecione o tipo de solicitação")]
        public TipoSolicitacaoPrivacidade Tipo { get; set; } = TipoSolicitacaoPrivacidade.Acesso;

        [System.ComponentModel.DataAnnotations.StringLength(2000)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Detalhes")]
        public string? Detalhes { get; set; }
    }

    public async Task OnGetAsync()
    {
        await CarregarAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            await CarregarAsync(user.Id);
            return Page();
        }

        _db.SolicitacoesPrivacidade.Add(new SolicitacaoPrivacidade
        {
            UserId = user.Id,
            NomeTitular = user.NomeCompleto,
            EmailTitular = user.Email ?? string.Empty,
            Tipo = Input.Tipo,
            Status = StatusSolicitacaoPrivacidade.Pendente,
            Detalhes = string.IsNullOrWhiteSpace(Input.Detalhes) ? null : Input.Detalhes.Trim(),
            DataCriacao = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        MensagemSucesso = "Solicitação registrada. Nossa equipe analisará pelo canal de privacidade.";
        Input = new InputModel();
        ModelState.Clear();
        await CarregarAsync(user.Id);
        return Page();
    }

    public async Task<IActionResult> OnGetExportarJsonAsync()
    {
        var payload = await MontarExportacaoAsync();
        if (payload == null)
            return Challenge();

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"psyall-meus-dados-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    public async Task<IActionResult> OnGetExportarCsvAsync()
    {
        var payload = await MontarExportacaoAsync();
        if (payload == null)
            return Challenge();

        var sb = new StringBuilder();
        sb.AppendLine("campo,valor");
        void Line(string k, object? v)
        {
            var raw = v?.ToString() ?? "";
            var escaped = "\"" + raw.Replace("\"", "\"\"") + "\"";
            sb.AppendLine($"{k},{escaped}");
        }

        Line("NomeCompleto", payload.Usuario.NomeCompleto);
        Line("Email", payload.Usuario.Email);
        Line("Telefone", payload.Usuario.Telefone);
        Line("CPF", payload.Usuario.CPF);
        Line("TipoUsuario", payload.Usuario.TipoUsuario);
        Line("DataCadastro", payload.Usuario.DataCadastro);
        Line("AceiteTermosEm", payload.Usuario.AceiteTermosEm);
        Line("AceitePrivacidadeEm", payload.Usuario.AceitePrivacidadeEm);
        Line("ConsentimentoDadosSaudeEm", payload.Usuario.ConsentimentoDadosSaudeEm);
        if (payload.Paciente != null)
        {
            Line("Paciente.Nome", payload.Paciente.Nome);
            Line("Paciente.Email", payload.Paciente.Email);
            Line("Paciente.Telefone", payload.Paciente.Telefone);
            Line("Paciente.DataNascimento", payload.Paciente.DataNascimento);
        }
        if (payload.Psicologo != null)
        {
            Line("Psicologo.Nome", payload.Psicologo.Nome);
            Line("Psicologo.CRP", payload.Psicologo.CRP);
            Line("Psicologo.Email", payload.Psicologo.Email);
        }
        foreach (var c in payload.Consultas)
        {
            Line($"Consulta.{c.Id}.DataHorario", c.DataHorario);
            Line($"Consulta.{c.Id}.Status", c.Status);
            Line($"Consulta.{c.Id}.Formato", c.Formato);
            Line($"Consulta.{c.Id}.Valor", c.Valor.ToString(CultureInfo.InvariantCulture));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"psyall-meus-dados-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private async Task CarregarAsync(string? userId = null)
    {
        userId ??= _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return;

        MinhasSolicitacoes = await _db.SolicitacoesPrivacidade.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.DataCriacao)
            .Take(20)
            .ToListAsync();
    }

    private async Task<ExportPayload?> MontarExportacaoAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return null;

        PacienteExport? pac = null;
        if (user.PacienteId is int pid)
        {
            var p = await _db.Pacientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid);
            if (p != null)
            {
                pac = new PacienteExport
                {
                    Nome = p.Nome,
                    Email = p.Email,
                    Telefone = p.Telefone,
                    DataNascimento = p.DataNascimento.ToString("yyyy-MM-dd"),
                    AceiteTermosEm = p.AceiteTermosEm?.ToString("o"),
                    AceitePrivacidadeEm = p.AceitePrivacidadeEm?.ToString("o"),
                    ConsentimentoDadosSaudeEm = p.ConsentimentoDadosSaudeEm?.ToString("o")
                };
            }
        }

        PsicologoExport? psi = null;
        if (user.PsicologoId is int sid)
        {
            var p = await _db.Psicologos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid);
            if (p != null)
            {
                psi = new PsicologoExport
                {
                    Nome = p.Nome,
                    Email = p.Email,
                    CRP = p.CRP,
                    Telefone = p.Telefone,
                    AceiteTermosEm = p.AceiteTermosEm?.ToString("o"),
                    AceitePrivacidadeEm = p.AceitePrivacidadeEm?.ToString("o"),
                    AceiteContratoEm = p.AceiteContratoEm?.ToString("o")
                };
            }
        }

        var consultasQuery = _db.Consultas.AsNoTracking().AsQueryable();
        if (user.PacienteId is int pacienteId)
            consultasQuery = consultasQuery.Where(c => c.PacienteId == pacienteId);
        else if (user.PsicologoId is int psicologoId)
            consultasQuery = consultasQuery.Where(c => c.PsicologoId == psicologoId);
        else
            consultasQuery = consultasQuery.Where(_ => false);

        var consultas = await consultasQuery
            .OrderByDescending(c => c.DataHorario)
            .Take(200)
            .Select(c => new ConsultaExport
            {
                Id = c.Id,
                DataHorario = c.DataHorario.ToString("o"),
                Status = c.Status.ToString(),
                Formato = c.Formato.ToString(),
                Valor = c.Valor,
                DuracaoMinutos = c.DuracaoMinutos
            })
            .ToListAsync();

        return new ExportPayload
        {
            ExportadoEm = DateTime.UtcNow.ToString("o"),
            Usuario = new UsuarioExport
            {
                NomeCompleto = user.NomeCompleto,
                Email = user.Email,
                Telefone = user.PhoneNumber,
                CPF = user.CPF,
                TipoUsuario = user.TipoUsuario.ToString(),
                DataCadastro = user.DataCadastro.ToString("o"),
                AceiteTermosEm = user.AceiteTermosEm?.ToString("o"),
                AceitePrivacidadeEm = user.AceitePrivacidadeEm?.ToString("o"),
                ConsentimentoDadosSaudeEm = user.ConsentimentoDadosSaudeEm?.ToString("o"),
                CookieAnalyticsAceito = user.CookieAnalyticsAceito
            },
            Paciente = pac,
            Psicologo = psi,
            Consultas = consultas
        };
    }

    private sealed class ExportPayload
    {
        public string ExportadoEm { get; set; } = "";
        public UsuarioExport Usuario { get; set; } = new();
        public PacienteExport? Paciente { get; set; }
        public PsicologoExport? Psicologo { get; set; }
        public List<ConsultaExport> Consultas { get; set; } = new();
    }

    private sealed class UsuarioExport
    {
        public string NomeCompleto { get; set; } = "";
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? CPF { get; set; }
        public string TipoUsuario { get; set; } = "";
        public string DataCadastro { get; set; } = "";
        public string? AceiteTermosEm { get; set; }
        public string? AceitePrivacidadeEm { get; set; }
        public string? ConsentimentoDadosSaudeEm { get; set; }
        public bool? CookieAnalyticsAceito { get; set; }
    }

    private sealed class PacienteExport
    {
        public string Nome { get; set; } = "";
        public string Email { get; set; } = "";
        public string Telefone { get; set; } = "";
        public string DataNascimento { get; set; } = "";
        public string? AceiteTermosEm { get; set; }
        public string? AceitePrivacidadeEm { get; set; }
        public string? ConsentimentoDadosSaudeEm { get; set; }
    }

    private sealed class PsicologoExport
    {
        public string Nome { get; set; } = "";
        public string Email { get; set; } = "";
        public string CRP { get; set; } = "";
        public string? Telefone { get; set; }
        public string? AceiteTermosEm { get; set; }
        public string? AceitePrivacidadeEm { get; set; }
        public string? AceiteContratoEm { get; set; }
    }

    private sealed class ConsultaExport
    {
        public int Id { get; set; }
        public string DataHorario { get; set; } = "";
        public string Status { get; set; } = "";
        public string Formato { get; set; } = "";
        public decimal Valor { get; set; }
        public int DuracaoMinutos { get; set; }
    }
}
