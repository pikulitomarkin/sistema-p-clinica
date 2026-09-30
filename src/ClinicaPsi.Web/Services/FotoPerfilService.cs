using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ClinicaPsi.Web.Services;

public class FotoPerfilService
{
    public const long MaxBytes = 3 * 1024 * 1024; // 3 MB
    public static readonly HashSet<string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly Dictionary<string, byte[][]> Assinaturas = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FotoPerfilService> _logger;

    public FotoPerfilService(IWebHostEnvironment env, ILogger<FotoPerfilService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public string GetUploadRoot()
    {
        var root = Path.Combine(_env.ContentRootPath, "data", "uploads", "perfil");
        Directory.CreateDirectory(root);
        return root;
    }

    public async Task<(bool Ok, string? UrlRelativa, string? Erro)> SalvarAsync(IFormFile arquivo, string? urlAnterior = null)
    {
        if (arquivo == null || arquivo.Length == 0)
            return (false, null, "Selecione uma imagem para enviar.");

        if (arquivo.Length > MaxBytes)
            return (false, null, "A imagem deve ter no máximo 3 MB.");

        var extensao = Path.GetExtension(arquivo.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(extensao) || !ExtensoesPermitidas.Contains(extensao))
            return (false, null, "Formato inválido. Use JPG, PNG ou WEBP.");

        var contentType = (arquivo.ContentType ?? string.Empty).ToLowerInvariant();
        var contentTypesOk = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        if (!contentTypesOk.Contains(contentType))
            return (false, null, "Tipo de arquivo não permitido.");

        await using var stream = arquivo.OpenReadStream();
        var header = new byte[12];
        var lidos = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (lidos < 3 || !AssinaturaValida(extensao, header, lidos))
            return (false, null, "O arquivo não parece ser uma imagem válida.");

        stream.Position = 0;

        var nomeArquivo = $"{Guid.NewGuid():N}{extensao}";
        var caminhoFisico = Path.Combine(GetUploadRoot(), nomeArquivo);

        try
        {
            await using (var fs = new FileStream(caminhoFisico, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fs);
            }

            RemoverArquivoFisico(urlAnterior);

            return (true, $"/uploads/perfil/{nomeArquivo}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar foto de perfil {File}", nomeArquivo);
            if (File.Exists(caminhoFisico))
            {
                try { File.Delete(caminhoFisico); } catch { /* ignore */ }
            }
            return (false, null, "Não foi possível salvar a imagem. Tente novamente.");
        }
    }

    public void RemoverArquivoFisico(string? urlRelativa)
    {
        if (string.IsNullOrWhiteSpace(urlRelativa))
            return;

        try
        {
            var nome = Path.GetFileName(urlRelativa);
            if (string.IsNullOrWhiteSpace(nome) || nome.Contains(".."))
                return;

            var caminho = Path.Combine(GetUploadRoot(), nome);
            if (File.Exists(caminho))
                File.Delete(caminho);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível remover foto antiga {Url}", urlRelativa);
        }
    }

    private static bool AssinaturaValida(string extensao, byte[] header, int length)
    {
        if (!Assinaturas.TryGetValue(extensao, out var pads))
            return false;

        foreach (var pad in pads)
        {
            if (length < pad.Length)
                continue;

            var ok = true;
            for (var i = 0; i < pad.Length; i++)
            {
                if (header[i] != pad[i])
                {
                    ok = false;
                    break;
                }
            }
            if (!ok)
                continue;

            if (extensao.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                if (length < 12)
                    return false;
                return header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
            }

            return true;
        }

        return false;
    }
}
