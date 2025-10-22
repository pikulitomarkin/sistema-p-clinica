using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;
using System.ComponentModel.DataAnnotations;

namespace ClinicaPsi.Web.Pages.Admin.Usuarios
{
    [Authorize(Roles = "Admin")]
    public class EditarModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditoriaService _auditoriaService;

        public EditarModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditoriaService auditoriaService)
        {
            _context = context;
            _userManager = userManager;
            _auditoriaService = auditoriaService;
        }

        [BindProperty]
        public EditarUsuarioInputModel Input { get; set; } = new();

        public ApplicationUser? UsuarioAtual { get; set; }
        public List<ClinicaPsi.Shared.Models.Psicologo> PsicologosDisponiveis { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            UsuarioAtual = await _userManager.FindByIdAsync(id);
            if (UsuarioAtual == null)
            {
                return NotFound();
            }

            await CarregarPsicologosDisponiveisAsync();
            await PreencherFormularioAsync(UsuarioAtual);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                UsuarioAtual = await _userManager.FindByIdAsync(Input.UserId);
                await CarregarPsicologosDisponiveisAsync();
                return Page();
            }

            var usuario = await _userManager.FindByIdAsync(Input.UserId);
            if (usuario == null)
            {
                return NotFound();
            }

            UsuarioAtual = usuario;

            // Validações específicas
            if (Input.TipoUsuario == TipoUsuario.Psicologo && !Input.PsicologoId.HasValue)
            {
                ModelState.AddModelError("Input.PsicologoId", "Selecione um psicólogo para associar ao usuário.");
                await CarregarPsicologosDisponiveisAsync();
                return Page();
            }

            // Verificar se mudou o email e se já existe outro usuário com esse email
            if (usuario.Email != Input.Email)
            {
                var usuarioExistente = await _userManager.FindByEmailAsync(Input.Email);
                if (usuarioExistente != null && usuarioExistente.Id != usuario.Id)
                {
                    ModelState.AddModelError("Input.Email", "Já existe outro usuário cadastrado com este email.");
                    await CarregarPsicologosDisponiveisAsync();
                    return Page();
                }
            }

            try
            {
                var tipoAnterior = usuario.TipoUsuario;
                var ativoAnterior = usuario.Ativo;

                // Atualizar dados básicos
                usuario.NomeCompleto = Input.NomeCompleto;
                usuario.Email = Input.Email;
                usuario.UserName = Input.Email;
                usuario.NormalizedEmail = Input.Email.ToUpper();
                usuario.NormalizedUserName = Input.Email.ToUpper();
                usuario.TipoUsuario = Input.TipoUsuario;
                usuario.Ativo = Input.Ativo;

                var resultUpdate = await _userManager.UpdateAsync(usuario);
                if (!resultUpdate.Succeeded)
                {
                    foreach (var error in resultUpdate.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    await CarregarPsicologosDisponiveisAsync();
                    return Page();
                }

                // Alterar senha se fornecida
                if (!string.IsNullOrWhiteSpace(Input.NovaSenha))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
                    var resultSenha = await _userManager.ResetPasswordAsync(usuario, token, Input.NovaSenha);
                    
                    if (!resultSenha.Succeeded)
                    {
                        foreach (var error in resultSenha.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        await CarregarPsicologosDisponiveisAsync();
                        return Page();
                    }
                }

                // Atualizar roles se o tipo mudou
                if (tipoAnterior != Input.TipoUsuario)
                {
                    var rolesAtuais = await _userManager.GetRolesAsync(usuario);
                    await _userManager.RemoveFromRolesAsync(usuario, rolesAtuais);
                    await _userManager.AddToRoleAsync(usuario, Input.TipoUsuario.ToString());
                }

                // Processar alterações específicas por tipo
                if (Input.TipoUsuario == TipoUsuario.Cliente)
                {
                    await AtualizarPacienteAsync(usuario);
                }
                else if (Input.TipoUsuario == TipoUsuario.Psicologo)
                {
                    await AtualizarVinculoPsicologoAsync(usuario);
                }

                // Registrar auditoria
                var adminUser = await _userManager.GetUserAsync(User);
                
                await _auditoriaService.RegistrarAcaoAsync(
                    adminId: adminUser?.Id ?? "Sistema",
                    adminNome: adminUser?.NomeCompleto ?? "Sistema",
                    usuarioAfetadoId: usuario.Id,
                    usuarioAfetadoNome: usuario.NomeCompleto,
                    usuarioAfetadoEmail: usuario.Email ?? string.Empty,
                    acao: TipoAcaoAuditoria.EdicaoUsuario,
                    detalhes: $"Usuário editado. Tipo: {Input.TipoUsuario}, Ativo: {Input.Ativo}, Senha alterada: {!string.IsNullOrWhiteSpace(Input.NovaSenha)}"
                );

                TempData["Success"] = "Usuário atualizado com sucesso!";
                return RedirectToPage("/Admin/Usuarios");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Erro ao atualizar usuário: {ex.Message}");
                await CarregarPsicologosDisponiveisAsync();
                return Page();
            }
        }

        private async Task PreencherFormularioAsync(ApplicationUser usuario)
        {
            Input.UserId = usuario.Id;
            Input.NomeCompleto = usuario.NomeCompleto;
            Input.Email = usuario.Email ?? string.Empty;
            Input.TipoUsuario = usuario.TipoUsuario;
            Input.Ativo = usuario.Ativo;

            // Carregar dados específicos
            if (usuario.TipoUsuario == TipoUsuario.Cliente && usuario.PacienteId.HasValue)
            {
                var paciente = await _context.Pacientes.FindAsync(usuario.PacienteId.Value);
                if (paciente != null)
                {
                    Input.CPF = paciente.CPF;
                    Input.Telefone = paciente.Telefone;
                    Input.DataNascimento = paciente.DataNascimento;
                    Input.Endereco = paciente.Endereco;
                }
            }
            else if (usuario.TipoUsuario == TipoUsuario.Psicologo && usuario.PsicologoId.HasValue)
            {
                Input.PsicologoId = usuario.PsicologoId.Value;
            }
        }

        private async Task AtualizarPacienteAsync(ApplicationUser usuario)
        {
            Paciente? paciente = null;

            if (usuario.PacienteId.HasValue)
            {
                paciente = await _context.Pacientes.FindAsync(usuario.PacienteId.Value);
            }

            if (paciente == null)
            {
                // Criar novo paciente
                paciente = new Paciente
                {
                    Nome = Input.NomeCompleto,
                    Email = Input.Email,
                    CPF = Input.CPF ?? string.Empty,
                    Telefone = Input.Telefone ?? string.Empty,
                    DataNascimento = Input.DataNascimento ?? DateTime.Now,
                    Endereco = Input.Endereco ?? string.Empty
                };
                _context.Pacientes.Add(paciente);
                await _context.SaveChangesAsync();

                usuario.PacienteId = paciente.Id;
                await _userManager.UpdateAsync(usuario);
            }
            else
            {
                // Atualizar paciente existente
                paciente.Nome = Input.NomeCompleto;
                paciente.Email = Input.Email;
                paciente.CPF = Input.CPF ?? paciente.CPF;
                paciente.Telefone = Input.Telefone ?? paciente.Telefone;
                if (Input.DataNascimento.HasValue)
                {
                    paciente.DataNascimento = Input.DataNascimento.Value;
                }
                paciente.Endereco = Input.Endereco ?? paciente.Endereco;

                await _context.SaveChangesAsync();
            }
        }

        private async Task AtualizarVinculoPsicologoAsync(ApplicationUser usuario)
        {
            if (!Input.PsicologoId.HasValue)
                return;

            var psicologoIdAnterior = usuario.PsicologoId;
            
            // Se mudou o psicólogo vinculado
            if (psicologoIdAnterior != Input.PsicologoId.Value)
            {
                // Desvincular psicólogo anterior se houver (não aplicável nesta versão - Psicologo não tem UsuarioId)
                
                // Vincular novo psicólogo
                usuario.PsicologoId = Input.PsicologoId.Value;
                await _userManager.UpdateAsync(usuario);
                await _context.SaveChangesAsync();
            }
        }

        private async Task CarregarPsicologosDisponiveisAsync()
        {
            // Carregar todos os psicólogos ativos (nota: o modelo Psicologo não tem campo UsuarioId)
            PsicologosDisponiveis = await _context.Psicologos
                .Where(p => p.Ativo)
                .OrderBy(p => p.Nome)
                .ToListAsync();
        }

        public class EditarUsuarioInputModel
        {
            [Required]
            public string UserId { get; set; } = string.Empty;

            [Required(ErrorMessage = "O nome completo é obrigatório")]
            public string NomeCompleto { get; set; } = string.Empty;

            [Required(ErrorMessage = "O email é obrigatório")]
            [EmailAddress(ErrorMessage = "Email inválido")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Selecione o tipo de usuário")]
            public TipoUsuario TipoUsuario { get; set; }

            public bool Ativo { get; set; } = true;

            // Campos opcionais para alterar senha
            [MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres")]
            public string? NovaSenha { get; set; }

            [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não conferem")]
            public string? ConfirmarSenha { get; set; }

            // Campos para Cliente
            public string? CPF { get; set; }
            public string? Telefone { get; set; }
            public DateTime? DataNascimento { get; set; }
            public string? Endereco { get; set; }

            // Campo para Psicólogo
            public int? PsicologoId { get; set; }
        }
    }
}
