using System.ComponentModel.DataAnnotations;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Account;

public class RegisterPsicologoModel : PageModel
{
    public const decimal ValorConsultaContrato = 50m;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly DocumentoCadastroService _docs;
    private readonly ILogger<RegisterPsicologoModel> _logger;

    public RegisterPsicologoModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        DocumentoCadastroService docs,
        ILogger<RegisterPsicologoModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _docs = docs;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Nome é obrigatório")]
        [Display(Name = "Nome completo")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "CRP é obrigatório")]
        [StringLength(20)]
        [Display(Name = "CRP")]
        public string CRP { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Telefone / WhatsApp")]
        public string? Telefone { get; set; }

        [Display(Name = "Especialidades")]
        [StringLength(500)]
        public string? Especialidades { get; set; }

        [Required(ErrorMessage = "Senha é obrigatória")]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "As senhas não coincidem")]
        [Display(Name = "Confirmar senha")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "Aceite os Termos de Uso da plataforma")]
        [Display(Name = "Aceite dos Termos")]
        public bool AceiteTermos { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Aceite a Política de Privacidade")]
        [Display(Name = "Aceite da Privacidade")]
        public bool AceitePrivacidade { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Aceite o Contrato Digital (consultas a R$ 50,00)")]
        [Display(Name = "Aceite do Contrato")]
        public bool AceiteContrato { get; set; }

        [Required(ErrorMessage = "Envie a CNH digital")]
        public IFormFile? DocumentoCnh { get; set; }

        [Required(ErrorMessage = "Envie a carteira do CRP")]
        public IFormFile? DocumentoCrp { get; set; }
    }

    public void OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            Redirect("/");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var emailNorm = Input.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByEmailAsync(emailNorm) != null)
        {
            ModelState.AddModelError(string.Empty, "Já existe uma conta com este e-mail.");
            return Page();
        }

        if (await _db.Psicologos.AnyAsync(p => p.CRP == Input.CRP.Trim() && p.ExcluidoEm == null))
        {
            ModelState.AddModelError(nameof(Input.CRP), "Este CRP já está cadastrado.");
            return Page();
        }

        var (okCnh, urlCnh, errCnh) = await _docs.SalvarAsync(Input.DocumentoCnh!, "cnh");
        if (!okCnh)
        {
            ModelState.AddModelError(nameof(Input.DocumentoCnh), errCnh ?? "Falha no envio da CNH.");
            return Page();
        }

        var (okCrp, urlCrp, errCrp) = await _docs.SalvarAsync(Input.DocumentoCrp!, "crp");
        if (!okCrp)
        {
            ModelState.AddModelError(nameof(Input.DocumentoCrp), errCrp ?? "Falha no envio do CRP.");
            return Page();
        }

        var agora = DateTime.UtcNow;
        var psicologo = new ClinicaPsi.Shared.Models.Psicologo
        {
            Nome = Input.Nome.Trim(),
            Email = emailNorm,
            CRP = Input.CRP.Trim(),
            Telefone = Input.Telefone?.Trim(),
            Especialidades = string.IsNullOrWhiteSpace(Input.Especialidades) ? null : Input.Especialidades.Trim(),
            ValorConsulta = ValorConsultaContrato,
            ValorContratoConsulta = ValorConsultaContrato,
            Ativo = false,
            StatusValidacao = StatusValidacaoPsicologo.Pendente,
            AceiteTermosEm = agora,
            AceiteContratoEm = agora,
            AceitePrivacidadeEm = agora,
            DocumentoCnhUrl = urlCnh,
            DocumentoCrpUrl = urlCrp,
            DataCadastro = agora,
            DataCriacao = agora,
            HorarioInicioManha = new TimeSpan(8, 0, 0),
            HorarioFimManha = new TimeSpan(12, 0, 0),
            HorarioInicioTarde = new TimeSpan(14, 0, 0),
            HorarioFimTarde = new TimeSpan(18, 0, 0),
            AtendeSegunda = true,
            AtendeTerca = true,
            AtendeQuarta = true,
            AtendeQuinta = true,
            AtendeSexta = true
        };

        _db.Psicologos.Add(psicologo);
        await _db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = emailNorm,
            Email = emailNorm,
            NomeCompleto = Input.Nome.Trim(),
            PhoneNumber = Input.Telefone?.Trim(),
            TipoUsuario = TipoUsuario.Psicologo,
            CRP = Input.CRP.Trim(),
            PsicologoId = psicologo.Id,
            EmailConfirmed = true,
            Ativo = true,
            DataCadastro = agora,
            AceiteTermosEm = agora,
            AceitePrivacidadeEm = agora
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            _db.Psicologos.Remove(psicologo);
            await _db.SaveChangesAsync();
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await _userManager.AddToRoleAsync(user, "Psicologo");
        psicologo.UserId = user.Id;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Cadastro de psicólogo pendente de validação: {Email} CRP {CRP}", emailNorm, Input.CRP);

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["Info"] = "Cadastro enviado! Aguarde a validação do admin com CNH e CRP para começar a atender.";
        return LocalRedirect("/psicologo/aguardando-validacao");
    }
}
