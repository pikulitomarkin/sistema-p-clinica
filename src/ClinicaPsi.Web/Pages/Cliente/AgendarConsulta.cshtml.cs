using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;
using System.Globalization;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Cliente
{
    [Authorize]
    public class AgendarConsultaModel : PageModel
    {
        private static readonly StatusConsulta[] StatusOcupados =
        {
            StatusConsulta.Agendada,
            StatusConsulta.Confirmada,
            StatusConsulta.Reagendada
        };

        private readonly AppDbContext _context;
        private readonly ConfiguracaoService _configuracaoService;
        private readonly VideoConsultaService _videoConsultaService;

        public AgendarConsultaModel(
            AppDbContext context,
            ConfiguracaoService configuracaoService,
            VideoConsultaService videoConsultaService)
        {
            _context = context;
            _configuracaoService = configuracaoService;
            _videoConsultaService = videoConsultaService;
        }

        public List<ClinicaPsi.Shared.Models.Psicologo> Psicologos { get; set; } = new();
        public List<Consulta> ConsultasExistentes { get; set; } = new();
        public Paciente? PacienteAtual { get; set; }
        public DateTime DataSelecionada { get; set; } = DateTime.Today;
        public string DataPadrao { get; set; } = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            public int PsicologoId { get; set; }

            /// <summary>Data escolhida no input type=date (yyyy-MM-dd).</summary>
            public string Data { get; set; } = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            /// <summary>Data/hora do slot (ISO yyyy-MM-ddTHH:mm) — string evita falha do binder pt-BR.</summary>
            public string? DataHorario { get; set; }

            public int DuracaoMinutos { get; set; } = 50;
            public TipoConsulta Tipo { get; set; } = TipoConsulta.Normal;
            public FormatoConsulta Formato { get; set; } = FormatoConsulta.Presencial;
            public string? Observacoes { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(DateTime? data, int? psicologoId)
        {
            try
            {
                if (data.HasValue && data.Value.Year > 1)
                    DataSelecionada = data.Value.Date;
                else
                    DataSelecionada = DateTime.Today;

                DataPadrao = DataSelecionada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Input.Data = DataPadrao;

                if (psicologoId.HasValue && psicologoId.Value > 0)
                    Input.PsicologoId = psicologoId.Value;

                await CarregarDadosAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar AgendarConsulta: {ex.Message}");
                Psicologos = new List<ClinicaPsi.Shared.Models.Psicologo>();
                ConsultasExistentes = new List<Consulta>();
                DataPadrao = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Input.Data = DataPadrao;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!TryResolverDataHorario(out var dataHorario, out var erroData))
            {
                ModelState.AddModelError("", erroData);
                await CarregarDadosAsync();
                return Page();
            }

            Input.DataHorario = dataHorario.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

            if (!ModelState.IsValid)
            {
                await CarregarDadosAsync();
                return Page();
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.PacienteId == null)
                {
                    ModelState.AddModelError("", "Usuário não está associado a um paciente.");
                    await CarregarDadosAsync();
                    return Page();
                }

                var paciente = await _context.Pacientes.FindAsync(user.PacienteId.Value);
                if (paciente == null)
                {
                    ModelState.AddModelError("", "Paciente não encontrado.");
                    await CarregarDadosAsync();
                    return Page();
                }

                var psicologo = await _context.Psicologos.FindAsync(Input.PsicologoId);
                if (psicologo == null || !psicologo.Ativo || psicologo.ExcluidoEm != null)
                {
                    ModelState.AddModelError("", "Psicólogo não encontrado.");
                    await CarregarDadosAsync();
                    return Page();
                }

                var configConsultas = await _configuracaoService.ObterConfigConsultasAsync();
                var duracao = Input.DuracaoMinutos > 0
                    ? Input.DuracaoMinutos
                    : (configConsultas.DuracaoPadrao > 0 ? configConsultas.DuracaoPadrao : 50);

                if (!await HorarioEstaDisponivelAsync(psicologo, dataHorario, duracao))
                {
                    ModelState.AddModelError("", "Este horário não está mais disponível. Selecione outro horário.");
                    await CarregarDadosAsync();
                    return Page();
                }

                var tipoConsulta = TipoConsulta.Normal;
                var valorConsulta = psicologo.ValorConsulta;

                var consulta = new Consulta
                {
                    PacienteId = user.PacienteId.Value,
                    PsicologoId = Input.PsicologoId,
                    DataHorario = dataHorario,
                    DuracaoMinutos = duracao,
                    Valor = valorConsulta,
                    Status = StatusConsulta.Agendada,
                    Tipo = tipoConsulta,
                    Formato = Input.Formato,
                    Observacoes = Input.Observacoes,
                    DataAgendamento = DateTime.Now,
                    DataCriacao = DateTime.Now,
                    NotificacaoEnviada = false,
                    ConfirmacaoRecebida = false
                };

                await _videoConsultaService.GarantirSalaAsync(consulta);
                _context.Consultas.Add(consulta);

                await _context.SaveChangesAsync();
                await _videoConsultaService.FinalizarSalaAposCriacaoAsync(consulta);

                TempData["Success"] = Input.Formato == FormatoConsulta.Online
                    ? "Consulta online agendada! Use o botão de videochamada em Minhas Consultas no horário."
                    : "Consulta agendada com sucesso!";
                return RedirectToPage("MinhasConsultas");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao agendar consulta: " + ex.Message);
                await CarregarDadosAsync();
                return Page();
            }
        }

        /// <summary>
        /// Retorna slots livres para o psicólogo na data (ISO yyyy-MM-dd).
        /// Parse explícito evita falha do model binder com cultura pt-BR.
        /// </summary>
        public async Task<IActionResult> OnGetHorariosDisponiveisAsync(int psicologoId, string? data)
        {
            if (psicologoId <= 0)
                return new JsonResult(Array.Empty<object>());

            if (!TryParseDataIso(data, out var dataConsulta))
                return new JsonResult(Array.Empty<object>());

            var psicologo = await _context.Psicologos
                .FirstOrDefaultAsync(p => p.Id == psicologoId && p.Ativo && p.ExcluidoEm == null);
            if (psicologo == null)
                return NotFound();

            var configConsultas = await _configuracaoService.ObterConfigConsultasAsync();
            var duracao = configConsultas.DuracaoPadrao > 0 ? configConsultas.DuracaoPadrao : 50;
            var intervalo = Math.Max(0, configConsultas.IntervaloMinimo);

            var consultasOcupadas = await ObterConsultasOcupadasAsync(psicologoId, dataConsulta);
            var horariosDisponiveis = GerarHorariosDisponiveis(
                psicologo, dataConsulta, consultasOcupadas, configConsultas, duracao, intervalo);

            return new JsonResult(horariosDisponiveis.Select(h => new
            {
                valor = h.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                texto = h.ToString("HH:mm", CultureInfo.InvariantCulture)
            }));
        }

        private async Task CarregarDadosAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.PacienteId != null)
            {
                PacienteAtual = await _context.Pacientes
                    .FindAsync(user.PacienteId.Value);
            }

            Psicologos = await _context.Psicologos
                .Where(p => p.Ativo && p.ExcluidoEm == null)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            if (TryParseDataIso(Input.Data, out var dataInput))
                DataSelecionada = dataInput;
            else if (DataSelecionada.Year <= 1)
                DataSelecionada = DateTime.Today;

            DataPadrao = DataSelecionada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(Input.Data) || Input.Data.StartsWith("0001", StringComparison.Ordinal))
                Input.Data = DataPadrao;

            ConsultasExistentes = await _context.Consultas
                .Include(c => c.Psicologo)
                .Where(c => c.DataHorario.Date == DataSelecionada.Date &&
                           StatusOcupados.Contains(c.Status))
                .OrderBy(c => c.DataHorario)
                .ToListAsync();
        }

        private bool TryResolverDataHorario(out DateTime dataHorario, out string erro)
        {
            dataHorario = default;
            erro = string.Empty;

            var raw = Input.DataHorario;
            if (string.IsNullOrWhiteSpace(raw) &&
                Request.Form.TryGetValue("Input.DataHorario", out var formValue))
            {
                raw = formValue.ToString();
            }

            if (TryParseDataHoraIso(raw, out dataHorario))
                return true;

            erro = "Selecione uma data e um horário disponíveis.";
            return false;
        }

        private static bool TryParseDataHoraIso(string? valor, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(valor))
                return false;

            if (DateTime.TryParseExact(
                    valor.Trim(),
                    new[]
                    {
                        "yyyy-MM-ddTHH:mm",
                        "yyyy-MM-ddTHH:mm:ss",
                        "yyyy-MM-dd HH:mm",
                        "yyyy-MM-dd HH:mm:ss",
                        "dd/MM/yyyy HH:mm"
                    },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out result))
            {
                return result.Year > 1;
            }

            return DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result)
                   && result.Year > 1;
        }

        private async Task<bool> HorarioEstaDisponivelAsync(
            ClinicaPsi.Shared.Models.Psicologo psicologo,
            DateTime dataHorario,
            int duracao)
        {
            if (dataHorario <= DateTime.Now)
                return false;

            var configConsultas = await _configuracaoService.ObterConfigConsultasAsync();
            var intervalo = Math.Max(0, configConsultas.IntervaloMinimo);
            var consultasOcupadas = await ObterConsultasOcupadasAsync(psicologo.Id, dataHorario.Date);
            var disponiveis = GerarHorariosDisponiveis(
                psicologo, dataHorario.Date, consultasOcupadas, configConsultas, duracao, intervalo);

            return disponiveis.Any(h => h == dataHorario);
        }

        private async Task<List<(DateTime Inicio, int Duracao)>> ObterConsultasOcupadasAsync(int psicologoId, DateTime data)
        {
            var inicioDia = data.Date;
            var fimDia = inicioDia.AddDays(1);

            var ocupadas = await _context.Consultas
                .AsNoTracking()
                .Where(c => c.PsicologoId == psicologoId &&
                            c.DataHorario >= inicioDia &&
                            c.DataHorario < fimDia &&
                            StatusOcupados.Contains(c.Status))
                .Select(c => new { c.DataHorario, c.DuracaoMinutos })
                .ToListAsync();

            return ocupadas
                .Select(c => (c.DataHorario, c.DuracaoMinutos))
                .ToList();
        }

        private static List<DateTime> GerarHorariosDisponiveis(
            ClinicaPsi.Shared.Models.Psicologo psicologo,
            DateTime data,
            List<(DateTime Inicio, int Duracao)> consultasOcupadas,
            ConsultasConfig configConsultas,
            int duracao,
            int intervalo)
        {
            var horarios = new List<DateTime>();
            var diaSemana = data.DayOfWeek;

            if (diaSemana == DayOfWeek.Saturday && !configConsultas.PermitirSabado)
                return horarios;

            if (diaSemana == DayOfWeek.Sunday && !configConsultas.PermitirDomingo)
                return horarios;

            if (!PsicologoAtendeNoDia(psicologo, diaSemana, out var usarFallbackClinica) && !usarFallbackClinica)
                return horarios;

            var passo = Math.Max(1, duracao + intervalo);
            var agora = DateTime.Now;
            var periodos = ObterPeriodosAtendimento(psicologo, data, configConsultas, usarFallbackClinica);

            foreach (var (inicio, fim) in periodos)
            {
                for (var slotInicio = inicio; slotInicio.AddMinutes(duracao) <= fim; slotInicio = slotInicio.AddMinutes(passo))
                {
                    if (slotInicio <= agora)
                        continue;

                    if (SlotConflita(slotInicio, duracao, intervalo, consultasOcupadas))
                        continue;

                    horarios.Add(slotInicio);
                }
            }

            return horarios.Distinct().OrderBy(h => h).ToList();
        }

        private static bool PsicologoAtendeNoDia(
            ClinicaPsi.Shared.Models.Psicologo psicologo,
            DayOfWeek diaSemana,
            out bool usarFallbackClinica)
        {
            usarFallbackClinica = false;

            var atendeNoDia = diaSemana switch
            {
                DayOfWeek.Monday => psicologo.AtendeSegunda,
                DayOfWeek.Tuesday => psicologo.AtendeTerca,
                DayOfWeek.Wednesday => psicologo.AtendeQuarta,
                DayOfWeek.Thursday => psicologo.AtendeQuinta,
                DayOfWeek.Friday => psicologo.AtendeSexta,
                DayOfWeek.Saturday => psicologo.AtendeSabado,
                DayOfWeek.Sunday => psicologo.AtendeDomingo,
                _ => false
            };

            // Se nenhum dia/período está marcado (cadastro incompleto), usa configs da clínica
            var nenhumDiaConfigurado =
                !psicologo.AtendeSegunda && !psicologo.AtendeTerca && !psicologo.AtendeQuarta &&
                !psicologo.AtendeQuinta && !psicologo.AtendeSexta && !psicologo.AtendeSabado &&
                !psicologo.AtendeDomingo;

            var nenhumPeriodo = !psicologo.AtendeManha && !psicologo.AtendeTarde;

            if (nenhumDiaConfigurado || nenhumPeriodo)
            {
                usarFallbackClinica = true;
                return true;
            }

            return atendeNoDia;
        }

        private static List<(DateTime Inicio, DateTime Fim)> ObterPeriodosAtendimento(
            ClinicaPsi.Shared.Models.Psicologo psicologo,
            DateTime data,
            ConsultasConfig config,
            bool forcarFallbackClinica)
        {
            var periodos = new List<(DateTime Inicio, DateTime Fim)>();

            if (!forcarFallbackClinica && (psicologo.AtendeManha || psicologo.AtendeTarde))
            {
                if (psicologo.AtendeManha && psicologo.HorarioInicioManha < psicologo.HorarioFimManha)
                {
                    periodos.Add((
                        data.Date.Add(psicologo.HorarioInicioManha),
                        data.Date.Add(psicologo.HorarioFimManha)));
                }

                if (psicologo.AtendeTarde && psicologo.HorarioInicioTarde < psicologo.HorarioFimTarde)
                {
                    periodos.Add((
                        data.Date.Add(psicologo.HorarioInicioTarde),
                        data.Date.Add(psicologo.HorarioFimTarde)));
                }
            }

            if (periodos.Count == 0)
            {
                // Fallback: horário único da clínica (Consultas.HorarioInicio / HorarioFim)
                if (!TryParseHora(config.HorarioInicio, out var inicioClinica))
                    inicioClinica = new TimeSpan(9, 0, 0);
                if (!TryParseHora(config.HorarioFim, out var fimClinica))
                    fimClinica = new TimeSpan(17, 0, 0);

                if (inicioClinica < fimClinica)
                    periodos.Add((data.Date.Add(inicioClinica), data.Date.Add(fimClinica)));
            }

            return periodos;
        }

        private static bool SlotConflita(
            DateTime slotInicio,
            int duracaoSlot,
            int intervalo,
            List<(DateTime Inicio, int Duracao)> ocupadas)
        {
            var slotFimComIntervalo = slotInicio.AddMinutes(duracaoSlot + intervalo);

            foreach (var (inicio, duracaoExistente) in ocupadas)
            {
                var duracao = duracaoExistente > 0 ? duracaoExistente : duracaoSlot;
                var ocupadoInicio = inicio;
                var ocupadoFimComIntervalo = inicio.AddMinutes(duracao + intervalo);

                // Cruza se o novo slot invade o bloco ocupado (incluindo intervalo mínimo)
                if (slotInicio < ocupadoFimComIntervalo && slotFimComIntervalo > ocupadoInicio)
                    return true;
            }

            return false;
        }

        private static bool TryParseDataIso(string? data, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(data))
                return false;

            if (DateTime.TryParseExact(
                    data.Trim(),
                    new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out result))
            {
                result = result.Date;
                return result.Year > 1;
            }

            // Último recurso: parse genérico (pode falhar em pt-BR com ISO)
            if (DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
            {
                result = result.Date;
                return result.Year > 1;
            }

            return false;
        }

        private static bool TryParseHora(string? valor, out TimeSpan hora)
        {
            hora = default;
            if (string.IsNullOrWhiteSpace(valor))
                return false;

            if (TimeSpan.TryParseExact(valor.Trim(), new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" },
                    CultureInfo.InvariantCulture, out hora))
                return true;

            if (TimeOnly.TryParse(valor, CultureInfo.InvariantCulture, out var timeOnly))
            {
                hora = timeOnly.ToTimeSpan();
                return true;
            }

            return false;
        }
    }
}
