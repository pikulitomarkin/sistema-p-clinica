using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.ConsultaPages;

[Authorize(Roles = "Admin,Psicologo,Cliente")]
public class VideoModel : PageModel
{
    private readonly VideoConsultaService _videoConsultaService;
    private readonly ProntuarioService _prontuarioService;
    private readonly PdfService _pdfService;
    private readonly ConfiguracaoService _configuracaoService;
    private readonly PsicologoService _psicologoService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<VideoModel> _logger;

    public VideoModel(
        VideoConsultaService videoConsultaService,
        ProntuarioService prontuarioService,
        PdfService pdfService,
        ConfiguracaoService configuracaoService,
        PsicologoService psicologoService,
        UserManager<ApplicationUser> userManager,
        ILogger<VideoModel> logger)
    {
        _videoConsultaService = videoConsultaService;
        _prontuarioService = prontuarioService;
        _pdfService = pdfService;
        _configuracaoService = configuracaoService;
        _psicologoService = psicologoService;
        _userManager = userManager;
        _logger = logger;
    }

    public Consulta? Consulta { get; set; }
    public ProntuarioEletronico? Prontuario { get; set; }
    public string? MensagemErro { get; set; }
    public string MeuNome { get; set; } = "Participante";
    public string NomeRemoto { get; set; } = "Participante";
    public string MeuPapel { get; set; } = "Cliente";
    public string? RoomName { get; set; }
    public bool SouPsicologoOuAdmin { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await _videoConsultaService.EstaHabilitadoAsync())
        {
            MensagemErro = "Videochamadas online estão desabilitadas nas configurações do sistema.";
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        Consulta = await _videoConsultaService.ObterConsultaComAcessoAsync(id, user, User.IsInRole("Admin"));
        if (Consulta == null)
        {
            MensagemErro = "Consulta não encontrada ou você não tem permissão para acessar esta sala.";
            return Page();
        }
        if (Consulta.Formato != FormatoConsulta.Online)
        {
            MensagemErro = "Esta consulta não é online e não possui sala de vídeo.";
            return Page();
        }
        if (Consulta.Status == StatusConsulta.Cancelada)
        {
            MensagemErro = "Esta consulta foi cancelada.";
            return Page();
        }

        try
        {
            await _videoConsultaService.GarantirSalaAsync(Consulta);
            RoomName = Consulta.VideoRoomName;

            SouPsicologoOuAdmin = User.IsInRole("Admin") || User.IsInRole("Psicologo");
            var nomePsicologo = Consulta.Psicologo?.Nome?.Trim();
            var nomePaciente = Consulta.Paciente?.Nome?.Trim();

            if (string.IsNullOrWhiteSpace(nomePsicologo))
                nomePsicologo = "Psicóloga";
            if (string.IsNullOrWhiteSpace(nomePaciente))
                nomePaciente = "Paciente";

            if (SouPsicologoOuAdmin)
            {
                MeuNome = nomePsicologo;
                NomeRemoto = nomePaciente;
                MeuPapel = User.IsInRole("Admin") ? "Admin" : "Psicologo";
                Prontuario = await _prontuarioService.ObterPorConsultaAsync(id);
            }
            else
            {
                MeuNome = nomePaciente;
                NomeRemoto = nomePsicologo;
                MeuPapel = "Cliente";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao preparar sala de vídeo da consulta {Id}", id);
            MensagemErro = "Não foi possível preparar a sala de vídeo.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSalvarProntuarioAsync(
        int id,
        int prontuarioId,
        int pacienteId,
        int psicologoId,
        int consultaId,
        string queixaPrincipal,
        string observacoes,
        string? evolucao,
        string? intervencoes,
        string? planoTerapeutico,
        string? estadoEmocional,
        string? medicamentosAtuais,
        string? proximaSessao,
        string? tipoAtendimento)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Psicologo"))
            return new JsonResult(new { ok = false, message = "Sem permissão." }) { StatusCode = 403 };

        if (!await _configuracaoService.ObterValorBoolAsync("Prontuario.Habilitado", true))
            return new JsonResult(new { ok = false, message = "Prontuário eletrônico desabilitado." }) { StatusCode = 400 };

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { ok = false, message = "Usuário não autenticado." }) { StatusCode = 401 };

        var consulta = await _videoConsultaService.ObterConsultaComAcessoAsync(id, user, User.IsInRole("Admin"));
        if (consulta == null)
            return new JsonResult(new { ok = false, message = "Consulta não encontrada." }) { StatusCode = 404 };

        if (string.IsNullOrWhiteSpace(queixaPrincipal) || string.IsNullOrWhiteSpace(observacoes))
            return new JsonResult(new { ok = false, message = "Queixa principal e observações são obrigatórias." }) { StatusCode = 400 };

        try
        {
            var psicologoLogado = await ObterPsicologoLogadoAsync();
            var alvoPsicologoId = User.IsInRole("Admin")
                ? (psicologoId > 0 ? psicologoId : consulta.PsicologoId)
                : (psicologoLogado?.Id ?? consulta.PsicologoId);

            if (!User.IsInRole("Admin") && psicologoLogado == null)
                return new JsonResult(new { ok = false, message = "Psicólogo não identificado." }) { StatusCode = 403 };

            ProntuarioEletronico prontuario;
            if (prontuarioId > 0)
            {
                var existente = await _prontuarioService.ObterPorIdAsync(prontuarioId);
                if (existente == null)
                    return new JsonResult(new { ok = false, message = "Prontuário não encontrado." }) { StatusCode = 404 };
                if (existente.Finalizado)
                    return new JsonResult(new { ok = false, message = "Prontuário finalizado não pode ser editado." }) { StatusCode = 400 };
                if (!User.IsInRole("Admin") && existente.PsicologoId != alvoPsicologoId)
                    return new JsonResult(new { ok = false, message = "Sem permissão neste prontuário." }) { StatusCode = 403 };

                existente.QueixaPrincipal = queixaPrincipal.Trim();
                existente.Observacoes = observacoes.Trim();
                existente.Evolucao = NullIfEmpty(evolucao);
                existente.Intervencoes = NullIfEmpty(intervencoes);
                existente.PlanoTerapeutico = NullIfEmpty(planoTerapeutico);
                existente.EstadoEmocional = NullIfEmpty(estadoEmocional);
                existente.MedicamentosAtuais = NullIfEmpty(medicamentosAtuais);
                existente.ProximaSessao = NullIfEmpty(proximaSessao);
                existente.TipoAtendimento = string.IsNullOrWhiteSpace(tipoAtendimento) ? "Online" : tipoAtendimento.Trim();
                existente.ConsultaId = consulta.Id;
                existente.PacienteId = consulta.PacienteId;
                existente.DataAtualizacao = DateTime.Now;
                prontuario = await _prontuarioService.AtualizarProntuarioAsync(existente);
            }
            else
            {
                var porConsulta = await _prontuarioService.ObterPorConsultaAsync(consulta.Id);
                if (porConsulta != null)
                {
                    if (porConsulta.Finalizado)
                        return new JsonResult(new { ok = false, message = "Prontuário finalizado não pode ser editado." }) { StatusCode = 400 };

                    porConsulta.QueixaPrincipal = queixaPrincipal.Trim();
                    porConsulta.Observacoes = observacoes.Trim();
                    porConsulta.Evolucao = NullIfEmpty(evolucao);
                    porConsulta.Intervencoes = NullIfEmpty(intervencoes);
                    porConsulta.PlanoTerapeutico = NullIfEmpty(planoTerapeutico);
                    porConsulta.EstadoEmocional = NullIfEmpty(estadoEmocional);
                    porConsulta.MedicamentosAtuais = NullIfEmpty(medicamentosAtuais);
                    porConsulta.ProximaSessao = NullIfEmpty(proximaSessao);
                    porConsulta.TipoAtendimento = string.IsNullOrWhiteSpace(tipoAtendimento) ? "Online" : tipoAtendimento.Trim();
                    porConsulta.DataAtualizacao = DateTime.Now;
                    prontuario = await _prontuarioService.AtualizarProntuarioAsync(porConsulta);
                }
                else
                {
                    prontuario = await _prontuarioService.CriarProntuarioAsync(new ProntuarioEletronico
                    {
                        PacienteId = consulta.PacienteId,
                        PsicologoId = alvoPsicologoId,
                        ConsultaId = consulta.Id,
                        DataSessao = consulta.DataHorario,
                        TipoAtendimento = string.IsNullOrWhiteSpace(tipoAtendimento) ? "Online" : tipoAtendimento.Trim(),
                        QueixaPrincipal = queixaPrincipal.Trim(),
                        Observacoes = observacoes.Trim(),
                        Evolucao = NullIfEmpty(evolucao),
                        Intervencoes = NullIfEmpty(intervencoes),
                        PlanoTerapeutico = NullIfEmpty(planoTerapeutico),
                        EstadoEmocional = NullIfEmpty(estadoEmocional),
                        MedicamentosAtuais = NullIfEmpty(medicamentosAtuais),
                        ProximaSessao = NullIfEmpty(proximaSessao),
                        DataCriacao = DateTime.Now,
                        DataAtualizacao = DateTime.Now,
                        Confidencial = true
                    });
                }
            }

            return new JsonResult(new
            {
                ok = true,
                message = "Prontuário salvo com sucesso.",
                prontuarioId = prontuario.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar prontuário na sala da consulta {Id}", id);
            return new JsonResult(new { ok = false, message = "Erro ao salvar prontuário." }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnPostGerarDeclaracaoAsync(int id)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Psicologo"))
            return new JsonResult(new { ok = false, message = "Sem permissão." }) { StatusCode = 403 };

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { ok = false, message = "Usuário não autenticado." }) { StatusCode = 401 };

        var consulta = await _videoConsultaService.ObterConsultaComAcessoAsync(id, user, User.IsInRole("Admin"));
        if (consulta == null)
            return new JsonResult(new { ok = false, message = "Consulta não encontrada." }) { StatusCode = 404 };

        try
        {
            var pdf = await _pdfService.GerarDeclaracaoComparecimentoAsync(id);
            var nome = $"Declaracao_{(consulta.Paciente?.Nome ?? "paciente").Replace(' ', '_')}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", nome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar declaração na sala {Id}", id);
            return new JsonResult(new { ok = false, message = "Erro ao gerar declaração." }) { StatusCode = 500 };
        }
    }

    public async Task<IActionResult> OnPostGerarAtestadoAsync(
        int id,
        string? cid,
        int diasAfastamento = 1,
        string? observacoes = null)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Psicologo"))
            return new JsonResult(new { ok = false, message = "Sem permissão." }) { StatusCode = 403 };

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return new JsonResult(new { ok = false, message = "Usuário não autenticado." }) { StatusCode = 401 };

        var consulta = await _videoConsultaService.ObterConsultaComAcessoAsync(id, user, User.IsInRole("Admin"));
        if (consulta == null)
            return new JsonResult(new { ok = false, message = "Consulta não encontrada." }) { StatusCode = 404 };

        if (diasAfastamento < 1) diasAfastamento = 1;
        if (diasAfastamento > 90) diasAfastamento = 90;

        try
        {
            var pdf = await _pdfService.GerarAtestadoAsync(
                id,
                cid ?? string.Empty,
                diasAfastamento,
                observacoes ?? string.Empty);
            var nome = $"Atestado_{(consulta.Paciente?.Nome ?? "paciente").Replace(' ', '_')}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", nome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar atestado na sala {Id}", id);
            return new JsonResult(new { ok = false, message = "Erro ao gerar atestado." }) { StatusCode = 500 };
        }
    }

    private async Task<Shared.Models.Psicologo?> ObterPsicologoLogadoAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        var todos = await _psicologoService.GetAllAsync();
        return todos.FirstOrDefault(p => p.UserId == user?.Id)
            ?? (user?.PsicologoId != null ? todos.FirstOrDefault(p => p.Id == user.PsicologoId) : null);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
