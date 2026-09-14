using ClinicaPsi.Application.Services;
using ClinicaPsi.Application.Services.Email;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClinicaPsi.Web.Pages.Psicologo;

[Authorize(Roles = "Admin,Psicologo")]
public class SalaConsultaModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VideoConsultaService _videoConsultaService;
    private readonly ProntuarioService _prontuarioService;
    private readonly PdfService _pdfService;
    private readonly IEmailService _emailService;
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SalaConsultaModel> _logger;

    public SalaConsultaModel(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        VideoConsultaService videoConsultaService,
        ProntuarioService prontuarioService,
        PdfService pdfService,
        IEmailService emailService,
        IOptionsMonitor<EmailOptions> emailOptions,
        IConfiguration configuration,
        ILogger<SalaConsultaModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _videoConsultaService = videoConsultaService;
        _prontuarioService = prontuarioService;
        _pdfService = pdfService;
        _emailService = emailService;
        _emailOptions = emailOptions;
        _configuration = configuration;
        _logger = logger;
    }

    public Consulta Consulta { get; set; } = null!;
    public string? EmbedUrl { get; set; }
    public bool VideoIniciado { get; set; }
    public string? MensagemSucesso { get; set; }
    public string? MensagemErro { get; set; }
    public string? MensagemInfo { get; set; }

    [BindProperty]
    public ProntuarioInput Input { get; set; } = new();

    [BindProperty]
    public AtestadoInput Atestado { get; set; } = new();

    public class ProntuarioInput
    {
        public int? Id { get; set; }
        public string QueixaPrincipal { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public string? Evolucao { get; set; }
        public string? Intervencoes { get; set; }
        public string? PlanoTerapeutico { get; set; }
        public string? ProximaSessao { get; set; }
        public string? EstadoEmocional { get; set; }
    }

    public class AtestadoInput
    {
        public string? Cid { get; set; }
        public int DiasAfastamento { get; set; } = 1;
        public string? Observacoes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id, bool iniciarVideo = false)
    {
        var acesso = await CarregarConsultaAsync(id);
        if (acesso != null) return acesso;

        await PrepararVideoAsync(iniciarVideo);
        await CarregarProntuarioAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostIniciarVideoAsync(int id)
    {
        var acesso = await CarregarConsultaAsync(id);
        if (acesso != null) return acesso;

        if (Consulta.Formato != FormatoConsulta.Online)
            Consulta.Formato = FormatoConsulta.Online;

        await _videoConsultaService.GarantirSalaAsync(Consulta);
        EmbedUrl = Consulta.VideoRoomUrl;
        VideoIniciado = true;
        await CarregarProntuarioAsync();
        MensagemInfo = "Videochamada pronta. Permita câmera e microfone.";
        return Page();
    }

    public async Task<IActionResult> OnPostChamarPacienteAsync(int id)
    {
        var acesso = await CarregarConsultaAsync(id);
        if (acesso != null) return acesso;

        try
        {
            if (Consulta.Formato != FormatoConsulta.Online)
                Consulta.Formato = FormatoConsulta.Online;

            await _videoConsultaService.GarantirSalaAsync(Consulta);

            Consulta.PacienteChamado = true;
            Consulta.PacienteChamadoEm = DateTime.Now;
            Consulta.DataAtualizacao = DateTime.Now;
            await _context.SaveChangesAsync();

            var linkSala = $"{ObterBaseUrlPublica()}/Cliente/SalaConsulta/{Consulta.Id}";
            var email = Consulta.Paciente?.Email;
            var html = EmailTemplates.ChamadaConsulta(
                Consulta.Paciente?.Nome ?? "paciente",
                Consulta.Psicologo?.Nome ?? "psicólogo(a)",
                Consulta.DataHorario,
                linkSala);

            bool enviada = false;
            string? erro = null;

            if (string.IsNullOrWhiteSpace(email))
            {
                erro = "Paciente sem e-mail cadastrado";
            }
            else if (!_emailService.IsConfigured)
            {
                _logger.LogWarning(
                    "[EMAIL STUB] Chamada consulta {Id} para {Email}. Link={Link}",
                    Consulta.Id, email, linkSala);
                enviada = true; // stub: não bloqueia fluxo clínico
                erro = null;
                MensagemInfo = $"Paciente chamado. E-mail em stub (defina RESEND_API_KEY). Link: {linkSala}";
            }
            else
            {
                var result = await _emailService.SendAsync(email, "Você está sendo chamado para a consulta", html);
                enviada = result.Success;
                erro = result.ErrorMessage;
            }

            _context.NotificacoesConsultas.Add(new NotificacaoConsulta
            {
                ConsultaId = Consulta.Id,
                Tipo = TipoNotificacao.Email,
                Destinatario = email ?? "(sem e-mail)",
                Assunto = "Chamada para sala da consulta",
                Conteudo = linkSala,
                DataEnvio = DateTime.Now,
                Enviada = enviada,
                ErroEnvio = erro
            });
            await _context.SaveChangesAsync();

            EmbedUrl = Consulta.VideoRoomUrl;
            VideoIniciado = true;
            await CarregarProntuarioAsync();

            if (string.IsNullOrEmpty(MensagemInfo))
            {
                if (enviada)
                    MensagemSucesso = $"Paciente chamado. E-mail enviado para {email}.";
                else
                    MensagemInfo = $"Paciente marcado como chamado. Aviso de e-mail: {erro}. Link: {linkSala}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar paciente da consulta {Id}", id);
            MensagemErro = "Não foi possível chamar o paciente.";
            await CarregarProntuarioAsync();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSalvarProntuarioAsync(int id)
    {
        var acesso = await CarregarConsultaAsync(id);
        if (acesso != null) return acesso;

        try
        {
            if (string.IsNullOrWhiteSpace(Input.QueixaPrincipal) || string.IsNullOrWhiteSpace(Input.Observacoes))
            {
                MensagemErro = "Preencha queixa principal e observações.";
                await PrepararVideoAsync(false);
                return Page();
            }

            ProntuarioEletronico prontuario;
            var existente = Input.Id is > 0
                ? await _prontuarioService.ObterPorIdAsync(Input.Id.Value)
                : await _prontuarioService.ObterPorConsultaAsync(id);

            if (existente != null)
            {
                if (existente.Finalizado)
                {
                    MensagemErro = "Prontuário finalizado não pode ser editado.";
                    await PrepararVideoAsync(false);
                    await CarregarProntuarioAsync();
                    return Page();
                }
                prontuario = existente;
            }
            else
            {
                prontuario = new ProntuarioEletronico
                {
                    PacienteId = Consulta.PacienteId,
                    PsicologoId = Consulta.PsicologoId,
                    ConsultaId = Consulta.Id,
                    DataSessao = Consulta.DataHorario.Date,
                    TipoAtendimento = Consulta.Formato == FormatoConsulta.Online ? "Online" : "Individual",
                    DataCriacao = DateTime.Now
                };
            }

            prontuario.QueixaPrincipal = Input.QueixaPrincipal.Trim();
            prontuario.Observacoes = Input.Observacoes.Trim();
            prontuario.Evolucao = Input.Evolucao;
            prontuario.Intervencoes = Input.Intervencoes;
            prontuario.PlanoTerapeutico = Input.PlanoTerapeutico;
            prontuario.ProximaSessao = Input.ProximaSessao;
            prontuario.EstadoEmocional = Input.EstadoEmocional;
            prontuario.DataAtualizacao = DateTime.Now;

            if (prontuario.Id > 0)
                await _prontuarioService.AtualizarProntuarioAsync(prontuario);
            else
                prontuario = await _prontuarioService.CriarProntuarioAsync(prontuario);

            Input.Id = prontuario.Id;
            MensagemSucesso = "Prontuário salvo com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar prontuário da consulta {Id}", id);
            MensagemErro = "Erro ao salvar prontuário.";
        }

        await PrepararVideoAsync(false);
        return Page();
    }

    public async Task<IActionResult> OnGetDeclaracaoAsync(int id)
    {
        var acesso = await CarregarConsultaAsync(id);
        if (acesso != null) return acesso;

        var pdf = await _pdfService.GerarDeclaracaoComparecimentoAsync(id);
        var nome = $"Declaracao_{Sanitize(Consulta.Paciente?.Nome)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", nome);
    }

    public async Task<IActionResult> OnPostAtestadoAsync(int id)
    {
        var acesso = await CarregarConsultaAsync(id);
        if (acesso != null) return acesso;

        if (Atestado.DiasAfastamento < 1 || Atestado.DiasAfastamento > 90)
        {
            MensagemErro = "Dias de afastamento devem estar entre 1 e 90.";
            await PrepararVideoAsync(false);
            await CarregarProntuarioAsync();
            return Page();
        }

        var pdf = await _pdfService.GerarAtestadoAsync(
            id,
            Atestado.Cid ?? string.Empty,
            Atestado.DiasAfastamento,
            Atestado.Observacoes ?? string.Empty);

        var nome = $"Atestado_{Sanitize(Consulta.Paciente?.Nome)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", nome);
    }

    private static string Sanitize(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "paciente" : name.Replace(' ', '_');

    private async Task<IActionResult?> CarregarConsultaAsync(int id)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (consulta == null)
        {
            TempData["Error"] = "Consulta não encontrada.";
            return RedirectToPage("/Psicologo/Agenda");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        if (!User.IsInRole("Admin"))
        {
            var psicologoId = user.PsicologoId
                ?? (await _context.Psicologos.FirstOrDefaultAsync(p => p.UserId == user.Id))?.Id;

            if (psicologoId == null || consulta.PsicologoId != psicologoId.Value)
            {
                TempData["Error"] = "Você não tem permissão para acessar esta consulta.";
                return RedirectToPage("/Psicologo/Agenda");
            }
        }

        Consulta = consulta;
        return null;
    }

    private async Task PrepararVideoAsync(bool iniciar)
    {
        if (Consulta.Formato == FormatoConsulta.Online || !string.IsNullOrWhiteSpace(Consulta.VideoRoomUrl) || Consulta.PacienteChamado)
        {
            if (Consulta.Formato != FormatoConsulta.Online && Consulta.PacienteChamado)
                Consulta.Formato = FormatoConsulta.Online;

            await _videoConsultaService.GarantirSalaAsync(Consulta);
            EmbedUrl = Consulta.VideoRoomUrl;
            if (iniciar || Consulta.PacienteChamado)
                VideoIniciado = true;
        }
    }

    private async Task CarregarProntuarioAsync()
    {
        var prontuario = await _prontuarioService.ObterPorConsultaAsync(Consulta.Id);
        if (prontuario != null)
        {
            Input = new ProntuarioInput
            {
                Id = prontuario.Id,
                QueixaPrincipal = prontuario.QueixaPrincipal,
                Observacoes = prontuario.Observacoes,
                Evolucao = prontuario.Evolucao,
                Intervencoes = prontuario.Intervencoes,
                PlanoTerapeutico = prontuario.PlanoTerapeutico,
                ProximaSessao = prontuario.ProximaSessao,
                EstadoEmocional = prontuario.EstadoEmocional
            };
        }
        else if (string.IsNullOrWhiteSpace(Input.QueixaPrincipal) && !string.IsNullOrWhiteSpace(Consulta.Observacoes))
        {
            Input.QueixaPrincipal = Consulta.Observacoes;
        }
    }

    private string ObterBaseUrlPublica()
    {
        var configured = _emailOptions.CurrentValue.PublicAppUrl
            ?? _configuration["PUBLIC_APP_URL"]
            ?? _configuration["WhatsApp:SiteUrl"]
            ?? Environment.GetEnvironmentVariable("PUBLIC_APP_URL");

        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        return $"{Request.Scheme}://{Request.Host}";
    }
}
