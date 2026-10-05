using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ClinicaPsi.Web.Services;

/// <summary>Upload de CNH digital e carteira CRP para validação de cadastro.</summary>
public class DocumentoCadastroService
{
    public const long MaxBytes = 8 * 1024 * 1024; // 8 MB
    public static readonly HashSet<string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DocumentoCadastroService> _logger;

    public DocumentoCadastroService(IWebHostEnvironment env, ILogger<DocumentoCadastroService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public string GetUploadRoot()
    {
        var root = Path.Combine(_env.ContentRootPath, "data", "uploads", "docs");
        Directory.CreateDirectory(root);
        return root;
    }

    public async Task<(bool Ok, string? UrlRelativa, string? Erro)> SalvarAsync(IFormFile arquivo, string prefixo)
    {
        if (arquivo == null || arquivo.Length == 0)
            return (false, null, "Envie o arquivo solicitado.");

        if (arquivo.Length > MaxBytes)
            return (false, null, "O arquivo deve ter no máximo 8 MB.");

        var extensao = Path.GetExtension(arquivo.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(extensao) || !ExtensoesPermitidas.Contains(extensao))
            return (false, null, "Formato inválido. Use JPG, PNG, WEBP ou PDF.");

        var contentType = (arquivo.ContentType ?? string.Empty).ToLowerInvariant();
        var okTypes = new[]
        {
            "image/jpeg", "image/jpg", "image/png", "image/webp", "application/pdf"
        };
        if (!okTypes.Contains(contentType) && extensao != ".pdf")
            return (false, null, "Tipo de arquivo não permitido.");

        await using var stream = arquivo.OpenReadStream();
        var header = new byte[8];
        var lidos = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (!CabecalhoValido(extensao, header, lidos))
            return (false, null, "O arquivo não parece ser um documento válido.");

        stream.Position = 0;
        var safePrefix = string.Concat((prefixo ?? "doc").Where(char.IsLetterOrDigit).Take(12));
        if (string.IsNullOrEmpty(safePrefix)) safePrefix = "doc";
        var nomeArquivo = $"{safePrefix}_{Guid.NewGuid():N}{extensao}";
        var caminhoFisico = Path.Combine(GetUploadRoot(), nomeArquivo);

        try
        {
            await using (var fs = new FileStream(caminhoFisico, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fs);
            }

            return (true, $"/uploads/docs/{nomeArquivo}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar documento {File}", nomeArquivo);
            if (File.Exists(caminhoFisico))
                File.Delete(caminhoFisico);
            return (false, null, "Não foi possível salvar o arquivo. Tente novamente.");
        }
    }

    private static bool CabecalhoValido(string extensao, byte[] header, int len)
    {
        if (len < 3) return false;
        return extensao switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => len >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".webp" => len >= 4 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46,
            ".pdf" => len >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46, // %PDF
            _ => false
        };
    }
}
