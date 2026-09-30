using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Application.Services.MercadoPago;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Cliente;

[Authorize(Roles = "Cliente")]
public class CarteiraModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CarteiraService _carteira;
    private readonly MercadoPagoService _mp;

    public CarteiraModel(AppDbContext db, CarteiraService carteira, MercadoPagoService mp)
    {
        _db = db;
        _carteira = carteira;
        _mp = mp;
    }

    public decimal Saldo { get; set; }
    public List<MovimentacaoCarteira> Movimentacoes { get; set; } = new();
    public bool MpDisponivel { get; set; }
    public bool UseSandbox { get; set; }
    public string? Erro { get; set; }
    public string? Sucesso { get; set; }

    public MovimentacaoCarteira? PixPendente { get; set; }

    [BindProperty]
    public DepositInput Input { get; set; } = new();

    public class DepositInput
    {
        [Range(5, 5000, ErrorMessage = "Depósito entre R$ 5,00 e R$ 5.000,00")]
        public decimal Valor { get; set; } = 50m;
    }

    public async Task<IActionResult> OnGetAsync(int? movId = null)
    {
        var pacienteId = await GetPacienteIdAsync();
        if (pacienteId is null)
            return RedirectToPage("/Account/Login");

        await CarregarAsync(pacienteId.Value, movId);
        Sucesso = TempData["Success"] as string;
        Erro = TempData["Error"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostDepositarAsync()
    {
        var pacienteId = await GetPacienteIdAsync();
        if (pacienteId is null)
            return RedirectToPage("/Account/Login");

        if (!ModelState.IsValid)
        {
            await CarregarAsync(pacienteId.Value);
            return Page();
        }

        MpDisponivel = _mp.IsConfigured;
        if (!MpDisponivel)
        {
            TempData["Error"] = "Pagamentos PIX indisponíveis no momento.";
            return RedirectToPage();
        }

        var paciente = await _db.Pacientes.FindAsync(pacienteId.Value);
        if (paciente is null)
        {
            TempData["Error"] = "Paciente não encontrado.";
            return RedirectToPage();
        }

        try
        {
            var pix = await _mp.CriarDepositoPixAsync(pacienteId.Value, paciente, Input.Valor);
            if (pix.JaAprovado)
            {
                TempData["Success"] = $"Depósito de {pix.Valor:C} creditado na carteira.";
                return RedirectToPage();
            }

            TempData["Success"] = "PIX gerado. Pague com o QR Code ou copia-e-cola abaixo.";
            return RedirectToPage(new { movId = pix.MovimentacaoId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Não foi possível gerar o PIX. " + ex.Message;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostVerificarAsync(int movimentacaoId)
    {
        var pacienteId = await GetPacienteIdAsync();
        if (pacienteId is null)
            return RedirectToPage("/Account/Login");

        var carteira = await _carteira.ObterOuCriarAsync(pacienteId.Value);
        var mov = await _db.MovimentacoesCarteira
            .FirstOrDefaultAsync(m => m.Id == movimentacaoId && m.CarteiraClienteId == carteira.Id);

        if (mov is null)
        {
            TempData["Error"] = "Depósito não encontrado.";
            return RedirectToPage();
        }

        if (mov.Status == StatusMovimentacaoCarteira.Confirmada)
        {
            TempData["Success"] = $"Depósito de {mov.Valor:C} já está na carteira.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(mov.MercadoPagoPaymentId) || !_mp.IsConfigured)
        {
            TempData["Error"] = "Aguardando confirmação do pagamento PIX.";
            return RedirectToPage(new { movId = movimentacaoId });
        }

        try
        {
            await _mp.SyncDepositoByPaymentIdAsync(mov.Id, mov.MercadoPagoPaymentId);
            await _db.Entry(mov).ReloadAsync();
            if (mov.Status == StatusMovimentacaoCarteira.Confirmada)
            {
                TempData["Success"] = $"Depósito de {mov.Valor:C} creditado com sucesso!";
                return RedirectToPage();
            }

            TempData["Success"] = "Pagamento ainda pendente. Se já pagou, aguarde alguns segundos e verifique de novo.";
            return RedirectToPage(new { movId = movimentacaoId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Não foi possível verificar o pagamento. " + ex.Message;
            return RedirectToPage(new { movId = movimentacaoId });
        }
    }

    private async Task CarregarAsync(int pacienteId, int? movId = null)
    {
        var (carteira, movs) = await _carteira.ObterComHistoricoAsync(pacienteId);
        Saldo = carteira.Saldo;
        Movimentacoes = movs;
        MpDisponivel = _mp.IsConfigured;
        UseSandbox = _mp.UseSandbox;

        if (movId.HasValue)
        {
            PixPendente = movs.FirstOrDefault(m => m.Id == movId.Value)
                ?? await _db.MovimentacoesCarteira
                    .FirstOrDefaultAsync(m => m.Id == movId.Value && m.CarteiraClienteId == carteira.Id);
        }
        else
        {
            PixPendente = movs.FirstOrDefault(m =>
                m.Tipo == TipoMovimentacaoCarteira.Credito &&
                m.Status == StatusMovimentacaoCarteira.Pendente &&
                !string.IsNullOrWhiteSpace(m.PixCopiaECola) &&
                (m.PixExpiraEm == null || m.PixExpiraEm > DateTime.Now));
        }
    }

    private async Task<int?> GetPacienteIdAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return null;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return user?.PacienteId;
    }
}
