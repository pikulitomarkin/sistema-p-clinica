using ClinicaPsi.Shared.Models;
using ClinicaPsi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Serviço para registrar ações de auditoria no sistema
/// </summary>
public class AuditoriaService
{
    private readonly AppDbContext _context;

    public AuditoriaService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Registra uma ação de auditoria
    /// </summary>
    public async Task RegistrarAcaoAsync(
        string adminId,
        string adminNome,
        string usuarioAfetadoId,
        string usuarioAfetadoNome,
        string usuarioAfetadoEmail,
        TipoAcaoAuditoria acao,
        string? detalhes = null,
        string? ipAddress = null,
        string? dadosAnteriores = null,
        string? dadosNovos = null)
    {
        var auditoria = new AuditoriaUsuario
        {
            AdminId = adminId,
            AdminNome = adminNome,
            UsuarioAfetadoId = usuarioAfetadoId,
            UsuarioAfetadoNome = usuarioAfetadoNome,
            UsuarioAfetadoEmail = usuarioAfetadoEmail,
            Acao = acao,
            DataHora = DateTime.Now,
            Detalhes = detalhes,
            IpAddress = ipAddress,
            DadosAnteriores = dadosAnteriores,
            DadosNovos = dadosNovos
        };

        _context.AuditoriasUsuarios.Add(auditoria);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Obtém todos os registros de auditoria com paginação
    /// </summary>
    public async Task<(List<AuditoriaUsuario> registros, int total)> ObterAuditoriasAsync(
        int pagina = 1,
        int itensPorPagina = 20,
        string? filtroAdmin = null,
        string? filtroUsuario = null,
        TipoAcaoAuditoria? filtroAcao = null,
        DateTime? dataInicio = null,
        DateTime? dataFim = null)
    {
        var query = _context.AuditoriasUsuarios.AsQueryable();

        // Aplicar filtros
        if (!string.IsNullOrWhiteSpace(filtroAdmin))
        {
            query = query.Where(a => a.AdminNome.Contains(filtroAdmin) || a.AdminId.Contains(filtroAdmin));
        }

        if (!string.IsNullOrWhiteSpace(filtroUsuario))
        {
            query = query.Where(a => 
                a.UsuarioAfetadoNome.Contains(filtroUsuario) || 
                a.UsuarioAfetadoEmail.Contains(filtroUsuario) ||
                a.UsuarioAfetadoId.Contains(filtroUsuario));
        }

        if (filtroAcao.HasValue)
        {
            query = query.Where(a => a.Acao == filtroAcao.Value);
        }

        if (dataInicio.HasValue)
        {
            query = query.Where(a => a.DataHora >= dataInicio.Value);
        }

        if (dataFim.HasValue)
        {
            var dataFimFinal = dataFim.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.DataHora <= dataFimFinal);
        }

        var total = await query.CountAsync();

        var registros = await query
            .OrderByDescending(a => a.DataHora)
            .Skip((pagina - 1) * itensPorPagina)
            .Take(itensPorPagina)
            .ToListAsync();

        return (registros, total);
    }

    /// <summary>
    /// Obtém o histórico de ações sobre um usuário específico
    /// </summary>
    public async Task<List<AuditoriaUsuario>> ObterHistoricoUsuarioAsync(string usuarioId)
    {
        return await _context.AuditoriasUsuarios
            .Where(a => a.UsuarioAfetadoId == usuarioId)
            .OrderByDescending(a => a.DataHora)
            .ToListAsync();
    }

    /// <summary>
    /// Obtém estatísticas de auditoria
    /// </summary>
    public async Task<Dictionary<TipoAcaoAuditoria, int>> ObterEstatisticasAsync(DateTime? dataInicio = null)
    {
        var query = _context.AuditoriasUsuarios.AsQueryable();

        if (dataInicio.HasValue)
        {
            query = query.Where(a => a.DataHora >= dataInicio.Value);
        }

        var estatisticas = await query
            .GroupBy(a => a.Acao)
            .Select(g => new { Acao = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Acao, x => x.Total);

        return estatisticas;
    }

    /// <summary>
    /// Limpa registros antigos de auditoria (manutenção)
    /// </summary>
    public async Task<int> LimparRegistrosAntigosAsync(int diasParaManter = 365)
    {
        var dataLimite = DateTime.Now.AddDays(-diasParaManter);
        
        var registrosAntigos = await _context.AuditoriasUsuarios
            .Where(a => a.DataHora < dataLimite)
            .ToListAsync();

        if (registrosAntigos.Any())
        {
            _context.AuditoriasUsuarios.RemoveRange(registrosAntigos);
            await _context.SaveChangesAsync();
        }

        return registrosAntigos.Count;
    }
}
