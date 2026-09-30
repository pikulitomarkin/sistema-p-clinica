using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

public class CarteiraService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CarteiraService> _logger;

    public CarteiraService(AppDbContext db, ILogger<CarteiraService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CarteiraCliente> ObterOuCriarAsync(int pacienteId, CancellationToken ct = default)
    {
        var carteira = await _db.CarteirasCliente
            .FirstOrDefaultAsync(c => c.PacienteId == pacienteId, ct);

        if (carteira is not null)
            return carteira;

        carteira = new CarteiraCliente
        {
            PacienteId = pacienteId,
            Saldo = 0m,
            DataCriacao = DateTime.Now
        };
        _db.CarteirasCliente.Add(carteira);
        await _db.SaveChangesAsync(ct);
        return carteira;
    }

    public async Task<(CarteiraCliente Carteira, List<MovimentacaoCarteira> Movimentacoes)> ObterComHistoricoAsync(
        int pacienteId, int take = 40, CancellationToken ct = default)
    {
        var carteira = await ObterOuCriarAsync(pacienteId, ct);
        var movs = await _db.MovimentacoesCarteira
            .AsNoTracking()
            .Where(m => m.CarteiraClienteId == carteira.Id)
            .OrderByDescending(m => m.DataCriacao)
            .Take(take)
            .ToListAsync(ct);
        return (carteira, movs);
    }

    public async Task<decimal> ObterSaldoAsync(int pacienteId, CancellationToken ct = default)
    {
        var carteira = await ObterOuCriarAsync(pacienteId, ct);
        return carteira.Saldo;
    }

    /// <summary>
    /// Debita saldo da carteira e marca a consulta como paga.
    /// Retorna false se saldo insuficiente (sem alterar nada).
    /// </summary>
    public async Task<bool> TentarDebitarConsultaAsync(Consulta consulta, CancellationToken ct = default)
    {
        if (consulta.StatusPagamento == StatusPagamento.Pago)
            return true;

        var valor = consulta.Valor > 0 ? consulta.Valor : 50m;
        if (valor <= 0)
        {
            consulta.StatusPagamento = StatusPagamento.Pago;
            consulta.PaidAt ??= DateTime.Now;
            consulta.ConfirmacaoRecebida = true;
            if (consulta.Status == StatusConsulta.Agendada)
                consulta.Status = StatusConsulta.Confirmada;
            consulta.DataAtualizacao = DateTime.Now;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var carteira = await _db.CarteirasCliente
                .FirstOrDefaultAsync(c => c.PacienteId == consulta.PacienteId, ct);

            if (carteira is null)
            {
                carteira = new CarteiraCliente
                {
                    PacienteId = consulta.PacienteId,
                    Saldo = 0m,
                    DataCriacao = DateTime.Now
                };
                _db.CarteirasCliente.Add(carteira);
                await _db.SaveChangesAsync(ct);
            }

            // Lock pessimista (PostgreSQL); em SQLite o BeginTransaction já serializa.
            if (_db.Database.IsNpgsql())
            {
                await _db.Database.ExecuteSqlRawAsync(
                    @"SELECT ""Id"" FROM ""CarteirasCliente"" WHERE ""Id"" = {0} FOR UPDATE",
                    new object[] { carteira.Id },
                    ct);
                await _db.Entry(carteira).ReloadAsync(ct);
            }

            if (carteira.Saldo < valor)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            carteira.Saldo -= valor;
            carteira.DataAtualizacao = DateTime.Now;

            var mov = new MovimentacaoCarteira
            {
                CarteiraClienteId = carteira.Id,
                Tipo = TipoMovimentacaoCarteira.Debito,
                Valor = valor,
                SaldoApos = carteira.Saldo,
                Descricao = $"Pagamento consulta #{consulta.Id}",
                Status = StatusMovimentacaoCarteira.Confirmada,
                ConsultaId = consulta.Id,
                ExternalReference = $"consulta-carteira-{consulta.Id}",
                DataCriacao = DateTime.Now,
                DataConfirmacao = DateTime.Now
            };
            _db.MovimentacoesCarteira.Add(mov);

            consulta.StatusPagamento = StatusPagamento.Pago;
            consulta.PaidAt = DateTime.Now;
            consulta.ConfirmacaoRecebida = true;
            consulta.DataAtualizacao = DateTime.Now;
            if (consulta.Status == StatusConsulta.Agendada)
                consulta.Status = StatusConsulta.Confirmada;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Carteira paciente {PacienteId}: débito R$ {Valor} consulta {ConsultaId}. Saldo={Saldo}",
                consulta.PacienteId, valor, consulta.Id, carteira.Saldo);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Credita depósito aprovado via Mercado Pago (idempotente por payment id).
    /// </summary>
    public async Task<bool> CreditarDepositoAprovadoAsync(
        int movimentacaoId,
        string paymentId,
        decimal valorPago,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Idempotência: payment já creditado
            var jaCreditado = await _db.MovimentacoesCarteira.AnyAsync(
                m => m.MercadoPagoPaymentId == paymentId &&
                     m.Status == StatusMovimentacaoCarteira.Confirmada &&
                     m.Tipo == TipoMovimentacaoCarteira.Credito,
                ct);
            if (jaCreditado)
            {
                await tx.CommitAsync(ct);
                return true;
            }

            var mov = await _db.MovimentacoesCarteira
                .FirstOrDefaultAsync(m => m.Id == movimentacaoId, ct);
            if (mov is null)
            {
                _logger.LogWarning("Depósito carteira: movimentação {Id} não encontrada.", movimentacaoId);
                await tx.RollbackAsync(ct);
                return false;
            }

            if (mov.Status == StatusMovimentacaoCarteira.Confirmada)
            {
                if (string.IsNullOrWhiteSpace(mov.MercadoPagoPaymentId))
                    mov.MercadoPagoPaymentId = paymentId;
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            }

            var carteira = await _db.CarteirasCliente
                .FirstOrDefaultAsync(c => c.Id == mov.CarteiraClienteId, ct);
            if (carteira is null)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            if (_db.Database.IsNpgsql())
            {
                await _db.Database.ExecuteSqlRawAsync(
                    @"SELECT ""Id"" FROM ""CarteirasCliente"" WHERE ""Id"" = {0} FOR UPDATE",
                    new object[] { carteira.Id },
                    ct);
                await _db.Entry(carteira).ReloadAsync(ct);
            }

            var credito = valorPago > 0 ? valorPago : mov.Valor;
            carteira.Saldo += credito;
            carteira.DataAtualizacao = DateTime.Now;

            mov.Status = StatusMovimentacaoCarteira.Confirmada;
            mov.MercadoPagoPaymentId = paymentId;
            mov.SaldoApos = carteira.Saldo;
            mov.DataConfirmacao = DateTime.Now;
            if (credito > 0 && credito != mov.Valor)
                mov.Valor = credito;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Carteira {CarteiraId}: crédito R$ {Valor} payment={PaymentId}. Saldo={Saldo}",
                carteira.Id, credito, paymentId, carteira.Saldo);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task MarcarDepositoFalhouAsync(int movimentacaoId, string? paymentId, CancellationToken ct = default)
    {
        var mov = await _db.MovimentacoesCarteira.FirstOrDefaultAsync(m => m.Id == movimentacaoId, ct);
        if (mov is null || mov.Status == StatusMovimentacaoCarteira.Confirmada)
            return;

        mov.Status = StatusMovimentacaoCarteira.Falhou;
        if (!string.IsNullOrWhiteSpace(paymentId))
            mov.MercadoPagoPaymentId = paymentId;
        await _db.SaveChangesAsync(ct);
    }
}
