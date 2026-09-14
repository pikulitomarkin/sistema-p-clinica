using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class EditarConsultaModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly VideoConsultaService _videoConsultaService;
        private readonly ILogger<EditarConsultaModel> _logger;

        public EditarConsultaModel(
            AppDbContext context,
            VideoConsultaService videoConsultaService,
            ILogger<EditarConsultaModel> logger)
        {
            _context = context;
            _videoConsultaService = videoConsultaService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? PacienteNome { get; set; }
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            public int Id { get; set; }

            [Required(ErrorMessage = "Data é obrigatória")]
            [DataType(DataType.Date)]
            [Display(Name = "Data")]
            public DateTime DataConsulta { get; set; }

            [Required(ErrorMessage = "Horário é obrigatório")]
            [Display(Name = "Horário")]
            public string HoraConsulta { get; set; } = "09:00";

            [Range(15, 180, ErrorMessage = "Duração deve estar entre 15 e 180 minutos")]
            [Display(Name = "Duração (minutos)")]
            public int DuracaoMinutos { get; set; } = 50;

            [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser positivo")]
            [Display(Name = "Valor")]
            public decimal Valor { get; set; }

            [Required]
            [Display(Name = "Status")]
            public StatusConsulta Status { get; set; }

            [Required]
            [Display(Name = "Tipo")]
            public TipoConsulta Tipo { get; set; }

            [Required]
            [Display(Name = "Formato")]
            public FormatoConsulta Formato { get; set; }

            [StringLength(1000)]
            [Display(Name = "Observações")]
            public string? Observacoes { get; set; }

            [Display(Name = "Relatório da sessão")]
            public string? RelatorioSessao { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var consulta = await CarregarConsultaAsync(id);
            if (consulta == null)
            {
                TempData["Error"] = "Consulta não encontrada.";
                return RedirectToPage("/Psicologo/Consultas");
            }

            var acesso = await VerificarAcessoConsultaAsync(consulta);
            if (acesso != null)
                return acesso;

            PreencherInput(consulta);
            PacienteNome = consulta.Paciente?.Nome;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var consulta = await CarregarConsultaAsync(id);
            if (consulta == null)
            {
                TempData["Error"] = "Consulta não encontrada.";
                return RedirectToPage("/Psicologo/Consultas");
            }

            var acesso = await VerificarAcessoConsultaAsync(consulta);
            if (acesso != null)
                return acesso;

            PacienteNome = consulta.Paciente?.Nome;

            if (id != Input.Id)
            {
                TempData["Error"] = "Identificador da consulta inválido.";
                return RedirectToPage("/Psicologo/Consultas");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            if (!TimeSpan.TryParse(Input.HoraConsulta, out var hora))
            {
                ModelState.AddModelError("Input.HoraConsulta", "Horário inválido.");
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            var dataHorario = Input.DataConsulta.Date.Add(hora);

            var conflito = await _context.Consultas
                .AnyAsync(c => c.PsicologoId == consulta.PsicologoId
                               && c.Id != id
                               && c.DataHorario == dataHorario
                               && c.Status != StatusConsulta.Cancelada);

            if (conflito)
            {
                ModelState.AddModelError("Input.HoraConsulta", "Já existe outra consulta neste horário.");
                ErrorMessage = "Conflito de horário. Escolha outro horário.";
                return Page();
            }

            try
            {
                var formatoAnterior = consulta.Formato;

                consulta.DataHorario = dataHorario;
                consulta.DuracaoMinutos = Input.DuracaoMinutos;
                consulta.Valor = Input.Valor;
                consulta.Status = Input.Status;
                consulta.Tipo = Input.Tipo;
                consulta.Formato = Input.Formato;
                consulta.Observacoes = string.IsNullOrWhiteSpace(Input.Observacoes)
                    ? null
                    : Input.Observacoes.Trim();
                consulta.RelatorioSessao = string.IsNullOrWhiteSpace(Input.RelatorioSessao)
                    ? null
                    : Input.RelatorioSessao.Trim();
                consulta.DataAtualizacao = DateTime.Now;

                if (Input.Status == StatusConsulta.Cancelada && !consulta.DataCancelamento.HasValue)
                    consulta.DataCancelamento = DateTime.Now;

                if (Input.Formato == FormatoConsulta.Online)
                {
                    await _videoConsultaService.GarantirSalaAsync(consulta);
                }

                await _context.SaveChangesAsync();

                if (Input.Formato == FormatoConsulta.Online && formatoAnterior != FormatoConsulta.Online)
                    await _videoConsultaService.FinalizarSalaAposCriacaoAsync(consulta);

                TempData["Success"] = "Consulta atualizada com sucesso!";
                return RedirectToPage("/Psicologo/ConsultaDetalhes", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar consulta {ConsultaId}", id);
                ErrorMessage = "Erro ao salvar alterações. Tente novamente.";
                return Page();
            }
        }

        private async Task<Consulta?> CarregarConsultaAsync(int id)
        {
            return await _context.Consultas
                .Include(c => c.Paciente)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        private async Task<IActionResult?> VerificarAcessoConsultaAsync(Consulta consulta)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            if (User.IsInRole("Admin"))
                return null;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            if (consulta.PsicologoId != user.PsicologoId.Value)
            {
                TempData["Error"] = "Você não tem permissão para editar esta consulta.";
                return RedirectToPage("/Psicologo/Consultas");
            }

            return null;
        }

        private void PreencherInput(Consulta consulta)
        {
            Input = new InputModel
            {
                Id = consulta.Id,
                DataConsulta = consulta.DataHorario.Date,
                HoraConsulta = consulta.DataHorario.ToString("HH:mm"),
                DuracaoMinutos = consulta.DuracaoMinutos,
                Valor = consulta.Valor,
                Status = consulta.Status,
                Tipo = consulta.Tipo,
                Formato = consulta.Formato,
                Observacoes = consulta.Observacoes,
                RelatorioSessao = consulta.RelatorioSessao
            };
        }
    }
}
