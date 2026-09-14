using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class EditarPacienteModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EditarPacienteModel> _logger;

        public EditarPacienteModel(AppDbContext context, ILogger<EditarPacienteModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            public int Id { get; set; }

            [Required(ErrorMessage = "Nome é obrigatório")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 100 caracteres")]
            public string Nome { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email é obrigatório")]
            [EmailAddress(ErrorMessage = "Email inválido")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "CPF é obrigatório")]
            [Display(Name = "CPF")]
            public string CPF { get; set; } = string.Empty;

            [Required(ErrorMessage = "Telefone é obrigatório")]
            public string Telefone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Data de nascimento é obrigatória")]
            [DataType(DataType.Date)]
            [Display(Name = "Data de Nascimento")]
            public DateTime DataNascimento { get; set; }

            [StringLength(200)]
            [Display(Name = "Endereço")]
            public string? Endereco { get; set; }

            [StringLength(100)]
            [Display(Name = "Contato de Emergência")]
            public string? ContatoEmergencia { get; set; }

            [Display(Name = "Telefone de Emergência")]
            public string? TelefoneEmergencia { get; set; }

            [Display(Name = "Histórico Médico")]
            public string? HistoricoMedico { get; set; }

            [Display(Name = "Medicamentos em Uso")]
            public string? MedicamentosUso { get; set; }

            [Display(Name = "Observações")]
            public string? Observacoes { get; set; }

            public bool Ativo { get; set; } = true;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var acesso = await VerificarAcessoPacienteAsync(id);
            if (acesso != null)
                return acesso;

            var paciente = await _context.Pacientes.FindAsync(id);
            if (paciente == null)
            {
                TempData["Error"] = "Paciente não encontrado.";
                return RedirectToPage("/Psicologo/Pacientes");
            }

            PreencherInput(paciente);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var acesso = await VerificarAcessoPacienteAsync(id);
            if (acesso != null)
                return acesso;

            if (id != Input.Id)
            {
                TempData["Error"] = "Identificador do paciente inválido.";
                return RedirectToPage("/Psicologo/Pacientes");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            var paciente = await _context.Pacientes.FindAsync(id);
            if (paciente == null)
            {
                TempData["Error"] = "Paciente não encontrado.";
                return RedirectToPage("/Psicologo/Pacientes");
            }

            var email = Input.Email.Trim().ToLowerInvariant();
            var cpf = LimparCpf(Input.CPF);
            var telefone = LimparTelefone(Input.Telefone);

            if (cpf.Length != 11 || !cpf.All(char.IsDigit))
            {
                ModelState.AddModelError("Input.CPF", "CPF inválido. Deve conter 11 dígitos.");
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(telefone))
            {
                ModelState.AddModelError("Input.Telefone", "Telefone é obrigatório.");
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            var emailExiste = await _context.Pacientes
                .AnyAsync(p => p.Email == email && p.Id != id);
            if (emailExiste)
            {
                ModelState.AddModelError("Input.Email", "Já existe um paciente com este email.");
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            var cpfExiste = await _context.Pacientes
                .AnyAsync(p => p.CPF == cpf && p.Id != id);
            if (cpfExiste)
            {
                ModelState.AddModelError("Input.CPF", "Já existe um paciente com este CPF.");
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            try
            {
                paciente.Nome = Input.Nome.Trim();
                paciente.Email = email;
                paciente.CPF = cpf;
                paciente.Telefone = telefone;
                paciente.DataNascimento = Input.DataNascimento.Date;
                paciente.Endereco = string.IsNullOrWhiteSpace(Input.Endereco) ? null : Input.Endereco.Trim();
                paciente.ContatoEmergencia = string.IsNullOrWhiteSpace(Input.ContatoEmergencia)
                    ? null
                    : Input.ContatoEmergencia.Trim();
                paciente.TelefoneEmergencia = string.IsNullOrWhiteSpace(Input.TelefoneEmergencia)
                    ? null
                    : LimparTelefone(Input.TelefoneEmergencia);
                paciente.HistoricoMedico = string.IsNullOrWhiteSpace(Input.HistoricoMedico)
                    ? null
                    : Input.HistoricoMedico.Trim();
                paciente.MedicamentosUso = string.IsNullOrWhiteSpace(Input.MedicamentosUso)
                    ? null
                    : Input.MedicamentosUso.Trim();
                paciente.Observacoes = string.IsNullOrWhiteSpace(Input.Observacoes)
                    ? null
                    : Input.Observacoes.Trim();
                paciente.Ativo = Input.Ativo;
                paciente.DataAtualizacao = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Paciente atualizado com sucesso!";
                return RedirectToPage("/Psicologo/Pacientes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar paciente {PacienteId}", id);
                ErrorMessage = "Erro ao salvar alterações. Tente novamente.";
                return Page();
            }
        }

        private async Task<IActionResult?> VerificarAcessoPacienteAsync(int pacienteId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            if (User.IsInRole("Admin"))
                return null;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            var psicologoId = user.PsicologoId.Value;
            var vinculado = await _context.Consultas
                .AnyAsync(c => c.PacienteId == pacienteId && c.PsicologoId == psicologoId);

            if (!vinculado)
            {
                TempData["Error"] = "Você não tem permissão para editar este paciente.";
                return RedirectToPage("/Psicologo/Pacientes");
            }

            return null;
        }

        private void PreencherInput(Paciente paciente)
        {
            Input = new InputModel
            {
                Id = paciente.Id,
                Nome = paciente.Nome,
                Email = paciente.Email,
                CPF = FormatCpf(paciente.CPF),
                Telefone = FormatTelefone(paciente.Telefone),
                DataNascimento = paciente.DataNascimento,
                Endereco = paciente.Endereco,
                ContatoEmergencia = paciente.ContatoEmergencia,
                TelefoneEmergencia = string.IsNullOrEmpty(paciente.TelefoneEmergencia)
                    ? null
                    : FormatTelefone(paciente.TelefoneEmergencia),
                HistoricoMedico = paciente.HistoricoMedico,
                MedicamentosUso = paciente.MedicamentosUso,
                Observacoes = paciente.Observacoes,
                Ativo = paciente.Ativo
            };
        }

        private static string LimparCpf(string cpf) =>
            (cpf ?? string.Empty).Replace(".", "").Replace("-", "").Replace(" ", "").Trim();

        private static string LimparTelefone(string telefone) =>
            (telefone ?? string.Empty)
                .Replace("(", "")
                .Replace(")", "")
                .Replace("-", "")
                .Replace(" ", "")
                .Trim();

        private static string FormatCpf(string cpf)
        {
            var digits = LimparCpf(cpf);
            if (digits.Length != 11)
                return cpf;
            return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}";
        }

        private static string FormatTelefone(string telefone)
        {
            var digits = LimparTelefone(telefone);
            if (digits.Length == 11)
                return $"({digits[..2]}) {digits[2..7]}-{digits[7..]}";
            if (digits.Length == 10)
                return $"({digits[..2]}) {digits[2..6]}-{digits[6..]}";
            return telefone;
        }
    }
}
